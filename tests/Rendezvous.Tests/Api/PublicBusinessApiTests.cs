using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Rendezvous.Domain.Availability;
using Rendezvous.Domain.Businesses;
using Rendezvous.Domain.Services;
using Rendezvous.Domain.Staff;
using Rendezvous.Infrastructure.Identity;
using Rendezvous.Infrastructure.Persistence;

namespace Rendezvous.Tests.Api;

public class PublicBusinessApiTests : IClassFixture<RendezvousApiFactory>
{
    private static int nextPublicNumber = 910000;
    private readonly RendezvousApiFactory factory;
    private readonly HttpClient client;

    public PublicBusinessApiTests(RendezvousApiFactory factory)
    {
        this.factory = factory;
        client = factory.CreateClient();
    }

    [Fact]
    public async Task Public_business_list_returns_only_approved_businesses_with_active_services_without_prices()
    {
        await SeedBusinessesAsync();

        var responseBody = await client.GetStringAsync("/api/public/businesses?search=Public");
        var businesses = await client.GetFromJsonAsync<List<PublicBusinessResponse>>(
            "/api/public/businesses?search=Public");

        businesses.Should().ContainSingle(business => business.Name == "Public Approved Barber");
        var business = businesses!.Single();

        business.Address.City.Should().Be("Istanbul");
        business.Services.Should().ContainSingle(service =>
            service.Name == "Haircut"
            && service.DurationMinutes == 30
            && service.CurrencyCode == "TRY");
        business.WorkingHours.Should().ContainSingle(hour =>
            hour.DayOfWeek == "Wednesday"
            && hour.OpensAt == "09:00"
            && hour.ClosesAt == "18:00");
        business.Photos.Should().ContainSingle(photo =>
            photo.ImageUrl == "/backend-api/public/business-photos/public-approved/content"
            && photo.AltText == "Public Approved Barber interior");
        business.ReviewSummary.ReviewCount.Should().Be(2);
        business.ReviewSummary.AverageRating.Should().Be(4.5m);
        business.AdditionalInformation.Should().Contain("Instant Confirmation");
        responseBody.Should().NotContain("basePriceAmount");
    }

    [Fact]
    public async Task Public_business_detail_returns_profile_sections_for_approved_business()
    {
        var businessId = await SeedBusinessesAsync();

        var business = await client.GetFromJsonAsync<PublicBusinessDetailResponse>(
            $"/api/public/businesses/{businessId}");

        business.Should().NotBeNull();
        business!.Address.City.Should().Be("Istanbul");
        business.Services.Should().ContainSingle(service =>
            service.Name == "Haircut"
            && service.CategoryName == "Hair Cut"
            && service.Description == "Classic haircut."
            && service.BasePriceAmount == 250);
        business.WorkingHours.Should().ContainSingle(hour =>
            hour.DayOfWeek == "Wednesday"
            && hour.OpensAt == "09:00"
            && hour.ClosesAt == "18:00");
        business.StaffMembers.Should().ContainSingle(staffMember =>
            staffMember.DisplayName == "Review Barber");
        business.ReviewSummary.ReviewCount.Should().Be(2);
        business.ReviewSummary.AverageRating.Should().Be(4.5m);
        business.Reviews.Should().BeInDescendingOrder(review => review.CreatedAtUtc);
        business.AdditionalInformation.Should().Contain("Instant Confirmation");
    }

    private async Task<Guid> SeedBusinessesAsync()
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ownerUserId = Guid.NewGuid();
        var ownerEmail = $"review-barber-{ownerUserId:N}@example.com";
        dbContext.Users.Add(new ApplicationUser
        {
            Id = ownerUserId,
            PublicNumber = Interlocked.Increment(ref nextPublicNumber),
            UserName = ownerEmail,
            NormalizedUserName = ownerEmail.ToUpperInvariant(),
            Email = ownerEmail,
            NormalizedEmail = ownerEmail.ToUpperInvariant(),
            FirstName = "Review",
            LastName = "Barber",
            EmailConfirmed = true
        });
        var approvedBusiness = new Business
        {
            OwnerUserId = ownerUserId,
            Name = "Public Approved Barber",
            Type = BusinessType.Barber,
            Status = BusinessStatus.Approved,
            TimeZoneId = "Europe/Istanbul",
            AddressLine = "Bagdat Caddesi 120",
            District = "Maltepe",
            City = "Istanbul",
            Country = "Turkey",
            Description = "Public barber profile.",
            SupportsInstantConfirmation = true
        };
        var pendingBusiness = new Business
        {
            OwnerUserId = ownerUserId,
            Name = "Public Pending Barber",
            Type = BusinessType.Barber,
            Status = BusinessStatus.PendingApproval,
            TimeZoneId = "Europe/Istanbul"
        };

