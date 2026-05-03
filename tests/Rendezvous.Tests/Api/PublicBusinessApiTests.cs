using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Rendezvous.Domain.Businesses;
using Rendezvous.Domain.Services;
using Rendezvous.Infrastructure.Persistence;

namespace Rendezvous.Tests.Api;

public class PublicBusinessApiTests : IClassFixture<RendezvousApiFactory>
{
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
        businesses!.Single().Services.Should().ContainSingle(service =>
            service.Name == "Haircut"
            && service.DurationMinutes == 30
            && service.CurrencyCode == "TRY");
        responseBody.Should().NotContain("basePriceAmount");
    }

    private async Task SeedBusinessesAsync()
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ownerUserId = Guid.NewGuid();
        var approvedBusiness = new Business
        {
            OwnerUserId = ownerUserId,
            Name = "Public Approved Barber",
            Type = BusinessType.Barber,
            Status = BusinessStatus.Approved,
            TimeZoneId = "Europe/Istanbul"
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
                DurationMinutes = 30,
                BasePriceAmount = 250,
                CurrencyCode = "TRY",
                IsActive = true
            },
            new BusinessService
            {
                BusinessId = approvedBusiness.Id,
                Name = "Inactive Color",
                DurationMinutes = 90,
                BasePriceAmount = 900,
                CurrencyCode = "TRY",
                IsActive = false
            },
            new BusinessService
            {
                BusinessId = pendingBusiness.Id,
                Name = "Pending Service",
                DurationMinutes = 30,
                BasePriceAmount = 100,
                CurrencyCode = "TRY",
                IsActive = true
            });

        await dbContext.SaveChangesAsync();
    }

    private sealed record PublicBusinessResponse(
        Guid Id,
        string Name,
        string Type,
        string TimeZoneId,
        IReadOnlyList<PublicBusinessServiceResponse> Services);

    private sealed record PublicBusinessServiceResponse(
        Guid Id,
        string Name,
        int DurationMinutes,
        string CurrencyCode);
}
