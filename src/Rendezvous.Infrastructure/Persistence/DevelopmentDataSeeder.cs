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
    private static readonly Guid ReviewReyesId = Guid.Parse("32f8a45f-5fb4-4d70-8cef-6fc1d7cb9390");
    private static readonly Guid ReviewDonnaId = Guid.Parse("86b26c13-7b22-4d82-aaf8-9785333f4d27");
    private static readonly Guid ReviewLaminId = Guid.Parse("44e45e8b-0436-4e12-975f-a6d22eab27fd");
    private static readonly Guid ReviewJonathanId = Guid.Parse("c5c5fba3-f3e5-4ff0-89bb-f14c32788d61");
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
            "Rendezvous",
            "Admin",
            AdminRoleId,
            cancellationToken);
        await SeedUserAsync(
            dbContext,
            configuration,
            "SeedUsers:Customer",
            CustomerUserId,
            10000002,
            "Demo",
            "Customer",
            UserRoleId,
            cancellationToken);
        var ownerUserId = await SeedUserAsync(
            dbContext,
            configuration,
            "SeedUsers:Owner",
            OwnerUserId,
            10000003,
            "Demo",
            "Owner",
            UserRoleId,
            cancellationToken);
        var employeeUserId = await SeedUserAsync(
            dbContext,
            configuration,
            "SeedUsers:Employee",
            EmployeeUserId,
            10000004,
            "Demo",
            "Employee",
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
            }

            ApplyDemoBusinessProfile(business);

            await dbContext.SaveChangesAsync(cancellationToken);

            await SeedBusinessOwnerMembershipAsync(dbContext, ownerUserId.Value, cancellationToken);
            await SeedOwnerStaffMemberAsync(dbContext, ownerUserId.Value, cancellationToken);
            await SeedBusinessEmployeeAsync(dbContext, employeeUserId, cancellationToken);
            await SeedBusinessServiceCategoriesAsync(dbContext, cancellationToken);
            await SeedBusinessServicesAsync(dbContext, cancellationToken);
            await SeedBusinessWorkingHoursAsync(dbContext, cancellationToken);
            await SeedStaffWorkingHoursAsync(dbContext, cancellationToken);
            await SeedBusinessReviewsAsync(dbContext, cancellationToken);
            return;
        }

        var demoBusiness = new Business
        {
            Id = BusinessId,
            OwnerUserId = ownerUserId.Value,
            Name = "Rendezvous Demo Barber",
            Type = BusinessType.Barber,
            Status = BusinessStatus.Approved,
            TimeZoneId = "Europe/Istanbul"
        };
        ApplyDemoBusinessProfile(demoBusiness);
        dbContext.Businesses.Add(demoBusiness);

        dbContext.BusinessServices.AddRange(
            new BusinessService
            {
                Id = Guid.Parse("a69eb469-64db-4eb3-9879-cacef2b8ccff"),
                BusinessId = BusinessId,
                Name = "Haircut",
                CategoryName = "Hair Cut",
                Description = "A clean, tailored haircut finished with neckline detailing.",
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
                CategoryName = "Beard Trim",
                Description = "Beard shaping, trim and line-up for a sharp finish.",
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
                CategoryName = "Featured",
                Description = "Full haircut and beard service with detailed finishing.",
                DurationMinutes = 45,
                BasePriceAmount = 750,
                CurrencyCode = "TRY",
                IsActive = true
            },
            new BusinessService
            {
                Id = Guid.Parse("7a3b6bde-448f-4d0c-ae83-140ff967e3fa"),
                BusinessId = BusinessId,
                Name = "Hair Dye",
                CategoryName = "Hair Dye",
                Description = "Color service planned around the current cut and desired finish.",
                DurationMinutes = 60,
                BasePriceAmount = 900,
                CurrencyCode = "TRY",
                IsActive = true
            });

        await dbContext.SaveChangesAsync(cancellationToken);
        await SeedBusinessOwnerMembershipAsync(dbContext, ownerUserId.Value, cancellationToken);
        await SeedOwnerStaffMemberAsync(dbContext, ownerUserId.Value, cancellationToken);
        await SeedBusinessEmployeeAsync(dbContext, employeeUserId, cancellationToken);
        await SeedBusinessServiceCategoriesAsync(dbContext, cancellationToken);
        await SeedBusinessWorkingHoursAsync(dbContext, cancellationToken);
        await SeedStaffWorkingHoursAsync(dbContext, cancellationToken);
        await SeedBusinessReviewsAsync(dbContext, cancellationToken);
    }

    private static void ApplyDemoBusinessProfile(Business business)
    {
        business.AddressLine = "Bagdat Caddesi 120";
        business.District = "Maltepe";
        business.City = "Istanbul";
        business.Country = "Turkey";
        business.Description = "Clean, appointment-led barber services with focused cuts, beard care and color work.";
        business.SupportsInstantConfirmation = true;
        business.SupportsPayByApp = true;
        business.IsPetFriendly = false;
        business.IsKidFriendly = true;
        business.IsNearPublicTransport = true;
        business.UsesOrganicProducts = true;
        business.UsesVeganProducts = false;
        business.IsEnvironmentallyFriendly = true;
    }

    private static async Task SeedBusinessServicesAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var services = await dbContext.BusinessServices
            .Where(service => service.BusinessId == BusinessId)
            .ToListAsync(cancellationToken);

        if (!services.Any(service => service.Name == "Hair Dye"))
        {
            var hairDyeService = new BusinessService
            {
                Id = Guid.Parse("7a3b6bde-448f-4d0c-ae83-140ff967e3fa"),
                BusinessId = BusinessId,
                Name = "Hair Dye",
                CategoryName = "Hair Dye",
                Description = "Color service planned around the current cut and desired finish.",
                DurationMinutes = 60,
                BasePriceAmount = 900,
                CurrencyCode = "TRY",
                IsActive = true
            };

            dbContext.BusinessServices.Add(hairDyeService);
            services.Add(hairDyeService);
        }

        foreach (var service in services)
        {
            service.CategoryName = service.Name switch
            {
                "Haircut" => "Hair Cut",
                "Beard Trim" => "Beard Trim",
                "Haircut and Beard" => "Featured",
                "Hair Dye" => "Hair Dye",
                _ => "Featured"
            };
            service.Description = service.Name switch
            {
                "Haircut" => "A clean, tailored haircut finished with neckline detailing.",
                "Beard Trim" => "Beard shaping, trim and line-up for a sharp finish.",
                "Haircut and Beard" => "Full haircut and beard service with detailed finishing.",
                "Hair Dye" => "Color service planned around the current cut and desired finish.",
                _ => service.Description
            };
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedBusinessServiceCategoriesAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var existingCategoryNames = await dbContext.BusinessServiceCategories
            .Where(category => category.BusinessId == BusinessId)
            .Select(category => category.Name)
            .ToListAsync(cancellationToken);

        var categoryNames = new[] { "Featured", "Hair Cut", "Beard Trim", "Hair Dye" };
        for (var index = 0; index < categoryNames.Length; index++)
        {
            var name = categoryNames[index];
            if (existingCategoryNames.Contains(name))
            {
                continue;
            }

            dbContext.BusinessServiceCategories.Add(new BusinessServiceCategory
            {
                BusinessId = BusinessId,
                Name = name,
                SortOrder = index,
                IsSystem = name == "Featured"
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedBusinessReviewsAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.Businesses.AnyAsync(business => business.Id == BusinessId, cancellationToken))
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var reviewSeeds = new[]
        {
            new BusinessReview
            {
                Id = ReviewReyesId,
                BusinessId = BusinessId,
                CustomerName = "Reyes B",
                CustomerInitial = "R",
                Rating = 5.0m,
                Comment = "Nice cut, sociable service. Easy to get along with and the finish was clean.",
                CreatedAtUtc = now.AddHours(-3),
                IsPublic = true
            },
            new BusinessReview
            {
                Id = ReviewDonnaId,
                BusinessId = BusinessId,
                CustomerName = "Donna P",
                CustomerInitial = "D",
                Rating = 5.0m,
                Comment = "Sharp work and a calm appointment. The team kept the timing tight.",
                CreatedAtUtc = now.AddDays(-1).AddHours(-2),
                IsPublic = true
            },
            new BusinessReview
            {
                Id = ReviewLaminId,
                BusinessId = BusinessId,
                CustomerName = "Lamin M",
                CustomerInitial = "L",
                Rating = 4.8m,
                Comment = "Great as always. The haircut and beard line-up were exactly what I asked for.",
                CreatedAtUtc = now.AddDays(-1).AddHours(-5),
                IsPublic = true
            },
            new BusinessReview
            {
                Id = ReviewJonathanId,
                BusinessId = BusinessId,
                CustomerName = "Jonathan S",
                CustomerInitial = "J",
                Rating = 4.9m,
                Comment = "This was a major change for me and the result landed well. The barber understood the style, kept checking length, and the final shape was spot on.",
                CreatedAtUtc = now.AddDays(-2),
                IsPublic = true
            }
        };

        foreach (var seed in reviewSeeds)
        {
            var review = await dbContext.BusinessReviews
                .SingleOrDefaultAsync(candidate => candidate.Id == seed.Id, cancellationToken);

            if (review is null)
            {
                dbContext.BusinessReviews.Add(seed);
                continue;
            }

            review.CustomerName = seed.CustomerName;
            review.CustomerInitial = seed.CustomerInitial;
            review.Rating = seed.Rating;
            review.Comment = seed.Comment;
            review.IsPublic = seed.IsPublic;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
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
                IsActive = true
            });

            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        if (!staffMember.IsActive)
        {
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
                IsActive = true
            });

            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        if (!staffMember.IsActive)
        {
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
        string firstName,
        string lastName,
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
                FirstName = firstName,
                LastName = lastName,
                EmailConfirmed = true
            };

            var passwordHasher = new PasswordHasher<ApplicationUser>();
            user.PasswordHash = passwordHasher.HashPassword(user, password);
            user.SecurityStamp = Guid.NewGuid().ToString();

            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        else if (string.IsNullOrWhiteSpace(user.FirstName) || string.IsNullOrWhiteSpace(user.LastName))
        {
            user.FirstName = firstName;
            user.LastName = lastName;

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
