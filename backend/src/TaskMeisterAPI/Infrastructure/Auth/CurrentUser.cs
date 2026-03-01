using System.Security.Claims;
using TaskMeisterAPI.Configuration;
using TaskMeisterAPI.Data;
using TaskMeisterAPI.Models.Entities;

namespace TaskMeisterAPI.Infrastructure.Auth;

public class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AppDbContext _dbContext;
    private User? _cachedUser;

    public CurrentUser(IHttpContextAccessor httpContextAccessor, AppDbContext dbContext)
    {
        _httpContextAccessor = httpContextAccessor;
        _dbContext = dbContext;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public int Id => TryGetId(out var id) 
        ? id 
        : throw new InvalidOperationException("User is not authenticated.");

    public bool TryGetId(out int id)
    {
        id = 0;
        var userIdClaim = User?.FindFirstValue(AppClaims.UserId);
        return int.TryParse(userIdClaim, out id);
    }

    public async Task<User?> GetUserAsync()
    {
        if (_cachedUser != null) 
            return _cachedUser;

        if (!TryGetId(out var id)) 
            return null;

        _cachedUser = await _dbContext.Users.FindAsync(id);
        return _cachedUser;
    }
}