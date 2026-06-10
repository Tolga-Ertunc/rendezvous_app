using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rendezvous.Api.Email;
using Rendezvous.Domain.Appointments;
using Rendezvous.Domain.Availability;
using Rendezvous.Domain.Businesses;
using Rendezvous.Domain.Services;
using Rendezvous.Domain.Staff;
using Rendezvous.Infrastructure.Persistence;

namespace Rendezvous.Tests.Api;

public class ProfilePhotoApiTests : IClassFixture<RendezvousApiFactory>
{
    private readonly RendezvousApiFactory factory;
    private readonly HttpClient client;

    public ProfilePhotoApiTests(RendezvousApiFactory factory)
    {
        this.factory = factory;
        client = factory.CreateClient();
    }

    [Fact]
    public async Task User_can_upload_replace_and_publicly_use_profile_photo()
    {
        var (customerToken, customer) = await RegisterAndGetCurrentUserAsync("profile-photo-customer@example.com");
        var (staffToken, staffUser) = await RegisterAndGetCurrentUserAsync("profile-photo-staff@example.com");

        client.DefaultRequestHeaders.Authorization = new("Bearer", customerToken);
        using var firstForm = CreatePhotoForm("../../unsafe-name.png", "image/png");
        var firstUploadResponse = await client.PostAsync("/api/profile/photo", firstForm);

        firstUploadResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var firstUpload = await firstUploadResponse.Content.ReadFromJsonAsync<CurrentUserResponse>();
        firstUpload!.ProfilePhotoUrl.Should().StartWith("/backend-api/public/profile-photos/");
        firstUpload.ProfilePhotoUrl.Should().NotContain("unsafe-name");
        var firstPhotoId = ExtractProfilePhotoId(firstUpload.ProfilePhotoUrl!);

        client.DefaultRequestHeaders.Authorization = null;
        var firstContentResponse = await client.GetAsync($"/api/public/profile-photos/{firstPhotoId}/content");
        firstContentResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        firstContentResponse.Content.Headers.ContentType!.MediaType.Should().Be("image/png");

        client.DefaultRequestHeaders.Authorization = new("Bearer", customerToken);
        using var secondForm = CreatePhotoForm("replacement.webp", "image/webp");
        var secondUploadResponse = await client.PostAsync("/api/profile/photo", secondForm);

        secondUploadResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var secondUpload = await secondUploadResponse.Content.ReadFromJsonAsync<CurrentUserResponse>();
        secondUpload!.ProfilePhotoUrl.Should().StartWith("/backend-api/public/profile-photos/");
        var secondPhotoId = ExtractProfilePhotoId(secondUpload.ProfilePhotoUrl!);
        secondPhotoId.Should().NotBe(firstPhotoId);

        client.DefaultRequestHeaders.Authorization = null;
        (await client.GetAsync($"/api/public/profile-photos/{firstPhotoId}/content"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.GetAsync($"/api/public/profile-photos/{secondPhotoId}/content"))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        client.DefaultRequestHeaders.Authorization = new("Bearer", staffToken);
        using var staffPhotoForm = CreatePhotoForm("staff.jpg", "image/jpeg");
        var staffUploadResponse = await client.PostAsync("/api/profile/photo", staffPhotoForm);
        staffUploadResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var staffUpload = await staffUploadResponse.Content.ReadFromJsonAsync<CurrentUserResponse>();

        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7));
        var setup = await SeedBusinessAsync(staffUser.Id, customer.Id, date);

        client.DefaultRequestHeaders.Authorization = new("Bearer", customerToken);
        var reviewResponse = await client.PostAsJsonAsync(
            $"/api/customer/appointments/{setup.AppointmentId}/review",
            new
            {
                rating = 5,
                comment = "Great service."
            });
        reviewResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        client.DefaultRequestHeaders.Authorization = null;
        var publicBusiness = await client.GetFromJsonAsync<PublicBusinessDetailResponse>(
            $"/api/public/businesses/{setup.BusinessId}");
        publicBusiness!.StaffMembers.Should().ContainSingle(staff =>
            staff.Id == setup.StaffMemberId
            && staff.ProfilePhotoUrl == staffUpload!.ProfilePhotoUrl);
        publicBusiness.Reviews.Should().ContainSingle(review =>
            review.CustomerProfilePhotoUrl == secondUpload.ProfilePhotoUrl);

