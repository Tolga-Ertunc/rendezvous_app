using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rendezvous.Api.Email;
using Rendezvous.Domain.Businesses;
using Rendezvous.Domain.Services;
using Rendezvous.Infrastructure.Persistence;

namespace Rendezvous.Tests.Api;

public class OwnerBusinessProfileApiTests : IClassFixture<RendezvousApiFactory>
{
    private readonly RendezvousApiFactory factory;
    private readonly HttpClient client;

    public OwnerBusinessProfileApiTests(RendezvousApiFactory factory)
    {
        this.factory = factory;
        client = factory.CreateClient();
    }

    [Fact]
    public async Task Owner_can_update_public_profile_and_service_category()
    {
        var (ownerToken, owner) = await RegisterAndGetCurrentUserAsync("profile-owner@example.com");
        var (businessId, serviceId) = await SeedOwnedBusinessAsync(owner.Id);
        client.DefaultRequestHeaders.Authorization = new("Bearer", ownerToken);

        var profileResponse = await client.PutAsJsonAsync(
            $"/api/owner/businesses/{businessId}/profile",
            new
            {
                name = "Updated Cuts",
                timeZoneId = "Europe/Istanbul",
                addressLine = "Bagdat Caddesi 250",
                district = "Kadikoy",
                city = "Istanbul",
                country = "Turkey",
                description = "Clean public profile copy.",
                supportsInstantConfirmation = true,
                supportsPayByApp = true,
                isPetFriendly = false,
                isKidFriendly = true,
                isNearPublicTransport = true,
                usesOrganicProducts = true,
                usesVeganProducts = false,
                isEnvironmentallyFriendly = true
            });
        var categoryResponse = await client.PostAsJsonAsync(
            $"/api/owner/businesses/{businessId}/service-categories",
            new
            {
                name = "Adult Haircut Service"
            });
        var serviceResponse = await client.PutAsJsonAsync(
            $"/api/owner/businesses/{businessId}/services/{serviceId}",
            new
            {
                name = "Haircut",
                categoryName = "Adult Haircut Service",
                durationMinutes = 45,
                basePriceAmount = 700,
                currencyCode = "TRY",
                isActive = true
            });

        profileResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        categoryResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        serviceResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var ownerBusiness = await client.GetFromJsonAsync<OwnerBusinessDetailResponse>(
            $"/api/owner/businesses/{businessId}");
        ownerBusiness!.Name.Should().Be("Updated Cuts");
        ownerBusiness.District.Should().Be("Kadikoy");
        ownerBusiness.SupportsPayByApp.Should().BeTrue();
        ownerBusiness.ServiceCategories.Should().Contain(category =>
            category.Name == "Adult Haircut Service" && !category.IsSystem);
        ownerBusiness.Services.Should().ContainSingle(service =>
            service.Id == serviceId && service.CategoryName == "Adult Haircut Service");

        client.DefaultRequestHeaders.Authorization = null;
        var publicBusiness = await client.GetFromJsonAsync<PublicBusinessDetailResponse>(
            $"/api/public/businesses/{businessId}");
        publicBusiness!.Name.Should().Be("Updated Cuts");
        publicBusiness.Address.District.Should().Be("Kadikoy");
        publicBusiness.Services.Should().ContainSingle(service =>
            service.CategoryName == "Adult Haircut Service" && service.BasePriceAmount == 700);
        publicBusiness.AdditionalInformation.Should().Contain("Pay by app");
    }

    [Fact]
    public async Task Owner_photo_upload_uses_generated_storage_name_and_public_content_endpoint()
    {
        var (ownerToken, owner) = await RegisterAndGetCurrentUserAsync("photo-owner@example.com");
        var (businessId, _) = await SeedOwnedBusinessAsync(owner.Id);
        client.DefaultRequestHeaders.Authorization = new("Bearer", ownerToken);
        using var form = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent([1, 2, 3, 4]);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(fileContent, "file", "../../unsafe-name.png");
        form.Add(new StringContent("Salon hero"), "altText");

        var response = await client.PostAsync(
            $"/api/owner/businesses/{businessId}/photos",
            form);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var photo = await response.Content.ReadFromJsonAsync<OwnerBusinessPhotoResponse>();
        photo!.ImageUrl.Should().StartWith("/backend-api/public/business-photos/");
        photo.ImageUrl.Should().NotContain("unsafe-name");

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var storedPhoto = await dbContext.BusinessPhotos.SingleAsync(candidate => candidate.Id == photo.Id);
        storedPhoto.StorageKey.Should().NotContain("unsafe-name");
        storedPhoto.StorageKey.Should().EndWith(".png");
        storedPhoto.ContentType.Should().Be("image/png");
        storedPhoto.FileSizeBytes.Should().Be(4);

        client.DefaultRequestHeaders.Authorization = null;
        var contentResponse = await client.GetAsync($"/api/public/business-photos/{photo.Id}/content");
        contentResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        contentResponse.Content.Headers.ContentType!.MediaType.Should().Be("image/png");
    }

