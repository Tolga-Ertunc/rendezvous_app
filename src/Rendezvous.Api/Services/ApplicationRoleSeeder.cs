using Microsoft.AspNetCore.Identity;
using Rendezvous.Infrastructure.Identity;

namespace Rendezvous.Api.Services;

public class ApplicationRoleSeeder
{
    private readonly RoleManager<IdentityRole<Guid>> roleManager;

    public ApplicationRoleSeeder(RoleManager<IdentityRole<Guid>> roleManager)
    {
        this.roleManager = roleManager;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await EnsureRoleAsync(ApplicationRoles.Admin, cancellationToken);
        await EnsureRoleAsync(ApplicationRoles.User, cancellationToken);
    }

    private async Task EnsureRoleAsync(string roleName, CancellationToken cancellationToken)
    {
        if (await roleManager.RoleExistsAsync(roleName))
        {
            return;
        }

        var result = await roleManager.CreateAsync(new IdentityRole<Guid>(roleName));
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(error => error.Description));
            throw new InvalidOperationException($"Role '{roleName}' could not be seeded. {errors}");
        }
    }
}
