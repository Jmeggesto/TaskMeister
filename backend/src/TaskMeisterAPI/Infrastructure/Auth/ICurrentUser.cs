using TaskMeisterAPI.Models.Entities;

namespace TaskMeisterAPI.Infrastructure.Auth;

public interface ICurrentUser
{
    int Id { get; }               // Throws if not authenticated; use TryGetId for safe checks
    bool IsAuthenticated { get; }
    bool TryGetId(out int id);
    Task<User?> GetUserAsync();    // Loads full user (with optional tracking) on demand
}