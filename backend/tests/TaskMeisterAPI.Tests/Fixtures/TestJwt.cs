namespace TaskMeisterAPI.Tests.Fixtures;

/// <summary>
/// JWT constants shared between TestWebApplicationFactory (configures the middleware)
/// and TestAuthHelper (generates tokens). Both sides must use identical values or
/// token validation will fail in integration tests.
/// </summary>
public static class TestJwt
{
    public const string Issuer        = "test-issuer";
    public const string Audience      = "test-audience";
    // Must be >= 32 chars to satisfy JwtOptions [MinLength(32)] validation.
    public const string SecretKey     = "test-secret-key-for-taskmeister!!";
    public const int    ExpiryMinutes = 60;
}
