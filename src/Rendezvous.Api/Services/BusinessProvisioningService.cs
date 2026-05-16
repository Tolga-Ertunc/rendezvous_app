using Microsoft.EntityFrameworkCore;
using Rendezvous.Domain.Availability;
using Rendezvous.Domain.Businesses;
using Rendezvous.Domain.Services;
using Rendezvous.Domain.Staff;
using Rendezvous.Infrastructure.Identity;
using Rendezvous.Infrastructure.Persistence;

namespace Rendezvous.Api.Services;

public class BusinessProvisioningService
{
    private readonly AppDbContext dbContext;

    public BusinessProvisioningService(AppDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task<Business> CreateOwnedBusinessAsync(
        Guid ownerUserId,
        string businessName,
        BusinessType businessType,
        string ownerStaffDisplayName,
        BusinessStatus status,
        CancellationToken cancellationToken)
    {
        var business = new Business
        {
            OwnerUserId = ownerUserId,
            Name = businessName.Trim(),
            Type = businessType,
            Status = status,
            TimeZoneId = "Europe/Istanbul"
        };

        var membership = new BusinessMembership
        {
            BusinessId = business.Id,
            UserId = ownerUserId,
            Role = BusinessMembershipRole.Owner,
            Status = BusinessMembershipStatus.Active,
            CreatedAtUtc = DateTime.UtcNow
        };

        var ownerName = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == ownerUserId)
            .Select(user => new
            {
                user.FirstName,
                user.LastName
            })
            .SingleOrDefaultAsync(cancellationToken);
        var defaultStaffDisplayName = ownerName is null
            ? business.Name
            : UserNames.FormatFullName(ownerName.FirstName, ownerName.LastName);

        var staffMember = new StaffMember
        {
            BusinessId = business.Id,
            UserId = ownerUserId,
            DisplayName = string.IsNullOrWhiteSpace(ownerStaffDisplayName)
                ? string.IsNullOrWhiteSpace(defaultStaffDisplayName)
                    ? business.Name
                    : defaultStaffDisplayName
                : ownerStaffDisplayName.Trim(),
            IsActive = true
        };

        dbContext.Businesses.Add(business);
        dbContext.BusinessMemberships.Add(membership);
        dbContext.StaffMembers.Add(staffMember);
        dbContext.BusinessServiceCategories.Add(new BusinessServiceCategory
        {
            BusinessId = business.Id,
            Name = "Featured",
            SortOrder = 0,
            IsSystem = true
        });
        AddDefaultBusinessWorkingHours(business.Id);
        AddDefaultStaffWorkingHours(staffMember.Id);

        return business;
    }

    private void AddDefaultBusinessWorkingHours(Guid businessId)
    {
        foreach (var day in Enumerable.Range(1, 6))
        {
            dbContext.BusinessWorkingHours.Add(new BusinessWorkingHour
            {
                BusinessId = businessId,
                DayOfWeek = (DayOfWeek)day,
                OpensAt = new TimeOnly(9, 0),
                ClosesAt = new TimeOnly(18, 0)
            });
        }
    }

    private void AddDefaultStaffWorkingHours(Guid staffMemberId)
    {
        foreach (var day in Enumerable.Range(1, 6))
        {
            dbContext.StaffWorkingHours.Add(new StaffWorkingHour
            {
                StaffMemberId = staffMemberId,
                DayOfWeek = (DayOfWeek)day,
                StartsAt = new TimeOnly(9, 0),
                EndsAt = new TimeOnly(18, 0)
            });
        }
    }
}
