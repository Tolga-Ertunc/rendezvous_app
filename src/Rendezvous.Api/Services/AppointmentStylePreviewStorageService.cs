using Microsoft.Extensions.Options;

namespace Rendezvous.Api.Services;

public class AppointmentStylePreviewStorageService
{
    public const long MaxFileSizeBytes = 5 * 1024 * 1024;
    private static readonly IReadOnlyDictionary<string, string> OriginalContentTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".webp"] = "image/webp"
    };

    private static readonly IReadOnlyDictionary<string, string> GeneratedExtensions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/webp"] = ".webp",
        ["image/svg+xml"] = ".svg"
    };

    private readonly string uploadRoot;

    public AppointmentStylePreviewStorageService(
        IWebHostEnvironment webHostEnvironment,
        IOptions<BusinessPhotoStorageOptions> options)
    {
        uploadRoot = string.IsNullOrWhiteSpace(options.Value.UploadRoot)
            ? Path.Combine(webHostEnvironment.ContentRootPath, "App_Data", "uploads")
            : options.Value.UploadRoot;
    }

    public async Task<StoredAppointmentStylePreview> SaveAsync(
        Guid previewId,
        IFormFile originalImage,
        GeneratedStylePreviewImage generatedImage,
        CancellationToken cancellationToken)
    {
        var originalExtension = ValidateOriginal(originalImage);
        var generatedExtension = ValidateGenerated(generatedImage);
        var directoryKey = Path.Combine("style-previews", previewId.ToString("N"));
        var directoryPath = GetAbsolutePath(directoryKey);
        Directory.CreateDirectory(directoryPath);

        var originalStorageKey = Path.Combine(directoryKey, $"original{originalExtension}");
        var generatedStorageKey = Path.Combine(directoryKey, $"generated{generatedExtension}");

        await using (var originalStream = new FileStream(
                         GetAbsolutePath(originalStorageKey),
                         FileMode.CreateNew,
                         FileAccess.Write))
        {
            await originalImage.CopyToAsync(originalStream, cancellationToken);
        }

        await File.WriteAllBytesAsync(
            GetAbsolutePath(generatedStorageKey),
            generatedImage.Bytes,
            cancellationToken);

        return new StoredAppointmentStylePreview(
            NormalizeStorageKey(originalStorageKey),
            OriginalContentTypes[originalExtension],
            originalImage.Length,
            NormalizeStorageKey(generatedStorageKey),
            generatedImage.ContentType,
            generatedImage.Bytes.Length);
    }

    public string GetAbsolutePath(string storageKey)
    {
        var fullRoot = Path.GetFullPath(uploadRoot);
        var fullPath = Path.GetFullPath(Path.Combine(fullRoot, storageKey));
        var normalizedRoot = fullRoot.EndsWith(Path.DirectorySeparatorChar)
            ? fullRoot
            : fullRoot + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(normalizedRoot, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Style preview storage path is outside the configured upload root.");
        }

        return fullPath;
    }

    private static string ValidateOriginal(IFormFile image)
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
        if (!OriginalContentTypes.TryGetValue(extension, out var expectedContentType))
        {
            throw new StylePreviewValidationException("Only JPEG, PNG, and WebP photos are supported.");
        }

        if (!string.Equals(image.ContentType, expectedContentType, StringComparison.OrdinalIgnoreCase))
        {
            throw new StylePreviewValidationException("Photo content type does not match the file extension.");
        }

        return extension;
    }

    private static string ValidateGenerated(GeneratedStylePreviewImage image)
    {
        if (image.Bytes.Length == 0)
        {
            throw new StylePreviewGenerationException("Style preview could not be generated.");
        }

        if (!GeneratedExtensions.TryGetValue(image.ContentType, out var extension))
        {
            throw new StylePreviewGenerationException("Style preview could not be generated.");
        }

        return extension;
    }

    private static string NormalizeStorageKey(string storageKey)
    {
        return storageKey.Replace(Path.DirectorySeparatorChar, '/');
    }
}

public sealed record StoredAppointmentStylePreview(
    string OriginalStorageKey,
    string OriginalContentType,
    long OriginalFileSizeBytes,
    string GeneratedStorageKey,
    string GeneratedContentType,
    long GeneratedFileSizeBytes);
