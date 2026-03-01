namespace TaskMeisterAPI.Infrastructure.Auth;

public interface ITokenValidator
{
    Task<bool> IsTokenValidAsync(int userId, int tokenVersion);
}