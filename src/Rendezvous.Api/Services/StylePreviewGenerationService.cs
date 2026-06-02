using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace Rendezvous.Api.Services;

public class StylePreviewGenerationService
{
    public const long MaxFileSizeBytes = 5 * 1024 * 1024;
    public const long MaxUploadRequestSizeBytes = 6 * 1024 * 1024;
    private const string PlaceholderApiKey = "PASTE_GOOGLE_STYLE_PREVIEW_API_KEY_HERE";
    private static readonly IReadOnlyDictionary<string, string> AllowedContentTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".webp"] = "image/webp"
    };

    private readonly HttpClient httpClient;
    private readonly IWebHostEnvironment environment;
    private readonly ILogger<StylePreviewGenerationService> logger;
    private readonly StylePreviewOptions options;

    public StylePreviewGenerationService(
        HttpClient httpClient,
        IWebHostEnvironment environment,
        ILogger<StylePreviewGenerationService> logger,
        IOptions<StylePreviewOptions> options)
    {
        this.httpClient = httpClient;
        this.environment = environment;
        this.logger = logger;
        this.options = options.Value;
    }

    public async Task<StylePreviewGenerationResult> GenerateAsync(
        IFormFile image,
        string prompt,
        CancellationToken cancellationToken)
    {
        var contentType = Validate(image, prompt);
        var normalizedPrompt = prompt.Trim();

        if (IsPlaceholderConfiguration())
        {
            if (environment.IsDevelopment() || environment.IsEnvironment("Testing"))
            {
                return CreatePlaceholderResult(normalizedPrompt);
            }

            throw new StylePreviewConfigurationException("Style preview generation is not configured.");
        }

        await using var imageStream = image.OpenReadStream();
        using var memoryStream = new MemoryStream();
        await imageStream.CopyToAsync(memoryStream, cancellationToken);

        var request = new GeminiGenerateContentRequest(
            [
                new GeminiContent(
                    [
                        GeminiPart.FromText(BuildGenerationPrompt(normalizedPrompt)),
                        GeminiPart.FromImage(contentType, Convert.ToBase64String(memoryStream.ToArray()))
                    ])
            ]);

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://generativelanguage.googleapis.com/v1beta/models/{options.Model}:generateContent")
        {
            Content = JsonContent.Create(request)
        };
        httpRequest.Headers.Add("x-goog-api-key", options.ApiKey);

        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning(
                "Style preview generation failed with upstream status {StatusCode}. Response: {ResponseBody}",
                (int)response.StatusCode,
                TruncateForLog(errorBody));

            if (response.StatusCode == HttpStatusCode.TooManyRequests && CanUsePlaceholderFallback())
            {
                return CreatePlaceholderResult(normalizedPrompt);
            }

            throw new StylePreviewGenerationException("Style preview could not be generated.");
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        var imagePart = ExtractInlineImage(responseBody);

        if (imagePart is null)
        {
            logger.LogWarning(
                "Style preview response did not contain inline image data. Response: {ResponseBody}",
                TruncateForLog(responseBody));

            throw new StylePreviewGenerationException("Style preview could not be generated.");
        }

        var mimeType = string.IsNullOrWhiteSpace(imagePart.MimeType)
            ? "image/png"
            : imagePart.MimeType;

        return new StylePreviewGenerationResult(
            Guid.NewGuid(),
            $"data:{mimeType};base64,{imagePart.Data}",
            normalizedPrompt,
            false);
    }

    private static GeminiInlineImage? ExtractInlineImage(string responseBody)
    {
        using var document = JsonDocument.Parse(responseBody);

        if (!document.RootElement.TryGetProperty("candidates", out var candidates)
            || candidates.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var candidate in candidates.EnumerateArray())
        {
            if (!candidate.TryGetProperty("content", out var content)
                || !content.TryGetProperty("parts", out var parts)
                || parts.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var part in parts.EnumerateArray())
            {
                if (!TryGetInlineData(part, out var inlineData)
                    || !TryGetString(inlineData, "data", out var data)
                    || string.IsNullOrWhiteSpace(data))
                {
                    continue;
                }

                TryGetString(inlineData, "mimeType", out var mimeType);
                if (string.IsNullOrWhiteSpace(mimeType))
                {
                    TryGetString(inlineData, "mime_type", out mimeType);
                }

                return new GeminiInlineImage(data, mimeType);
            }
        }

        return null;
    }

    private static bool TryGetInlineData(JsonElement part, out JsonElement inlineData)
    {
        if (part.TryGetProperty("inlineData", out inlineData))
        {
            return true;
        }

        return part.TryGetProperty("inline_data", out inlineData);
    }

    private static bool TryGetString(JsonElement element, string propertyName, out string? value)
    {
        value = null;

        if (!element.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString();
        return true;
    }

    private bool IsPlaceholderConfiguration()
    {
        return string.IsNullOrWhiteSpace(options.ApiKey)
            || string.Equals(options.ApiKey, PlaceholderApiKey, StringComparison.Ordinal);
    }

    private bool CanUsePlaceholderFallback()
    {
        return environment.IsDevelopment() || environment.IsEnvironment("Testing");
    }

    private static string TruncateForLog(string value)
    {
        const int maxLength = 1000;

        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Length > maxLength ? value[..maxLength] : value;
    }

    private static string Validate(IFormFile image, string prompt)
    {
        if (image.Length <= 0)
        {
            throw new StylePreviewValidationException("Photo file is required.");
        }

        if (image.Length > MaxFileSizeBytes)
        {
            throw new StylePreviewValidationException("Photo file cannot exceed 5MB.");
        }

        var extension = Path.GetExtension(image.FileName).ToLowerInvariant();
        if (!AllowedContentTypes.TryGetValue(extension, out var expectedContentType))
        {
            throw new StylePreviewValidationException("Only JPEG, PNG, and WebP photos are supported.");
        }

        if (!string.Equals(image.ContentType, expectedContentType, StringComparison.OrdinalIgnoreCase))
        {
            throw new StylePreviewValidationException("Photo content type does not match the file extension.");
        }

        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new StylePreviewValidationException("Prompt is required.");
        }

        if (prompt.Trim().Length > 1000)
        {
            throw new StylePreviewValidationException("Prompt cannot exceed 1000 characters.");
        }

        return expectedContentType;
    }

    private static string BuildGenerationPrompt(string prompt)
    {
        return "Create a realistic haircut or grooming style preview using the uploaded person photo. "
            + "Preserve the person's identity, face shape, expression, skin tone, camera angle, and background as much as possible. "
            + "Change only the haircut, beard, or grooming style requested by the customer. "
            + "Keep the result suitable as a barber reference image. Customer request: "
            + prompt;
    }

    private static StylePreviewGenerationResult CreatePlaceholderResult(string prompt)
    {
        var safePrompt = EscapeSvgText(prompt);
        var svg = $"""
            <svg xmlns="http://www.w3.org/2000/svg" width="960" height="960" viewBox="0 0 960 960">
              <rect width="960" height="960" fill="#f4f4f5"/>
              <rect x="120" y="120" width="720" height="720" rx="48" fill="#ffffff" stroke="#d4d4d8" stroke-width="4"/>
              <circle cx="480" cy="376" r="128" fill="#e4e4e7"/>
              <path d="M304 716c24-112 96-176 176-176s152 64 176 176" fill="#d4d4d8"/>
              <text x="480" y="188" text-anchor="middle" font-family="Arial, sans-serif" font-size="38" font-weight="700" fill="#111111">Style preview placeholder</text>
              <text x="480" y="794" text-anchor="middle" font-family="Arial, sans-serif" font-size="26" fill="#71717a">{safePrompt}</text>
            </svg>
            """;
        var data = Convert.ToBase64String(Encoding.UTF8.GetBytes(svg));

        return new StylePreviewGenerationResult(
            Guid.NewGuid(),
            $"data:image/svg+xml;base64,{data}",
            prompt,
            true);
    }

    private static string EscapeSvgText(string value)
    {
        var trimmed = value.Length > 44 ? value[..44] + "..." : value;

        return trimmed
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal);
    }
}

