namespace TaskMeisterAPI.Configuration;

/// <summary>
/// Custom JWT claim names used across the application.
/// Centralised here so the string is never duplicated between issuance (UserService)
/// and validation (Program / OnTokenValidated).
/// </summary>
public static class AppClaims
{
    /// <summary>
    /// Carries the user's current TokenVersion at the time the JWT was issued.
    /// If the stored version has since been incremented (e.g. via logout), the
    /// token is treated as revoked.
    /// </summary>
    public const string TokenVersion = "tok_ver";
    public const string UserId = "user_id";
}
