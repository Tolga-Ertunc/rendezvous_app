using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Rendezvous.Domain.Availability;
using Rendezvous.Domain.Services;
using Rendezvous.Domain.Staff;
using Rendezvous.Domain.Businesses;
using Rendezvous.Infrastructure.Persistence;

namespace Rendezvous.Tests.Api;

public class AuthAndBusinessFlowTests : IClassFixture<RendezvousApiFactory>
{
    private readonly RendezvousApiFactory factory;
    private readonly HttpClient client;

    public AuthAndBusinessFlowTests(RendezvousApiFactory factory)
    {
        this.factory = factory;
        client = factory.CreateClient();
    }

    [Fact]
    public async Task Guest_can_view_booking_availability_for_approved_business()
    {
        var (_, user) = await RegisterAndGetCurrentUserAsync("availability-owner@example.com");
        client.DefaultRequestHeaders.Authorization = null;
        var setup = await CreateBookableBusinessAsync(user.Id, "Availability Barber");

        var response = await client.GetAsync(
            $"/api/booking/businesses/{setup.BusinessId}/services/{setup.ServiceId}/availability?date={setup.LocalDate:yyyy-MM-dd}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var availability = await response.Content.ReadFromJsonAsync<BookingAvailabilityResponse>();

        availability!.Slots.Should().NotBeEmpty();
        availability.Slots.Should().Contain(slot =>
            slot.StaffMembers.Any(staffMember => staffMember.StaffMemberId == setup.StaffMemberId));
    }

    [Fact]
    public async Task Guest_cannot_create_appointment_request()
    {
        var (_, user) = await RegisterAndGetCurrentUserAsync("guest-post-owner@example.com");
        var setup = await CreateBookableBusinessAsync(user.Id, "Guest Post Barber");
        client.DefaultRequestHeaders.Authorization = null;

        var response = await client.PostAsJsonAsync(
            "/api/booking/appointment-requests",
            new
            {
                businessId = setup.BusinessId,
                serviceId = setup.ServiceId,
                staffMemberId = setup.StaffMemberId,
                startsAtUtc = setup.StartsAtUtc
            });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Registered_customer_can_create_pending_appointment_request()
    {
        var (_, owner) = await RegisterAndGetCurrentUserAsync("booking-owner@example.com");
        var setup = await CreateBookableBusinessAsync(owner.Id, "Booking Barber");
        var token = await RegisterAsync("booking-customer@example.com");
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var response = await client.PostAsJsonAsync(
            "/api/booking/appointment-requests",
            new
            {
                businessId = setup.BusinessId,
                serviceId = setup.ServiceId,
                staffMemberId = setup.StaffMemberId,
                startsAtUtc = setup.StartsAtUtc
            });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var appointmentRequest = await response.Content.ReadFromJsonAsync<AppointmentRequestResponse>();

        appointmentRequest!.Status.Should().Be("Pending");
    }

    [Fact]
    public async Task Email_availability_reports_duplicate_and_available_emails()
    {
        await RegisterAsync("duplicate-check@example.com");
        client.DefaultRequestHeaders.Authorization = null;

        var duplicate = await client.GetFromJsonAsync<EmailAvailabilityResponse>(
            "/api/auth/email-availability?email=duplicate-check@example.com");
        var available = await client.GetFromJsonAsync<EmailAvailabilityResponse>(
            "/api/auth/email-availability?email=available-check@example.com");

        duplicate!.IsAvailable.Should().BeFalse();
        available!.IsAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task Register_rejects_password_confirmation_mismatch()
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new
            {
                email = "mismatch@example.com",
                password = "StrongPass123!",
                confirmPassword = "DifferentPass123!"
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_rejects_password_shorter_than_eight_characters()
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new
            {
                email = "short-password@example.com",
                password = "Short1!",
                confirmPassword = "Short1!"
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_accepts_configured_password_without_lowercase()
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new
            {
                email = "uppercase-password@example.com",
                password = "PASSWORD1!",
                confirmPassword = "PASSWORD1!"
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Register_rejects_duplicate_email()
    {
        await RegisterAsync("duplicate-register@example.com");

        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new
            {
                email = "duplicate-register@example.com",
                password = "StrongPass123!",
                confirmPassword = "StrongPass123!"
            });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Registered_customer_cannot_create_business()
    {
        var token = await RegisterAsync("customer-flow@example.com");
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var createResponse = await client.PostAsJsonAsync(
            "/api/owner/businesses",
            new
            {
                name = "Flow Barber",
                type = 1,
                ownerStaffDisplayName = "Flow Owner"
            });

        createResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Active_owner_can_create_pending_business()
    {
        var (token, user) = await RegisterAndGetCurrentUserAsync("owner-flow@example.com");
        await GrantOwnerMembershipAsync(user.Id, "Existing Owner Business");
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var createResponse = await client.PostAsJsonAsync(
            "/api/owner/businesses",
            new
            {
                name = "Flow Barber",
                type = 1,
                ownerStaffDisplayName = "Flow Owner"
            });

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var businesses = await client.GetFromJsonAsync<List<OwnerBusinessResponse>>(
            "/api/owner/businesses");

        businesses.Should().ContainSingle(business =>
            business.Name == "Flow Barber"
            && business.Status == "PendingApproval");
    }

    [Fact]
    public async Task Owner_can_invite_employee_and_employee_can_accept()
    {
        var (ownerToken, ownerUser) = await RegisterAndGetCurrentUserAsync("owner-invite@example.com");
        await GrantOwnerMembershipAsync(ownerUser.Id, "Existing Invite Business");
        client.DefaultRequestHeaders.Authorization = new("Bearer", ownerToken);

        var createBusinessResponse = await client.PostAsJsonAsync(
            "/api/owner/businesses",
            new
            {
                name = "Invite Barber",
                type = 1,
                ownerStaffDisplayName = "Invite Owner"
            });
        var business = await createBusinessResponse.Content
            .ReadFromJsonAsync<OwnerBusinessDetailResponse>();

        var inviteResponse = await client.PostAsJsonAsync(
            $"/api/owner/businesses/{business!.Id}/invitations",
            new
            {
                email = "employee-invite@example.com",
                staffDisplayName = "Invite Employee"
            });

        inviteResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var invitation = await inviteResponse.Content
            .ReadFromJsonAsync<OwnerBusinessInvitationResponse>();
        invitation!.AcceptanceToken.Should().NotBeNullOrWhiteSpace();

        var employeeToken = await RegisterAsync("employee-invite@example.com");
        client.DefaultRequestHeaders.Authorization = new("Bearer", employeeToken);

        var acceptResponse = await client.PostAsJsonAsync(
            "/api/business-invitations/accept",
            new { token = invitation.AcceptanceToken });

        acceptResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var me = await client.GetFromJsonAsync<CurrentUserResponse>("/api/auth/me");

        me!.BusinessMemberships.Should().ContainSingle(membership =>
            membership.BusinessId == business.Id
            && membership.Role == "Employee"
            && membership.Status == "Active");
    }

    private async Task<string> RegisterAsync(string email)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new
            {
                email,
                password = "StrongPass123!",
                confirmPassword = "StrongPass123!"
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var tokenResponse = await response.Content.ReadFromJsonAsync<AuthTokenResponse>();
        return tokenResponse!.AccessToken;
    }

    private async Task<(string Token, CurrentUserResponse User)> RegisterAndGetCurrentUserAsync(string email)
    {
        var token = await RegisterAsync(email);
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        var user = await client.GetFromJsonAsync<CurrentUserResponse>("/api/auth/me");

        return (token, user!);
    }

    private async Task GrantOwnerMembershipAsync(Guid userId, string businessName)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var business = new Business
        {
            OwnerUserId = userId,
            Name = businessName,
            Type = BusinessType.Barber,
            Status = BusinessStatus.Approved,
            TimeZoneId = "Europe/Istanbul"
        };

        dbContext.Businesses.Add(business);
        dbContext.BusinessMemberships.Add(new BusinessMembership
        {
            BusinessId = business.Id,
            UserId = userId,
            Role = BusinessMembershipRole.Owner,
            Status = BusinessMembershipStatus.Active,
            CreatedAtUtc = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync();
    }

    private async Task<BookableBusinessSetup> CreateBookableBusinessAsync(
        Guid ownerUserId,
        string businessName)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var localDate = GetNextBusinessDate();
        var startsAtUtc = ConvertLocalToUtc(localDate, new TimeOnly(9, 0));
        var business = new Business
        {
            OwnerUserId = ownerUserId,
            Name = businessName,
            Type = BusinessType.Barber,
            Status = BusinessStatus.Approved,
            TimeZoneId = "Europe/Istanbul"
        };
        var service = new BusinessService
        {
            BusinessId = business.Id,
            Name = "Haircut",
            DurationMinutes = 30,
            BasePriceAmount = 500,
            CurrencyCode = "TRY",
            IsActive = true
        };
        var staffMember = new StaffMember
        {
            BusinessId = business.Id,
            UserId = ownerUserId,
            DisplayName = "Test Barber",
            IsActive = true
        };

        dbContext.Businesses.Add(business);
        dbContext.BusinessServices.Add(service);
        dbContext.StaffMembers.Add(staffMember);
        dbContext.BusinessWorkingHours.Add(new BusinessWorkingHour
        {
            BusinessId = business.Id,
            DayOfWeek = localDate.DayOfWeek,
            OpensAt = new TimeOnly(9, 0),
            ClosesAt = new TimeOnly(17, 0)
        });
        dbContext.StaffWorkingHours.Add(new StaffWorkingHour
        {
            StaffMemberId = staffMember.Id,
            DayOfWeek = localDate.DayOfWeek,
            StartsAt = new TimeOnly(9, 0),
            EndsAt = new TimeOnly(17, 0)
        });

        await dbContext.SaveChangesAsync();

        return new BookableBusinessSetup(
            business.Id,
            service.Id,
            staffMember.Id,
            localDate,
            startsAtUtc);
    }

    private static DateOnly GetNextBusinessDate()
    {
        var date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(2);

        return date.DayOfWeek == DayOfWeek.Sunday
            ? date.AddDays(1)
            : date;
    }

    private static DateTimeOffset ConvertLocalToUtc(DateOnly date, TimeOnly time)
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");
        var localDateTime = DateTime.SpecifyKind(date.ToDateTime(time), DateTimeKind.Unspecified);
        var utcDateTime = TimeZoneInfo.ConvertTimeToUtc(localDateTime, timeZone);

        return new DateTimeOffset(utcDateTime, TimeSpan.Zero);
    }

    private sealed record AuthTokenResponse(string AccessToken);

    private sealed record EmailAvailabilityResponse(bool IsAvailable);

    private sealed record BookingAvailabilityResponse(
        IReadOnlyList<AvailabilitySlotResponse> Slots);

    private sealed record AvailabilitySlotResponse(
        IReadOnlyList<AvailableStaffResponse> StaffMembers);

    private sealed record AvailableStaffResponse(Guid StaffMemberId);

    private sealed record AppointmentRequestResponse(string Status);

    private sealed record BookableBusinessSetup(
        Guid BusinessId,
        Guid ServiceId,
        Guid StaffMemberId,
        DateOnly LocalDate,
        DateTimeOffset StartsAtUtc);

    private sealed record OwnerBusinessResponse(
        Guid Id,
        string Name,
        string Type,
        string Status,
        string TimeZoneId);

    private sealed record OwnerBusinessDetailResponse(
        Guid Id,
        string Name,
        string Type,
        string Status,
        string TimeZoneId);

    private sealed record OwnerBusinessInvitationResponse(string? AcceptanceToken);

    private sealed record CurrentUserResponse(
        Guid Id,
        int PublicNumber,
        string Email,
        IReadOnlyList<string> Roles,
        IReadOnlyList<BusinessMembershipResponse> BusinessMemberships);

    private sealed record BusinessMembershipResponse(
        Guid BusinessId,
        string BusinessName,
        string Role,
        string Status);
}
