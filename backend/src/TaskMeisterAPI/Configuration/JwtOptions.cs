using System.ComponentModel.DataAnnotations;

namespace TaskMeisterAPI.Configuration;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; init; } = string.Empty;

    [Required]
    public string Audience { get; init; } = string.Empty;

    /// <summary>
    /// How long (in minutes) a token remains valid after issue.
    /// </summary>
    [Range(1, 10080)] // 10080 = one week
    public int ExpiryMinutes { get; init; } = 60;

    /// <summary>
    /// HMAC signing key. Must be at least 32 characters.
    /// Never put this in appsettings — supply via:
    ///   Development : dotnet user-secrets set "Jwt:SecretKey" "..."
    ///   Production  : environment variable  Jwt__SecretKey=...
    /// </summary>
    [Required, MinLength(32)]
    public string SecretKey { get; init; } = string.Empty;
}
