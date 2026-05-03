using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Rendezvous.Domain.Availability;
using Rendezvous.Domain.Businesses;
using Rendezvous.Domain.Services;
using Rendezvous.Domain.Staff;
using Rendezvous.Infrastructure.Identity;

namespace Rendezvous.Infrastructure.Persistence;

public static class DevelopmentDataSeeder
{
    private static readonly Guid AdminUserId = Guid.Parse("0824eb72-e174-43bc-94e5-e8ad5b29db1e");
    private static readonly Guid CustomerUserId = Guid.Parse("7d921458-cfcd-4828-9077-df84c893e9f7");
    private static readonly Guid OwnerUserId = Guid.Parse("f1f52eb3-0e71-4439-85d8-05b5dc35322b");
    private static readonly Guid EmployeeUserId = Guid.Parse("172a3f8e-3a6b-43e2-99c2-1bb2f9b3e2b2");
    private static readonly Guid BusinessId = Guid.Parse("21cb18cf-fecb-4f03-943e-87c1ed8cc876");
    private static readonly Guid BusinessOwnerMembershipId = Guid.Parse("3d0879cb-9ddb-45dd-94f1-e5a3c43edda2");
    private static readonly Guid BusinessEmployeeMembershipId = Guid.Parse("c5afbd6c-8ed2-43f6-b92a-17d27a5c3545");
    private static readonly Guid OwnerStaffMemberId = Guid.Parse("c7f422d0-345f-4196-9d4f-acddfcce8307");
    private static readonly Guid EmployeeStaffMemberId = Guid.Parse("db4ea86f-d902-4df8-ac38-ff7cb8e5fdc4");
    private static readonly Guid AdminRoleId = Guid.Parse("461a8ed4-92ce-4507-a85e-29bc5d690e47");
    private static readonly Guid UserRoleId = Guid.Parse("6846f7d9-b22a-4a2b-a45a-7147a7b98558");
    private static readonly (Guid Id, DayOfWeek DayOfWeek)[] BusinessWorkingHourSeed =
    [
        (Guid.Parse("5c8ed290-c699-4da0-8c5e-008d72eb201a"), DayOfWeek.Monday),
        (Guid.Parse("7f9cb960-2f80-4ff8-baa2-0d1f4eba1fc0"), DayOfWeek.Tuesday),
        (Guid.Parse("80ef7e17-569f-4a45-a3d6-83291f654f82"), DayOfWeek.Wednesday),
        (Guid.Parse("b1866d8e-a21c-462a-9556-c7e25083bda9"), DayOfWeek.Thursday),
        (Guid.Parse("d33bc527-6e48-4afe-a527-0fb201d98fef"), DayOfWeek.Friday),
        (Guid.Parse("dbbffdea-7a45-4058-92eb-884d93f9609d"), DayOfWeek.Saturday)
    ];
    private static readonly (Guid Id, DayOfWeek DayOfWeek)[] StaffWorkingHourSeed =
    [
        (Guid.Parse("d6f3fdad-730d-492f-b9ad-a210fd7f8789"), DayOfWeek.Monday),
        (Guid.Parse("3091d21f-5103-41b3-a2f6-e0e951031c1c"), DayOfWeek.Tuesday),
        (Guid.Parse("c13a4ec1-3344-474f-88cf-a4d78c7283f0"), DayOfWeek.Wednesday),
        (Guid.Parse("2f5dc7f3-4041-478c-8235-15609e9c6f77"), DayOfWeek.Thursday),
        (Guid.Parse("a7999b8e-2c3d-41f5-85cf-85774788c8af"), DayOfWeek.Friday),
        (Guid.Parse("5a410dd3-e3be-473c-997e-c0268282f69f"), DayOfWeek.Saturday)
    ];
    private static readonly (Guid Id, DayOfWeek DayOfWeek)[] EmployeeStaffWorkingHourSeed =
    [
        (Guid.Parse("7a0d5544-2eed-4efe-82ee-397fb8932a4d"), DayOfWeek.Monday),
        (Guid.Parse("2c021c6e-0f7f-4df6-8f2d-178bbf8d4da7"), DayOfWeek.Tuesday),
        (Guid.Parse("b28bd52a-2b82-4e78-8f0c-91882f11a8e4"), DayOfWeek.Wednesday),
        (Guid.Parse("90a99a57-93e9-4868-b9c6-1d945102c24b"), DayOfWeek.Thursday),
        (Guid.Parse("66dd48dc-7ec4-4afe-afaf-7a2bfc830032"), DayOfWeek.Friday),
        (Guid.Parse("5084888c-2b1f-4c58-8066-1418d3642a78"), DayOfWeek.Saturday)
    ];