        var availability = await client.GetFromJsonAsync<BookingAvailabilityResponse>(
            $"/api/booking/businesses/{setup.BusinessId}/services/{setup.ServiceId}/availability?date={date:yyyy-MM-dd}");
        availability!.Slots.SelectMany(slot => slot.StaffMembers).Should().Contain(staff =>
            staff.StaffMemberId == setup.StaffMemberId
            && staff.ProfilePhotoUrl == staffUpload!.ProfilePhotoUrl);
    }

    [Fact]
    public async Task Profile_photo_upload_rejects_invalid_type_and_large_file()
    {
        var (token, _) = await RegisterAndGetCurrentUserAsync("profile-photo-invalid@example.com");
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        using var invalidForm = CreatePhotoForm("bad.gif", "image/gif");
        var invalidResponse = await client.PostAsync("/api/profile/photo", invalidForm);

        invalidResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var largeForm = CreatePhotoForm("large.png", "image/png", 6 * 1024 * 1024);
        var largeResponse = await client.PostAsync("/api/profile/photo", largeForm);

        largeResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task<SeededBusiness> SeedBusinessAsync(Guid staffUserId, Guid customerUserId, DateOnly date)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var business = new Business
        {
            OwnerUserId = staffUserId,
            Name = "Profile Photo Barber",
            Type = BusinessType.Barber,
            Status = BusinessStatus.Approved,
            TimeZoneId = "Europe/Istanbul"
        };
        var service = new BusinessService
        {
            BusinessId = business.Id,
            Name = "Haircut",
            CategoryName = "Hair Cut",
            Description = "Profile photo service.",
            DurationMinutes = 30,
            BasePriceAmount = 500,
            CurrencyCode = "TRY",
            IsActive = true
        };
        var staffMember = new StaffMember
        {
            BusinessId = business.Id,
            UserId = staffUserId,
            IsActive = true
        };
        var appointment = new Appointment
        {
            BusinessId = business.Id,
            BusinessServiceId = service.Id,
            StaffMemberId = staffMember.Id,
            CustomerUserId = customerUserId,
            StartsAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
            EndsAtUtc = DateTimeOffset.UtcNow.AddDays(-1).AddMinutes(30),
            Status = AppointmentStatus.Completed,
            PriceAmount = 500,
            CurrencyCode = "TRY"
        };

        dbContext.Businesses.Add(business);
        dbContext.BusinessServiceCategories.Add(new BusinessServiceCategory
        {
            BusinessId = business.Id,
            Name = "Hair Cut",
            SortOrder = 0,
            IsSystem = true
        });
        dbContext.BusinessServices.Add(service);
        dbContext.StaffMembers.Add(staffMember);
        dbContext.BusinessWorkingHours.Add(new BusinessWorkingHour
        {
            BusinessId = business.Id,
            DayOfWeek = date.DayOfWeek,
            OpensAt = new TimeOnly(9, 0),
            ClosesAt = new TimeOnly(17, 0)
        });
        dbContext.StaffWorkingHours.Add(new StaffWorkingHour
        {
            StaffMemberId = staffMember.Id,
            DayOfWeek = date.DayOfWeek,
            StartsAt = new TimeOnly(9, 0),
            EndsAt = new TimeOnly(17, 0)
        });
        dbContext.Appointments.Add(appointment);
        await dbContext.SaveChangesAsync();

        return new SeededBusiness(business.Id, service.Id, staffMember.Id, appointment.Id);
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
                firstName = "Profile",
                lastName = "Photo",
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

    private static MultipartFormDataContent CreatePhotoForm(
        string fileName,
        string contentType,
        int byteCount = 4)
    {
        var form = new MultipartFormDataContent();
        var payload = new byte[byteCount];
        payload[0] = 1;
        var fileContent = new ByteArrayContent(payload);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(fileContent, "file", fileName);

        return form;
    }

    private static Guid ExtractProfilePhotoId(string profilePhotoUrl)
    {
        var match = Regex.Match(profilePhotoUrl, @"/profile-photos/([^/]+)/content");
        match.Success.Should().BeTrue();

        return Guid.Parse(match.Groups[1].Value);
    }

    private sealed record AuthTokenResponse(string AccessToken);

    private sealed record CurrentUserResponse(
        Guid Id,
        string? ProfilePhotoUrl);

    private sealed record SeededBusiness(
        Guid BusinessId,
        Guid ServiceId,
        Guid StaffMemberId,
        Guid AppointmentId);

    private sealed record PublicBusinessDetailResponse(
        IReadOnlyList<PublicBusinessStaffMemberResponse> StaffMembers,
        IReadOnlyList<PublicBusinessReviewResponse> Reviews);

    private sealed record PublicBusinessStaffMemberResponse(
        Guid Id,
        string? ProfilePhotoUrl);

    private sealed record PublicBusinessReviewResponse(string? CustomerProfilePhotoUrl);

    private sealed record BookingAvailabilityResponse(IReadOnlyList<AvailabilitySlotResponse> Slots);

    private sealed record AvailabilitySlotResponse(IReadOnlyList<AvailableStaffResponse> StaffMembers);

    private sealed record AvailableStaffResponse(
        Guid StaffMemberId,
        string? ProfilePhotoUrl);
}