public sealed record StylePreviewGenerationResult(
    Guid PreviewId,
    string ImageUrl,
    string Prompt,
    bool IsPlaceholder);

public class StylePreviewValidationException : Exception
{
    public StylePreviewValidationException(string message)
        : base(message)
    {
    }
}

public class StylePreviewConfigurationException : Exception
{
    public StylePreviewConfigurationException(string message)
        : base(message)
    {
    }
}

public class StylePreviewGenerationException : Exception
{
    public StylePreviewGenerationException(string message)
        : base(message)
    {
    }
}

internal sealed record GeminiGenerateContentRequest(
    [property: JsonPropertyName("contents")] IReadOnlyList<GeminiContent> Contents);

internal sealed record GeminiContent(
    [property: JsonPropertyName("parts")] IReadOnlyList<GeminiPart> Parts);

internal sealed record GeminiPart(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [property: JsonPropertyName("text")] string? Text,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [property: JsonPropertyName("inline_data")] GeminiInlineData? InlineData)
{
    public static GeminiPart FromText(string text)
    {
        return new GeminiPart(text, null);
    }

    public static GeminiPart FromImage(string mimeType, string data)
    {
        return new GeminiPart(null, new GeminiInlineData(mimeType, data));
    }
}

internal sealed record GeminiInlineData(
    [property: JsonPropertyName("mime_type")] string MimeType,
    [property: JsonPropertyName("data")] string Data);

internal sealed record GeminiInlineImage(string Data, string? MimeType);