    [Fact]
    public async Task Owner_photo_upload_rejects_invalid_extension_and_more_than_four_photos()
    {
        var (ownerToken, owner) = await RegisterAndGetCurrentUserAsync("photo-limit-owner@example.com");
        var (businessId, _) = await SeedOwnedBusinessAsync(owner.Id);
        client.DefaultRequestHeaders.Authorization = new("Bearer", ownerToken);

        using var invalidForm = CreatePhotoForm("bad.gif", "image/gif");
        var invalidResponse = await client.PostAsync(
            $"/api/owner/businesses/{businessId}/photos",
            invalidForm);

        invalidResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        for (var index = 0; index < 4; index++)
        {
            using var form = CreatePhotoForm($"photo-{index}.jpg", "image/jpeg");
            var uploadResponse = await client.PostAsync(
                $"/api/owner/businesses/{businessId}/photos",
                form);
            uploadResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        using var overflowForm = CreatePhotoForm("overflow.jpg", "image/jpeg");
        var overflowResponse = await client.PostAsync(
            $"/api/owner/businesses/{businessId}/photos",
            overflowForm);

        overflowResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task<(Guid BusinessId, Guid ServiceId)> SeedOwnedBusinessAsync(Guid ownerUserId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var business = new Business
        {
            OwnerUserId = ownerUserId,
            Name = "Owner Profile Barber",
            Type = BusinessType.Barber,
            Status = BusinessStatus.Approved,
            TimeZoneId = "Europe/Istanbul"
        };
        var service = new BusinessService
        {
            BusinessId = business.Id,
            Name = "Haircut",
            CategoryName = "Hair Cut",
            DurationMinutes = 30,
            BasePriceAmount = 500,
            CurrencyCode = "TRY",
            IsActive = true
        };

        dbContext.Businesses.Add(business);
        dbContext.BusinessServiceCategories.AddRange(
            new BusinessServiceCategory
            {
                BusinessId = business.Id,
                Name = "Featured",
                SortOrder = 0,
                IsSystem = true
            },
            new BusinessServiceCategory
            {
                BusinessId = business.Id,
                Name = "Hair Cut",
                SortOrder = 1,
                IsSystem = false
            });
        dbContext.BusinessMemberships.Add(new BusinessMembership
        {
            BusinessId = business.Id,
            UserId = ownerUserId,
            Role = BusinessMembershipRole.Owner,
            Status = BusinessMembershipStatus.Active,
            CreatedAtUtc = DateTime.UtcNow
        });
        dbContext.BusinessServices.Add(service);
        await dbContext.SaveChangesAsync();

        return (business.Id, service.Id);
    }

    private async Task<(string Token, CurrentUserResponse User)> RegisterAndGetCurrentUserAsync(string email)
    {
        var token = await RegisterAsync(email);
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        var user = await client.GetFromJsonAsync<CurrentUserResponse>("/api/auth/me");

        return (token, user!);
    }

    private async Task<string> RegisterAsync(string email)
    {
        var registerResponse = await client.PostAsJsonAsync(
            "/api/auth/register",
            new
            {
                email,
                password = "StrongPass123!",
                confirmPassword = "StrongPass123!"
            });
        registerResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var emailSender = factory.Services.GetRequiredService<InMemoryEmailSender>();
        var message = emailSender.SentMessages
            .Last(message => message.To.Equals(email, StringComparison.OrdinalIgnoreCase));
        var match = Regex.Match(message.TextBody, @"\b\d{6}\b");
        match.Success.Should().BeTrue();

        var confirmResponse = await client.PostAsJsonAsync(
            "/api/auth/confirm-email",
            new
            {
                email,
                code = match.Value
            });
        confirmResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                email,
                password = "StrongPass123!"
            });
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var tokenResponse = await loginResponse.Content.ReadFromJsonAsync<AuthTokenResponse>();

        return tokenResponse!.AccessToken;
    }

    private static MultipartFormDataContent CreatePhotoForm(string fileName, string contentType)
    {
        var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent([1, 2, 3, 4]);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(fileContent, "file", fileName);
        form.Add(new StringContent(fileName), "altText");

        return form;
    }

    private sealed record AuthTokenResponse(string AccessToken);

    private sealed record CurrentUserResponse(Guid Id);

    private sealed record OwnerBusinessDetailResponse(
        Guid Id,
        string Name,
        string District,
        bool SupportsPayByApp,
        IReadOnlyList<OwnerBusinessServiceCategoryResponse> ServiceCategories,
        IReadOnlyList<OwnerBusinessServiceResponse> Services);

    private sealed record OwnerBusinessServiceCategoryResponse(
        Guid Id,
        string Name,
        int SortOrder,
        bool IsSystem);

    private sealed record OwnerBusinessServiceResponse(
        Guid Id,
        string CategoryName);

    private sealed record OwnerBusinessPhotoResponse(
        Guid Id,
        string ImageUrl);

    private sealed record PublicBusinessDetailResponse(
        Guid Id,
        string Name,
        PublicBusinessAddressResponse Address,
        IReadOnlyList<PublicBusinessServiceResponse> Services,
        IReadOnlyList<string> AdditionalInformation);

    private sealed record PublicBusinessAddressResponse(string District);

    private sealed record PublicBusinessServiceResponse(
        string CategoryName,
        decimal BasePriceAmount);
}
