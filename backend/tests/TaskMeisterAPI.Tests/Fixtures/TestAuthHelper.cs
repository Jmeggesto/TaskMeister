using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using TaskMeisterAPI.Configuration;

namespace TaskMeisterAPI.Tests.Fixtures;

/// <summary>
/// Generates signed JWTs for use in integration tests.
/// Mirrors UserService.BuildAuthResponse exactly so tokens pass OnTokenValidated.
/// </summary>
public static class TestAuthHelper
{
    private static readonly JwtSecurityTokenHandler Handler = new();

    /// <summary>
    /// Generates a signed JWT using the same claims and signing key as UserService.
    /// The token is valid for ExpiryMinutes from now.
    /// </summary>
    public static string GenerateToken(int userId, string userName, int tokenVersion = 1)
    {
        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestJwt.SecretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var now   = DateTime.UtcNow;

        var token = new JwtSecurityToken(
            issuer:   TestJwt.Issuer,
            audience: TestJwt.Audience,
            claims:
            [
                new Claim(AppClaims.UserId,             userId.ToString()),
                new Claim(AppClaims.TokenVersion,       tokenVersion.ToString()),
                new Claim(JwtRegisteredClaimNames.Name, userName),
                new Claim(JwtRegisteredClaimNames.Jti,  Guid.NewGuid().ToString()),
            ],
            notBefore:          now,
            expires:            now.AddMinutes(TestJwt.ExpiryMinutes),
            signingCredentials: creds);

        return Handler.WriteToken(token);
    }

    /// <summary>Returns an Authorization header value for the given token.</summary>
    public static AuthenticationHeaderValue AuthHeader(string token) =>
        new("Bearer", token);

    /// <summary>
    /// Generates a token with an expiry in the past, which will fail lifetime validation.
    /// </summary>
    public static string GenerateExpiredToken(int userId, string userName, int tokenVersion = 1)
    {
        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestJwt.SecretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var past  = DateTime.UtcNow.AddHours(-2);

        var token = new JwtSecurityToken(
            issuer:   TestJwt.Issuer,
            audience: TestJwt.Audience,
            claims:
            [
                new Claim(AppClaims.UserId,             userId.ToString()),
                new Claim(AppClaims.TokenVersion,       tokenVersion.ToString()),
                new Claim(JwtRegisteredClaimNames.Name, userName),
                new Claim(JwtRegisteredClaimNames.Jti,  Guid.NewGuid().ToString()),
            ],
            notBefore:          past,
            expires:            past.AddMinutes(30),
            signingCredentials: creds);

        return Handler.WriteToken(token);
    }
}
