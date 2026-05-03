using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace Rendezvous.Tests.Api;

public class AuthAndBusinessFlowTests : IClassFixture<RendezvousApiFactory>
{
    private readonly HttpClient client;

    public AuthAndBusinessFlowTests(RendezvousApiFactory factory)
    {
        client = factory.CreateClient();
    }

    [Fact]
    public async Task Registered_user_can_create_pending_business_as_owner()
    {
        var token = await RegisterAsync("owner-flow@example.com");
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
        var ownerToken = await RegisterAsync("owner-invite@example.com");
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
                password = "StrongPass123!"
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var tokenResponse = await response.Content.ReadFromJsonAsync<AuthTokenResponse>();
        return tokenResponse!.AccessToken;
    }

    private sealed record AuthTokenResponse(string AccessToken);

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