        dbContext.Businesses.AddRange(approvedBusiness, pendingBusiness);
        dbContext.BusinessServices.AddRange(
            new BusinessService
            {
                BusinessId = approvedBusiness.Id,
                Name = "Haircut",
                CategoryName = "Hair Cut",
                Description = "Classic haircut.",
                DurationMinutes = 30,
                BasePriceAmount = 250,
                CurrencyCode = "TRY",
                IsActive = true
            },
            new BusinessService
            {
                BusinessId = approvedBusiness.Id,
                Name = "Inactive Color",
                CategoryName = "Hair Dye",
                Description = "Inactive color service.",
                DurationMinutes = 90,
                BasePriceAmount = 900,
                CurrencyCode = "TRY",
                IsActive = false
            },
            new BusinessService
            {
                BusinessId = pendingBusiness.Id,
                Name = "Pending Service",
                CategoryName = "Featured",
                Description = "Pending service.",
                DurationMinutes = 30,
                BasePriceAmount = 100,
                CurrencyCode = "TRY",
                IsActive = true
            });
        dbContext.BusinessWorkingHours.Add(new BusinessWorkingHour
        {
            BusinessId = approvedBusiness.Id,
            DayOfWeek = DayOfWeek.Wednesday,
            OpensAt = new TimeOnly(9, 0),
            ClosesAt = new TimeOnly(18, 0)
        });
        dbContext.BusinessPhotos.Add(new BusinessPhoto
        {
            BusinessId = approvedBusiness.Id,
            ImageUrl = "/backend-api/public/business-photos/public-approved/content",
            AltText = "Public Approved Barber interior",
            SortOrder = 0
        });
        dbContext.StaffMembers.Add(new StaffMember
        {
            BusinessId = approvedBusiness.Id,
            UserId = ownerUserId,
            IsActive = true
        });
        dbContext.BusinessReviews.AddRange(
            new BusinessReview
            {
                BusinessId = approvedBusiness.Id,
                CustomerName = "Reyes B",
                CustomerInitial = "R",
                Rating = 5.0m,
                Comment = "Great cut.",
                CreatedAtUtc = DateTimeOffset.UtcNow.AddHours(-1),
                IsPublic = true
            },
            new BusinessReview
            {
                BusinessId = approvedBusiness.Id,
                CustomerName = "Donna P",
                CustomerInitial = "D",
                Rating = 4.0m,
                Comment = "Clean service.",
                CreatedAtUtc = DateTimeOffset.UtcNow.AddHours(-3),
                IsPublic = true
            });

        await dbContext.SaveChangesAsync();
        return approvedBusiness.Id;
    }

    private sealed record PublicBusinessResponse(
        Guid Id,
        string Name,
        string Type,
        string TimeZoneId,
        PublicBusinessAddressResponse Address,
        IReadOnlyList<PublicBusinessServiceResponse> Services,
        IReadOnlyList<PublicBusinessWorkingHourResponse> WorkingHours,
        IReadOnlyList<PublicBusinessPhotoResponse> Photos,
        PublicBusinessReviewSummaryResponse ReviewSummary,
        IReadOnlyList<string> AdditionalInformation);

    private sealed record PublicBusinessServiceResponse(
        Guid Id,
        string Name,
        int DurationMinutes,
        string CurrencyCode);

    private sealed record PublicBusinessDetailResponse(
        Guid Id,
        string Name,
        string Type,
        string TimeZoneId,
        PublicBusinessAddressResponse Address,
        string Description,
        IReadOnlyList<PublicBusinessDetailServiceResponse> Services,
        IReadOnlyList<PublicBusinessWorkingHourResponse> WorkingHours,
        IReadOnlyList<PublicBusinessStaffMemberResponse> StaffMembers,
        IReadOnlyList<PublicBusinessPhotoResponse> Photos,
        PublicBusinessReviewSummaryResponse ReviewSummary,
        IReadOnlyList<PublicBusinessReviewResponse> Reviews,
        IReadOnlyList<string> AdditionalInformation);

    private sealed record PublicBusinessDetailServiceResponse(
        Guid Id,
        string Name,
        string CategoryName,
        string Description,
        int DurationMinutes,
        decimal BasePriceAmount,
        string CurrencyCode);

    private sealed record PublicBusinessAddressResponse(
        string AddressLine,
        string District,
        string City,
        string Country);

    private sealed record PublicBusinessWorkingHourResponse(
        string DayOfWeek,
        string OpensAt,
        string ClosesAt);

    private sealed record PublicBusinessStaffMemberResponse(
        Guid Id,
        string DisplayName);

    private sealed record PublicBusinessPhotoResponse(
        Guid Id,
        string ImageUrl,
        string AltText,
        int SortOrder);

    private sealed record PublicBusinessReviewSummaryResponse(
        decimal AverageRating,
        int ReviewCount);

    private sealed record PublicBusinessReviewResponse(
        Guid Id,
        string CustomerName,
        string CustomerInitial,
        decimal Rating,
        string Comment,
        DateTimeOffset CreatedAtUtc);
}
