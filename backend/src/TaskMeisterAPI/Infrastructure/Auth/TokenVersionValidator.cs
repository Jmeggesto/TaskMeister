using Microsoft.EntityFrameworkCore;
using TaskMeisterAPI.Data;

namespace TaskMeisterAPI.Infrastructure.Auth;

public class TokenVersionValidator : ITokenValidator
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<TokenVersionValidator> _logger;

    public TokenVersionValidator(AppDbContext dbContext, ILogger<TokenVersionValidator> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<bool> IsTokenValidAsync(int userId, int tokenVersion)
    {
        var user = await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.TokenVersion })
            .FirstOrDefaultAsync();

        if (user is null)
        {
            _logger.LogWarning(
                "Token validation failed: userId {UserId} not found. " +
                "Possible deleted account or forged token.", userId);
            return false;
        }

        if (user.TokenVersion != tokenVersion)
        {
            _logger.LogWarning(
                "Token validation failed: version mismatch for userId {UserId}. " +
                "Token may be replayed after logout.", userId);
            return false;
        }

        return true;
    }
}
