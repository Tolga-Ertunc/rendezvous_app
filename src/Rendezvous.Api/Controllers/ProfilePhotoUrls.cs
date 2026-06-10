namespace Rendezvous.Api.Controllers;

internal static class ProfilePhotoUrls
{
    public static string? Build(Guid? profilePhotoId)
    {
        return profilePhotoId.HasValue
            ? $"/backend-api/public/profile-photos/{profilePhotoId.Value}/content"
            : null;
    }
}
