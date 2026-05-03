using Microsoft.EntityFrameworkCore;
using Rendezvous.Infrastructure.Persistence;

namespace Rendezvous.Api.Services;

public class PublicNumberGenerator
{
    private const int MinPublicNumber = 10000000;
    private const int MaxPublicNumber = 99999999;

    private readonly AppDbContext dbContext;

    public PublicNumberGenerator(AppDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task<int> GenerateAsync(CancellationToken cancellationToken)
    {
        var nextPublicNumber = await dbContext.Users
            .Select(user => (int?)user.PublicNumber)
            .MaxAsync(cancellationToken) ?? MinPublicNumber - 1;

        if (nextPublicNumber >= MaxPublicNumber)
        {
            throw new InvalidOperationException("No public user numbers are available.");
        }

        return nextPublicNumber + 1;
    }
}