    public static async Task SeedAsync(
        AppDbContext dbContext,
        IConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        await SeedRoleAsync(dbContext, AdminRoleId, ApplicationRoles.Admin, cancellationToken);
        await SeedRoleAsync(dbContext, UserRoleId, ApplicationRoles.User, cancellationToken);
        await SeedUserAsync(
            dbContext,
            configuration,
            "SeedUsers:Admin",
            AdminUserId,
            10000001,
            AdminRoleId,
            cancellationToken);
        await SeedUserAsync(
            dbContext,
            configuration,
            "SeedUsers:Customer",
            CustomerUserId,
            10000002,
            UserRoleId,
            cancellationToken);
        var ownerUserId = await SeedUserAsync(
            dbContext,
            configuration,
            "SeedUsers:Owner",
            OwnerUserId,
            10000003,
            UserRoleId,
            cancellationToken);
        var employeeUserId = await SeedUserAsync(
            dbContext,
            configuration,
            "SeedUsers:Employee",
            EmployeeUserId,
            10000004,
            UserRoleId,
            cancellationToken,
            fallbackPasswordSectionName: "SeedUsers:Owner");

        if (ownerUserId is null)
        {
            return;
        }

        var business = await dbContext.Businesses
            .SingleOrDefaultAsync(candidate => candidate.Id == BusinessId, cancellationToken);

        if (business is not null)
        {
            if (business.OwnerUserId != ownerUserId.Value)
            {
                business.OwnerUserId = ownerUserId.Value;
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            await SeedBusinessOwnerMembershipAsync(dbContext, ownerUserId.Value, cancellationToken);
            await SeedOwnerStaffMemberAsync(dbContext, ownerUserId.Value, cancellationToken);
            await SeedBusinessEmployeeAsync(dbContext, employeeUserId, cancellationToken);
            await SeedBusinessWorkingHoursAsync(dbContext, cancellationToken);
            await SeedStaffWorkingHoursAsync(dbContext, cancellationToken);
            return;
        }

        dbContext.Businesses.Add(new Business
        {
            Id = BusinessId,
            OwnerUserId = ownerUserId.Value,
            Name = "Rendezvous Demo Barber",
            Type = BusinessType.Barber,
            Status = BusinessStatus.Approved,
            TimeZoneId = "Europe/Istanbul"
        });

        dbContext.BusinessServices.AddRange(
            new BusinessService
            {
                Id = Guid.Parse("a69eb469-64db-4eb3-9879-cacef2b8ccff"),
                BusinessId = BusinessId,
                Name = "Haircut",
                DurationMinutes = 30,
                BasePriceAmount = 500,
                CurrencyCode = "TRY",
                IsActive = true
            },
            new BusinessService
            {
                Id = Guid.Parse("11384d77-62d8-44d3-9e2e-3c45d72e2786"),
                BusinessId = BusinessId,
                Name = "Beard Trim",
                DurationMinutes = 20,
                BasePriceAmount = 300,
                CurrencyCode = "TRY",
                IsActive = true
            },
            new BusinessService
            {
                Id = Guid.Parse("572dfda3-13a2-4078-bba7-5f6407b78187"),
                BusinessId = BusinessId,
                Name = "Haircut and Beard",
                DurationMinutes = 45,
                BasePriceAmount = 750,
                CurrencyCode = "TRY",
                IsActive = true
            });

        await dbContext.SaveChangesAsync(cancellationToken);
        await SeedBusinessOwnerMembershipAsync(dbContext, ownerUserId.Value, cancellationToken);
        await SeedOwnerStaffMemberAsync(dbContext, ownerUserId.Value, cancellationToken);
        await SeedBusinessEmployeeAsync(dbContext, employeeUserId, cancellationToken);
        await SeedBusinessWorkingHoursAsync(dbContext, cancellationToken);
        await SeedStaffWorkingHoursAsync(dbContext, cancellationToken);
    }

    private static async Task SeedBusinessEmployeeAsync(
        AppDbContext dbContext,
        Guid? employeeUserId,
        CancellationToken cancellationToken)
    {
        if (employeeUserId is null)
        {
            return;
        }

        await SeedBusinessEmployeeMembershipAsync(dbContext, employeeUserId.Value, cancellationToken);
        await SeedEmployeeStaffMemberAsync(dbContext, employeeUserId.Value, cancellationToken);
    }

    private static async Task SeedBusinessOwnerMembershipAsync(
        AppDbContext dbContext,
        Guid ownerUserId,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.Businesses.AnyAsync(business => business.Id == BusinessId, cancellationToken)
            || !await dbContext.Users.AnyAsync(user => user.Id == ownerUserId, cancellationToken))
        {
            return;
        }

        var membership = await dbContext.BusinessMemberships
            .SingleOrDefaultAsync(
                candidate => candidate.BusinessId == BusinessId && candidate.UserId == ownerUserId,
                cancellationToken);

        if (membership is null)
        {
            dbContext.BusinessMemberships.Add(new BusinessMembership
            {
                Id = BusinessOwnerMembershipId,
                BusinessId = BusinessId,
                UserId = ownerUserId,
                Role = BusinessMembershipRole.Owner,
                Status = BusinessMembershipStatus.Active,
                CreatedAtUtc = DateTime.UtcNow
            });

            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        if (membership.Role != BusinessMembershipRole.Owner
            || membership.Status != BusinessMembershipStatus.Active)
        {
            membership.Role = BusinessMembershipRole.Owner;
            membership.Status = BusinessMembershipStatus.Active;

            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task SeedOwnerStaffMemberAsync(
        AppDbContext dbContext,
        Guid ownerUserId,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.Businesses.AnyAsync(business => business.Id == BusinessId, cancellationToken)
            || !await dbContext.Users.AnyAsync(user => user.Id == ownerUserId, cancellationToken))
        {
            return;
        }

        var staffMember = await dbContext.StaffMembers
            .SingleOrDefaultAsync(
                candidate => candidate.BusinessId == BusinessId && candidate.UserId == ownerUserId,
                cancellationToken);

        if (staffMember is null)
        {
            dbContext.StaffMembers.Add(new StaffMember
            {
                Id = OwnerStaffMemberId,
                BusinessId = BusinessId,
                UserId = ownerUserId,
                DisplayName = "Demo Barber",
                IsActive = true
            });

            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        if (staffMember.DisplayName != "Demo Barber" || !staffMember.IsActive)
        {
            staffMember.DisplayName = "Demo Barber";
            staffMember.IsActive = true;

            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task SeedBusinessEmployeeMembershipAsync(
        AppDbContext dbContext,
        Guid employeeUserId,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.Businesses.AnyAsync(business => business.Id == BusinessId, cancellationToken)
            || !await dbContext.Users.AnyAsync(user => user.Id == employeeUserId, cancellationToken))
        {
            return;
        }

        var membership = await dbContext.BusinessMemberships
            .SingleOrDefaultAsync(
                candidate => candidate.BusinessId == BusinessId && candidate.UserId == employeeUserId,
                cancellationToken);

        if (membership is null)
        {
            dbContext.BusinessMemberships.Add(new BusinessMembership
            {
                Id = BusinessEmployeeMembershipId,
                BusinessId = BusinessId,
                UserId = employeeUserId,
                Role = BusinessMembershipRole.Employee,
                Status = BusinessMembershipStatus.Active,
                CreatedAtUtc = DateTime.UtcNow
            });

            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        if (membership.Role != BusinessMembershipRole.Employee
            || membership.Status != BusinessMembershipStatus.Active)
        {
            membership.Role = BusinessMembershipRole.Employee;
            membership.Status = BusinessMembershipStatus.Active;

            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task SeedEmployeeStaffMemberAsync(
        AppDbContext dbContext,
        Guid employeeUserId,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.Businesses.AnyAsync(business => business.Id == BusinessId, cancellationToken)
            || !await dbContext.Users.AnyAsync(user => user.Id == employeeUserId, cancellationToken))
        {
            return;
        }

        var staffMember = await dbContext.StaffMembers
            .SingleOrDefaultAsync(
                candidate => candidate.BusinessId == BusinessId && candidate.UserId == employeeUserId,
                cancellationToken);

        if (staffMember is null)
        {
            dbContext.StaffMembers.Add(new StaffMember
            {
                Id = EmployeeStaffMemberId,
                BusinessId = BusinessId,
                UserId = employeeUserId,
                DisplayName = "Demo Employee",
                IsActive = true
            });

            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        if (staffMember.DisplayName != "Demo Employee" || !staffMember.IsActive)
        {
            staffMember.DisplayName = "Demo Employee";
            staffMember.IsActive = true;

            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task SeedBusinessWorkingHoursAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.Businesses.AnyAsync(business => business.Id == BusinessId, cancellationToken))
        {
            return;
        }

        var opensAt = new TimeOnly(9, 0);
        var closesAt = new TimeOnly(18, 0);

        foreach (var seed in BusinessWorkingHourSeed)
        {
            var workingHour = await dbContext.BusinessWorkingHours
                .SingleOrDefaultAsync(
                    candidate => candidate.BusinessId == BusinessId && candidate.DayOfWeek == seed.DayOfWeek,
                    cancellationToken);

            if (workingHour is null)
            {
                dbContext.BusinessWorkingHours.Add(new BusinessWorkingHour
                {
                    Id = seed.Id,
                    BusinessId = BusinessId,
                    DayOfWeek = seed.DayOfWeek,
                    OpensAt = opensAt,
                    ClosesAt = closesAt
                });

                continue;
            }

            if (workingHour.OpensAt != opensAt || workingHour.ClosesAt != closesAt)
            {
                workingHour.OpensAt = opensAt;
                workingHour.ClosesAt = closesAt;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedStaffWorkingHoursAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.StaffMembers.AnyAsync(staffMember => staffMember.Id == OwnerStaffMemberId, cancellationToken))
        {
            return;
        }

        var startsAt = new TimeOnly(9, 0);
        var endsAt = new TimeOnly(18, 0);

        foreach (var seed in StaffWorkingHourSeed)
        {
            var workingHour = await dbContext.StaffWorkingHours
                .SingleOrDefaultAsync(
                    candidate => candidate.StaffMemberId == OwnerStaffMemberId && candidate.DayOfWeek == seed.DayOfWeek,
                    cancellationToken);

            if (workingHour is null)
            {
                dbContext.StaffWorkingHours.Add(new StaffWorkingHour
                {
                    Id = seed.Id,
                    StaffMemberId = OwnerStaffMemberId,
                    DayOfWeek = seed.DayOfWeek,
                    StartsAt = startsAt,
                    EndsAt = endsAt
                });

                continue;
            }

            if (workingHour.StartsAt != startsAt || workingHour.EndsAt != endsAt)
            {
                workingHour.StartsAt = startsAt;
                workingHour.EndsAt = endsAt;
            }
        }

        await SeedEmployeeStaffWorkingHoursAsync(dbContext, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedEmployeeStaffWorkingHoursAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.StaffMembers.AnyAsync(staffMember => staffMember.Id == EmployeeStaffMemberId, cancellationToken))
        {
            return;
        }

        var startsAt = new TimeOnly(10, 0);
        var endsAt = new TimeOnly(17, 0);

        foreach (var seed in EmployeeStaffWorkingHourSeed)
        {
            var workingHour = await dbContext.StaffWorkingHours
                .SingleOrDefaultAsync(
                    candidate => candidate.StaffMemberId == EmployeeStaffMemberId && candidate.DayOfWeek == seed.DayOfWeek,
                    cancellationToken);

            if (workingHour is null)
            {
                dbContext.StaffWorkingHours.Add(new StaffWorkingHour
                {
                    Id = seed.Id,
                    StaffMemberId = EmployeeStaffMemberId,
                    DayOfWeek = seed.DayOfWeek,
                    StartsAt = startsAt,
                    EndsAt = endsAt
                });

                continue;
            }

            if (workingHour.StartsAt != startsAt || workingHour.EndsAt != endsAt)
            {
                workingHour.StartsAt = startsAt;
                workingHour.EndsAt = endsAt;
            }
        }
    }

    private static async Task SeedRoleAsync(
        AppDbContext dbContext,
        Guid roleId,
        string roleName,
        CancellationToken cancellationToken)
    {
        if (await dbContext.Roles.AnyAsync(role => role.NormalizedName == roleName.ToUpperInvariant(), cancellationToken))
        {
            return;
        }

        dbContext.Roles.Add(new IdentityRole<Guid>
        {
            Id = roleId,
            Name = roleName,
            NormalizedName = roleName.ToUpperInvariant()
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task<Guid?> SeedUserAsync(
        AppDbContext dbContext,
        IConfiguration configuration,
        string sectionName,
        Guid userId,
        int publicNumber,
        Guid roleId,
        CancellationToken cancellationToken,
        string? fallbackPasswordSectionName = null)
    {
        var email = configuration[$"{sectionName}:Email"];
        var password = configuration[$"{sectionName}:Password"];
        if (string.IsNullOrWhiteSpace(password) && fallbackPasswordSectionName is not null)
        {
            password = configuration[$"{fallbackPasswordSectionName}:Password"];
        }

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return null;
        }

        var normalizedEmail = email.ToUpperInvariant();
        var user = await dbContext.Users
            .SingleOrDefaultAsync(candidate => candidate.NormalizedEmail == normalizedEmail, cancellationToken);

        if (user is null)
        {
            user = new ApplicationUser
            {
                Id = userId,
                PublicNumber = publicNumber,
                UserName = email,
                NormalizedUserName = normalizedEmail,
                Email = email,
                NormalizedEmail = normalizedEmail,
                EmailConfirmed = true
            };

            var passwordHasher = new PasswordHasher<ApplicationUser>();
            user.PasswordHash = passwordHasher.HashPassword(user, password);
            user.SecurityStamp = Guid.NewGuid().ToString();

            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        if (!await dbContext.UserRoles.AnyAsync(
            userRole => userRole.UserId == user.Id && userRole.RoleId == roleId,
            cancellationToken))
        {
            dbContext.UserRoles.Add(new IdentityUserRole<Guid>
            {
                UserId = user.Id,
                RoleId = roleId
            });

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return user.Id;
    }
}
