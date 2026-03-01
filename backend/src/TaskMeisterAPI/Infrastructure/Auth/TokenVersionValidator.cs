using Microsoft.EntityFrameworkCore;
using TaskMeisterAPI.Data;

namespace TaskMeisterAPI.Infrastructure.Auth;

public class TokenVersionValidator : ITokenValidator
{
    private readonly AppDbContext _dbContext;
    public TokenVersionValidator(AppDbContext dbContext) => _dbContext = dbContext;

    public async Task<bool> IsTokenValidAsync(int userId, int tokenVersion)
    {
        var user = await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new {u.TokenVersion})
            .FirstOrDefaultAsync();

        return user != null && user.TokenVersion == tokenVersion;
    }
}
