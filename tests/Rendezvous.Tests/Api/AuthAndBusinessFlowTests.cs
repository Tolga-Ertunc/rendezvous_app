using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rendezvous.Api.Email;
using Rendezvous.Domain.Appointments;
using Rendezvous.Domain.Availability;
using Rendezvous.Domain.Services;
using Rendezvous.Domain.Staff;
using Rendezvous.Domain.Businesses;
using Rendezvous.Infrastructure.Identity;
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
    public async Task Owner_approval_creates_customer_notification_and_sends_email()
    {
        var (ownerToken, owner) = await RegisterAndGetCurrentUserAsync("approval-email-owner@example.com");
        var setup = await CreateBookableBusinessAsync(owner.Id, "Approval Email Barber");
        await AddOwnerMembershipAsync(owner.Id, setup.BusinessId);
        var customerToken = await RegisterAsync("approval-email-customer@example.com");
        client.DefaultRequestHeaders.Authorization = new("Bearer", customerToken);
        var requestResponse = await client.PostAsJsonAsync(
            "/api/booking/appointment-requests",
            new
            {
                businessId = setup.BusinessId,
                serviceId = setup.ServiceId,
                staffMemberId = setup.StaffMemberId,
                startsAtUtc = setup.StartsAtUtc
            });
        var appointmentRequest = await requestResponse.Content.ReadFromJsonAsync<AppointmentRequestResponse>();

        client.DefaultRequestHeaders.Authorization = new("Bearer", ownerToken);
        var ownerRequests = await client.GetFromJsonAsync<IReadOnlyList<OwnerAppointmentRequestListResponse>>(
            $"/api/owner/businesses/{setup.BusinessId}/appointment-requests");

        ownerRequests.Should().ContainSingle(request =>
            request.Id == appointmentRequest!.Id
            && request.CustomerFullName == "Test User");

        var approvalResponse = await client.PostAsync(
            $"/api/owner/businesses/{setup.BusinessId}/appointment-requests/{appointmentRequest!.Id}/approve",
            content: null);

        approvalResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await dbContext.Notifications
            .AnyAsync(notification =>
                notification.Title == "Appointment request approved"
                && notification.LinkUrl == "/appointments"))
            .Should()
            .BeTrue();
        var emailSender = factory.Services.GetRequiredService<InMemoryEmailSender>();
        emailSender.SentMessages.Should().Contain(message =>
            message.To == "approval-email-customer@example.com"
            && message.Subject == "Your appointment is approved"
            && message.TextBody.Contains("Approval Email Barber", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Business_full_day_exception_closes_booking_availability()
    {
        var (ownerToken, owner) = await RegisterAndGetCurrentUserAsync("closed-day-owner@example.com");
        var setup = await CreateBookableBusinessAsync(owner.Id, "Closed Day Barber");
        await AddOwnerMembershipAsync(owner.Id, setup.BusinessId);
        client.DefaultRequestHeaders.Authorization = new("Bearer", ownerToken);

        var createResponse = await client.PostAsJsonAsync(
            $"/api/owner/businesses/{setup.BusinessId}/availability-exceptions",
            new
            {
                type = "BusinessClosed",
                date = setup.LocalDate,
                isFullDay = true
            });

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        client.DefaultRequestHeaders.Authorization = null;
        var availability = await client.GetFromJsonAsync<BookingAvailabilityResponse>(
            $"/api/booking/businesses/{setup.BusinessId}/services/{setup.ServiceId}/availability?date={setup.LocalDate:yyyy-MM-dd}");

        availability!.Slots.Should().BeEmpty();
    }

    [Fact]
    public async Task Staff_leave_removes_only_that_staff_member_from_availability()
    {
        var (ownerToken, owner) = await RegisterAndGetCurrentUserAsync("staff-leave-owner@example.com");
        var setup = await CreateBookableBusinessAsync(owner.Id, "Staff Leave Barber");
        await AddOwnerMembershipAsync(owner.Id, setup.BusinessId);
        var secondStaffId = await AddStaffMemberAsync(setup.BusinessId, owner.Id, setup.LocalDate, "Second Barber");
        client.DefaultRequestHeaders.Authorization = new("Bearer", ownerToken);

        var createResponse = await client.PostAsJsonAsync(
            $"/api/owner/businesses/{setup.BusinessId}/availability-exceptions",
            new
            {
                staffMemberId = setup.StaffMemberId,
                type = "StaffLeave",
                date = setup.LocalDate,
                isFullDay = false,
                startsAt = "09:00",
                endsAt = "09:30"
            });

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        client.DefaultRequestHeaders.Authorization = null;
        var availability = await client.GetFromJsonAsync<BookingAvailabilityResponse>(
            $"/api/booking/businesses/{setup.BusinessId}/services/{setup.ServiceId}/availability?date={setup.LocalDate:yyyy-MM-dd}");
        var firstSlot = availability!.Slots.Single(slot => slot.StartsAtLocal == "09:00");

        firstSlot.StaffMembers.Should().NotContain(staff => staff.StaffMemberId == setup.StaffMemberId);
        firstSlot.StaffMembers.Should().Contain(staff => staff.StaffMemberId == secondStaffId);
    }

    [Fact]
    public async Task Appointment_request_rejects_slot_blocked_by_exception()
    {
        var (ownerToken, owner) = await RegisterAndGetCurrentUserAsync("blocked-slot-owner@example.com");
        var setup = await CreateBookableBusinessAsync(owner.Id, "Blocked Slot Barber");
        await AddOwnerMembershipAsync(owner.Id, setup.BusinessId);
        client.DefaultRequestHeaders.Authorization = new("Bearer", ownerToken);

        await client.PostAsJsonAsync(
            $"/api/owner/businesses/{setup.BusinessId}/availability-exceptions",
            new
            {
                staffMemberId = setup.StaffMemberId,
                type = "StaffLeave",
                date = setup.LocalDate,
                isFullDay = false,
                startsAt = "09:00",
                endsAt = "09:30"
            });

        var customerToken = await RegisterAsync("blocked-slot-customer@example.com");
        client.DefaultRequestHeaders.Authorization = new("Bearer", customerToken);
        var response = await client.PostAsJsonAsync(
            "/api/booking/appointment-requests",
            new
            {
                businessId = setup.BusinessId,
                serviceId = setup.ServiceId,
                staffMemberId = setup.StaffMemberId,
                startsAtUtc = setup.StartsAtUtc
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Exception_conflict_requires_confirmation_and_cancels_active_appointments_when_confirmed()
    {
        var (ownerToken, owner) = await RegisterAndGetCurrentUserAsync("exception-conflict-owner@example.com");
        var setup = await CreateBookableBusinessAsync(owner.Id, "Conflict Barber");
        await AddOwnerMembershipAsync(owner.Id, setup.BusinessId);
        var appointmentIds = await AddAppointmentsAsync(owner.Id, setup);
        client.DefaultRequestHeaders.Authorization = new("Bearer", ownerToken);

        var request = new
        {
            type = "BusinessClosed",
            date = setup.LocalDate,
            isFullDay = false,
            startsAt = "09:00",
            endsAt = "10:00"
        };
        var conflictResponse = await client.PostAsJsonAsync(
            $"/api/owner/businesses/{setup.BusinessId}/availability-exceptions",
            request);

        conflictResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var conflict = await conflictResponse.Content.ReadFromJsonAsync<AvailabilityExceptionConflictResponse>();
        conflict!.AppointmentCount.Should().Be(2);

        var confirmedResponse = await client.PostAsJsonAsync(
            $"/api/owner/businesses/{setup.BusinessId}/availability-exceptions",
            new
            {
                type = "BusinessClosed",
                date = setup.LocalDate,
                isFullDay = false,
                startsAt = "09:00",
                endsAt = "10:00",
                cancelConflictingAppointments = true
            });

        confirmedResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var statuses = await dbContext.Appointments
            .Where(appointment => appointmentIds.Contains(appointment.Id))
            .ToDictionaryAsync(appointment => appointment.Id, appointment => appointment.Status);

        statuses[appointmentIds[0]].Should().Be(AppointmentStatus.Cancelled);
        statuses[appointmentIds[1]].Should().Be(AppointmentStatus.Cancelled);
        statuses[appointmentIds[2]].Should().Be(AppointmentStatus.Completed);
    }

    [Fact]
    public async Task Employee_can_manage_only_own_leave_records()
    {
        var (_, owner) = await RegisterAndGetCurrentUserAsync("employee-leave-owner@example.com");
        var setup = await CreateBookableBusinessAsync(owner.Id, "Employee Leave Barber");
        var (employeeToken, employee) = await RegisterAndGetCurrentUserAsync("employee-leave@example.com");
        var employeeStaffId = await AddEmployeeAsync(setup.BusinessId, employee.Id, setup.LocalDate);
        client.DefaultRequestHeaders.Authorization = new("Bearer", employeeToken);

        var createResponse = await client.PostAsJsonAsync(
            "/api/employee/availability-exceptions",
            new
            {
                businessId = setup.BusinessId,
                staffMemberId = employeeStaffId,
                type = "StaffLeave",
                date = setup.LocalDate,
                isFullDay = false,
                startsAt = "10:00",
                endsAt = "11:00",
                note = "Personal leave"
            });

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<AvailabilityExceptionResponse>();

        var list = await client.GetFromJsonAsync<List<AvailabilityExceptionResponse>>(
            "/api/employee/availability-exceptions");
        list.Should().ContainSingle(exception => exception.Id == created!.Id);

        var updateResponse = await client.PutAsJsonAsync(
            $"/api/employee/availability-exceptions/{created!.Id}",
            new
            {
                businessId = setup.BusinessId,
                staffMemberId = employeeStaffId,
                type = "StaffLeave",
                date = setup.LocalDate,
                isFullDay = false,
                startsAt = "11:00",
                endsAt = "12:00",
                note = "Updated leave"
            });

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var deleteResponse = await client.DeleteAsync(
            $"/api/employee/availability-exceptions/{created.Id}");

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
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
                firstName = "Mismatch",
                lastName = "User",
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
                firstName = "Short",
                lastName = "Password",
                email = "short-password@example.com",
                password = "Short1!",
                confirmPassword = "Short1!"
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_rejects_missing_first_name_or_last_name()
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new
            {
                firstName = "",
                lastName = "User",
                email = "missing-name@example.com",
                password = "StrongPass123!",
                confirmPassword = "StrongPass123!"
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_creates_pending_registration_without_real_user()
    {
        await StartRegistrationAsync(
            "pending-registration@example.com",
            firstName: "  Pending  ",
            lastName: "  User  ");

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        (await dbContext.PendingEmailRegistrations
            .AnyAsync(registration => registration.Email == "pending-registration@example.com"))
            .Should()
            .BeTrue();
        (await dbContext.Users
            .AnyAsync(user => user.Email == "pending-registration@example.com"))
            .Should()
            .BeFalse();
        var pendingRegistration = await dbContext.PendingEmailRegistrations
            .SingleAsync(registration => registration.Email == "pending-registration@example.com");
        pendingRegistration.FirstName.Should().Be("Pending");
        pendingRegistration.LastName.Should().Be("User");
    }

    [Fact]
    public async Task Confirm_email_rejects_wrong_code_and_increments_attempt_count()
    {
        await StartRegistrationAsync("wrong-code@example.com");

        var response = await client.PostAsJsonAsync(
            "/api/auth/confirm-email",
            new
            {
                email = "wrong-code@example.com",
                code = "000000"
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var pendingRegistration = await dbContext.PendingEmailRegistrations
            .SingleAsync(registration => registration.Email == "wrong-code@example.com");

        pendingRegistration.FailedAttemptCount.Should().Be(1);
    }

    [Fact]
    public async Task Confirm_email_rejects_expired_code()
    {
        await StartRegistrationAsync("expired-code@example.com");
        var code = GetLatestConfirmationCode("expired-code@example.com");

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var pendingRegistration = await dbContext.PendingEmailRegistrations
                .SingleAsync(registration => registration.Email == "expired-code@example.com");
            pendingRegistration.CodeExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1);
            await dbContext.SaveChangesAsync();
        }

        var response = await client.PostAsJsonAsync(
            "/api/auth/confirm-email",
            new
            {
                email = "expired-code@example.com",
                code
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Confirm_email_creates_confirmed_user_with_user_role()
    {
        await StartRegistrationAsync(
            "confirm-success@example.com",
            firstName: "Confirm",
            lastName: "Success");
        var code = GetLatestConfirmationCode("confirm-success@example.com");

        var response = await client.PostAsJsonAsync(
            "/api/auth/confirm-email",
            new
            {
                email = "confirm-success@example.com",
                code
            });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var token = await LoginAsync("confirm-success@example.com");
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        var me = await client.GetFromJsonAsync<CurrentUserResponse>("/api/auth/me");

        me!.Email.Should().Be("confirm-success@example.com");
        me.FirstName.Should().Be("Confirm");
        me.LastName.Should().Be("Success");
        me.FullName.Should().Be("Confirm Success");
        me.Roles.Should().Contain("User");
    }

    [Fact]
    public async Task Resend_confirmation_code_is_rate_limited()
    {
        await StartRegistrationAsync("resend-limit@example.com");

        var response = await client.PostAsJsonAsync(
            "/api/auth/resend-confirmation-code",
            new { email = "resend-limit@example.com" });

        response.StatusCode.Should().Be((HttpStatusCode)429);
    }

    [Fact]
    public async Task Register_accepts_configured_password_without_lowercase()
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new
            {
                firstName = "Uppercase",
                lastName = "Password",
                email = "uppercase-password@example.com",
                password = "PASSWORD1!",
                confirmPassword = "PASSWORD1!"
            });

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task Register_rejects_duplicate_email()
    {
        await RegisterAsync("duplicate-register@example.com");

        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new
            {
                firstName = "Duplicate",
                lastName = "User",
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
                type = 1
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
                type = 1
            });

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdBusiness = await createResponse.Content
            .ReadFromJsonAsync<OwnerBusinessDetailResponse>();
        createdBusiness!.StaffMembers.Should().ContainSingle(staffMember =>
            staffMember.DisplayName == "Test User"
            && staffMember.Email == "owner-flow@example.com");

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
                type = 1
            });
        var business = await createBusinessResponse.Content
            .ReadFromJsonAsync<OwnerBusinessDetailResponse>();

        var inviteResponse = await client.PostAsJsonAsync(
            $"/api/owner/businesses/{business!.Id}/invitations",
            new
            {
                email = "employee-invite@example.com"
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

    [Fact]
    public async Task Admin_users_include_names_and_can_search_by_name()
    {
        var (_, targetUser) = await RegisterAndGetCurrentUserAsync(
            "search-name-user@example.com",
            firstName: "Searchable",
            lastName: "Customer");
        var adminEmail = "admin-search-users@example.com";
        await RegisterAsync(adminEmail, firstName: "Admin", lastName: "Manager");
        await AddAdminRoleAsync(adminEmail);
        var adminToken = await LoginAsync(adminEmail);
        client.DefaultRequestHeaders.Authorization = new("Bearer", adminToken);

        var users = await client.GetFromJsonAsync<IReadOnlyList<AdminUserSummaryResponse>>(
            "/api/admin/users?search=Searchable");

        users.Should().ContainSingle(user =>
            user.Id == targetUser.Id
            && user.FirstName == "Searchable"
            && user.LastName == "Customer"
            && user.FullName == "Searchable Customer");

        var detail = await client.GetFromJsonAsync<AdminUserDetailResponse>(
            $"/api/admin/users/{targetUser.Id}");

        detail!.FirstName.Should().Be("Searchable");
        detail.LastName.Should().Be("Customer");
        detail.FullName.Should().Be("Searchable Customer");
    }

    private async Task<string> RegisterAsync(
        string email,
        string firstName = "Test",
        string lastName = "User")
    {
        await StartRegistrationAsync(email, firstName, lastName);
        var confirmationCode = GetLatestConfirmationCode(email);
        var confirmResponse = await client.PostAsJsonAsync(
            "/api/auth/confirm-email",
            new
            {
                email,
                code = confirmationCode
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
        tokenResponse!.User.FirstName.Should().Be(firstName.Trim());
        tokenResponse.User.LastName.Should().Be(lastName.Trim());
        tokenResponse.User.FullName.Should().Be($"{firstName.Trim()} {lastName.Trim()}");

        return tokenResponse.AccessToken;
    }

    private async Task<string> LoginAsync(string email)
    {
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

    private async Task StartRegistrationAsync(
        string email,
        string firstName = "Test",
        string lastName = "User")
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new
            {
                firstName,
                lastName,
                email,
                password = "StrongPass123!",
                confirmPassword = "StrongPass123!"
            });

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    private string GetLatestConfirmationCode(string email)
    {
        var emailSender = factory.Services.GetRequiredService<InMemoryEmailSender>();
        var message = emailSender.SentMessages
            .Last(message => message.To.Equals(email, StringComparison.OrdinalIgnoreCase));
        var match = Regex.Match(message.TextBody, @"\b\d{6}\b");

        match.Success.Should().BeTrue();
        return match.Value;
    }

    private async Task<(string Token, CurrentUserResponse User)> RegisterAndGetCurrentUserAsync(
        string email,
        string firstName = "Test",
        string lastName = "User")
    {
        var token = await RegisterAsync(email, firstName, lastName);
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        var user = await client.GetFromJsonAsync<CurrentUserResponse>("/api/auth/me");
        user!.FirstName.Should().Be(firstName.Trim());
        user.LastName.Should().Be(lastName.Trim());
        user.FullName.Should().Be($"{firstName.Trim()} {lastName.Trim()}");

        return (token, user);
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

    private async Task AddOwnerMembershipAsync(Guid userId, Guid businessId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        dbContext.BusinessMemberships.Add(new BusinessMembership
        {
            BusinessId = businessId,
            UserId = userId,
            Role = BusinessMembershipRole.Owner,
            Status = BusinessMembershipStatus.Active,
            CreatedAtUtc = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync();
    }

    private async Task AddAdminRoleAsync(string email)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await dbContext.Users.SingleAsync(candidate => candidate.Email == email);
        var adminRole = await dbContext.Roles.SingleAsync(role => role.Name == ApplicationRoles.Admin);

        if (await dbContext.UserRoles.AnyAsync(userRole =>
            userRole.UserId == user.Id && userRole.RoleId == adminRole.Id))
        {
            return;
        }

        dbContext.UserRoles.Add(new IdentityUserRole<Guid>
        {
            UserId = user.Id,
            RoleId = adminRole.Id
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

    private async Task<Guid> AddStaffMemberAsync(
        Guid businessId,
        Guid userId,
        DateOnly localDate,
        string displayName)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var staffMember = new StaffMember
        {
            BusinessId = businessId,
            UserId = userId,
            IsActive = true
        };

        dbContext.StaffMembers.Add(staffMember);
        dbContext.StaffWorkingHours.Add(new StaffWorkingHour
        {
            StaffMemberId = staffMember.Id,
            DayOfWeek = localDate.DayOfWeek,
            StartsAt = new TimeOnly(9, 0),
            EndsAt = new TimeOnly(17, 0)
        });

        await dbContext.SaveChangesAsync();

        return staffMember.Id;
    }

    private async Task<Guid> AddEmployeeAsync(Guid businessId, Guid employeeUserId, DateOnly localDate)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var staffMember = new StaffMember
        {
            BusinessId = businessId,
            UserId = employeeUserId,
            IsActive = true
        };

        dbContext.BusinessMemberships.Add(new BusinessMembership
        {
            BusinessId = businessId,
            UserId = employeeUserId,
            Role = BusinessMembershipRole.Employee,
            Status = BusinessMembershipStatus.Active,
            CreatedAtUtc = DateTime.UtcNow
        });
        dbContext.StaffMembers.Add(staffMember);
        dbContext.StaffWorkingHours.Add(new StaffWorkingHour
        {
            StaffMemberId = staffMember.Id,
            DayOfWeek = localDate.DayOfWeek,
            StartsAt = new TimeOnly(9, 0),
            EndsAt = new TimeOnly(17, 0)
        });

        await dbContext.SaveChangesAsync();

        return staffMember.Id;
    }

    private async Task<IReadOnlyList<Guid>> AddAppointmentsAsync(
        Guid customerUserId,
        BookableBusinessSetup setup)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var appointments = new[]
        {
            new Appointment
            {
                BusinessId = setup.BusinessId,
                BusinessServiceId = setup.ServiceId,
                StaffMemberId = setup.StaffMemberId,
                CustomerUserId = customerUserId,
                StartsAtUtc = setup.StartsAtUtc,
                EndsAtUtc = setup.StartsAtUtc.AddMinutes(30),
                Status = AppointmentStatus.Pending,
                PriceAmount = 500,
                CurrencyCode = "TRY"
            },
            new Appointment
            {
                BusinessId = setup.BusinessId,
                BusinessServiceId = setup.ServiceId,
                StaffMemberId = setup.StaffMemberId,
                CustomerUserId = customerUserId,
                StartsAtUtc = setup.StartsAtUtc.AddMinutes(30),
                EndsAtUtc = setup.StartsAtUtc.AddMinutes(60),
                Status = AppointmentStatus.Approved,
                PriceAmount = 500,
                CurrencyCode = "TRY"
            },
            new Appointment
            {
                BusinessId = setup.BusinessId,
                BusinessServiceId = setup.ServiceId,
                StaffMemberId = setup.StaffMemberId,
                CustomerUserId = customerUserId,
                StartsAtUtc = setup.StartsAtUtc.AddMinutes(60),
                EndsAtUtc = setup.StartsAtUtc.AddMinutes(90),
                Status = AppointmentStatus.Completed,
                PriceAmount = 500,
                CurrencyCode = "TRY"
            }
        };

        dbContext.Appointments.AddRange(appointments);
        await dbContext.SaveChangesAsync();

        return appointments.Select(appointment => appointment.Id).ToList();
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

    private sealed record AuthTokenResponse(
        string AccessToken,
        AuthenticatedUserResponse User);

    private sealed record AuthenticatedUserResponse(
        Guid Id,
        int PublicNumber,
        string Email,
        string FirstName,
        string LastName,
        string FullName,
        IReadOnlyList<string> Roles);

    private sealed record EmailAvailabilityResponse(bool IsAvailable);

    private sealed record BookingAvailabilityResponse(
        IReadOnlyList<AvailabilitySlotResponse> Slots);

    private sealed record AvailabilitySlotResponse(
        DateTimeOffset StartsAtUtc,
        DateTimeOffset EndsAtUtc,
        string StartsAtLocal,
        string EndsAtLocal,
        IReadOnlyList<AvailableStaffResponse> StaffMembers);

    private sealed record AvailableStaffResponse(Guid StaffMemberId);

    private sealed record AppointmentRequestResponse(Guid Id, string Status);

    private sealed record OwnerAppointmentRequestListResponse(
        Guid Id,
        string CustomerFullName);

    private sealed record AvailabilityExceptionResponse(
        Guid Id,
        Guid BusinessId,
        Guid? StaffMemberId,
        string? StaffDisplayName,
        string Type,
        DateOnly Date,
        bool IsFullDay,
        string? StartsAt,
        string? EndsAt,
        string? Note,
        DateTime CreatedAtUtc);

    private sealed record AvailabilityExceptionConflictResponse(
        int AppointmentCount);

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
        string TimeZoneId,
        IReadOnlyList<OwnerBusinessStaffMemberResponse> StaffMembers);

    private sealed record OwnerBusinessStaffMemberResponse(
        Guid Id,
        string DisplayName,
        string Email,
        bool IsActive);

    private sealed record OwnerBusinessInvitationResponse(string? AcceptanceToken);

    private sealed record CurrentUserResponse(
        Guid Id,
        int PublicNumber,
        string Email,
        string FirstName,
        string LastName,
        string FullName,
        IReadOnlyList<string> Roles,
        IReadOnlyList<BusinessMembershipResponse> BusinessMemberships);

    private sealed record AdminUserSummaryResponse(
        Guid Id,
        int PublicNumber,
        string Email,
        string FirstName,
        string LastName,
        string FullName,
        bool IsSuspended,
        IReadOnlyList<string> Roles);

    private sealed record AdminUserDetailResponse(
        Guid Id,
        int PublicNumber,
        string Email,
        string FirstName,
        string LastName,
        string FullName,
        bool IsSuspended,
        IReadOnlyList<string> Roles,
        IReadOnlyList<BusinessMembershipResponse> BusinessMemberships);

    private sealed record BusinessMembershipResponse(
        Guid BusinessId,
        string BusinessName,
        string Role,
        string Status);
}
