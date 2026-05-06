using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Rendezvous.Infrastructure.Identity;
using Rendezvous.Infrastructure.Persistence;

namespace Rendezvous.Tests.Api;

public sealed class RendezvousApiFactory : WebApplicationFactory<Program>
{
    private readonly string databaseName = $"RendezvousTests_{Guid.NewGuid():N}";

    public RendezvousApiFactory()
    {
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__DefaultConnection",
            "Host=localhost;Database=rendezvous_tests");
        Environment.SetEnvironmentVariable("Jwt__Issuer", "Rendezvous.Tests");
        Environment.SetEnvironmentVariable("Jwt__Audience", "Rendezvous.Tests");
        Environment.SetEnvironmentVariable(
            "Jwt__SigningKey",
            "rendezvous-tests-signing-key-with-enough-length");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration(configuration =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=rendezvous_tests",
                ["Jwt:Issuer"] = "Rendezvous.Tests",
                ["Jwt:Audience"] = "Rendezvous.Tests",
                ["Jwt:SigningKey"] = "rendezvous-tests-signing-key-with-enough-length"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options =>
                options
                    .UseInMemoryDatabase(databaseName)
                    .ConfigureWarnings(warnings =>
                        warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning)));

            using var scope = services.BuildServiceProvider().CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            dbContext.Database.EnsureDeleted();
            dbContext.Database.EnsureCreated();

            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            EnsureRoleAsync(roleManager, ApplicationRoles.Admin).GetAwaiter().GetResult();
            EnsureRoleAsync(roleManager, ApplicationRoles.User).GetAwaiter().GetResult();
        });
    }

    private static async Task EnsureRoleAsync(
        RoleManager<IdentityRole<Guid>> roleManager,
        string roleName)
    {
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            await roleManager.CreateAsync(new IdentityRole<Guid>(roleName));
        }
    }
}
