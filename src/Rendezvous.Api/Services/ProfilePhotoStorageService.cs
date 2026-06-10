using Microsoft.Extensions.Options;

namespace Rendezvous.Api.Services;

public class ProfilePhotoStorageService
{
    public const long MaxFileSizeBytes = 5 * 1024 * 1024;
    public const long MaxUploadRequestSizeBytes = 6 * 1024 * 1024;
    private static readonly IReadOnlyDictionary<string, string> AllowedContentTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".webp"] = "image/webp"
    };

    private readonly string uploadRoot;

    public ProfilePhotoStorageService(
        IWebHostEnvironment webHostEnvironment,
        IOptions<BusinessPhotoStorageOptions> options)
    {
        uploadRoot = string.IsNullOrWhiteSpace(options.Value.UploadRoot)
            ? Path.Combine(webHostEnvironment.ContentRootPath, "App_Data", "uploads")
            : options.Value.UploadRoot;
    }

    public async Task<StoredProfilePhoto> SaveAsync(
        Guid userId,
        Guid profilePhotoId,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        var extension = Validate(file);
        var storageKey = Path.Combine(
            "profile-photos",
            userId.ToString("N"),
            $"{profilePhotoId:N}{extension}");
        var absolutePath = GetAbsolutePath(storageKey);
        var directory = Path.GetDirectoryName(absolutePath)
            ?? throw new InvalidOperationException("Profile photo storage directory could not be resolved.");

        Directory.CreateDirectory(directory);

        await using var stream = new FileStream(absolutePath, FileMode.CreateNew, FileAccess.Write);
        await file.CopyToAsync(stream, cancellationToken);

        return new StoredProfilePhoto(
            storageKey.Replace(Path.DirectorySeparatorChar, '/'),
            AllowedContentTypes[extension],
            file.Length);
    }

    public void Delete(string? storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
        {
            return;
        }

        var absolutePath = GetAbsolutePath(storageKey);
        if (File.Exists(absolutePath))
        {
            File.Delete(absolutePath);
        }
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
            throw new InvalidOperationException("Profile photo storage path is outside the configured upload root.");
        }

        return fullPath;
    }

    private static string Validate(IFormFile file)
    {
        if (file.Length <= 0)
        {
            throw new ProfilePhotoValidationException("Photo file is required.");
        }

        if (file.Length > MaxFileSizeBytes)
        {
            throw new ProfilePhotoValidationException("Photo file cannot exceed 5MB.");
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedContentTypes.TryGetValue(extension, out var expectedContentType))
        {
            throw new ProfilePhotoValidationException("Only JPEG, PNG, and WebP photos are supported.");
        }

        if (!string.Equals(file.ContentType, expectedContentType, StringComparison.OrdinalIgnoreCase))
        {
            throw new ProfilePhotoValidationException("Photo content type does not match the file extension.");
        }

        return extension;
    }
}

public sealed record StoredProfilePhoto(
    string StorageKey,
    string ContentType,
    long FileSizeBytes);

public class ProfilePhotoValidationException : Exception
{
    public ProfilePhotoValidationException(string message)
        : base(message)
    {
    }
}
