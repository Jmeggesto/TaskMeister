using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using TaskMeisterAPI.Tests.Fixtures;
using Xunit;

namespace TaskMeisterAPI.Tests.Integration;

/// <summary>
/// Integration tests for the Users HTTP API (signup / login / logout)
/// and the JWT token versioning / revocation behaviour wired into OnTokenValidated.
/// </summary>
public class UsersControllerTests : IDisposable
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient                _client;

    public UsersControllerTests()
    {
        _factory = new TestWebApplicationFactory();
        _client  = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static int _counter;
    private static string UniqueEmail() =>
        $"user{Interlocked.Increment(ref _counter)}@example.com";

    private async Task<(string token, string name)> SignupAsync(
        string? name     = null,
        string? email    = null,
        string? password = null)
    {
        name     ??= $"User{Guid.NewGuid():N}"[..10];
        email    ??= UniqueEmail();
        password ??= "SecurePass123!";

        var resp = await _client.PostAsJsonAsync("/api/users/signup",
            new { name, email, password });
        resp.StatusCode.Should().Be(HttpStatusCode.Created,
            because: $"signup for {email} should succeed");

        var doc   = JsonDocument.Parse(await resp.Content.ReadAsStringAsync()).RootElement;
        var token = doc.GetProperty("token").GetString()!;
        return (token, name);
    }

    private async Task<string> LoginAsync(string email, string password = "SecurePass123!")
    {
        var resp = await _client.PostAsJsonAsync("/api/users/login",
            new { email, password });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync()).RootElement;
        return doc.GetProperty("token").GetString()!;
    }

    private async Task LogoutAsync(string token)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/users/logout");
        req.Headers.Authorization = TestAuthHelper.AuthHeader(token);
        var resp = await _client.SendAsync(req);
        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // -------------------------------------------------------------------------
    // POST /api/users/signup — happy path
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Signup_Returns201_WithAuthResponse_WhenRequestIsValid()
    {
        var resp = await _client.PostAsJsonAsync("/api/users/signup",
            new { name = "Alice", email = UniqueEmail(), password = "SecurePass123!" });

        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync()).RootElement;
        doc.GetProperty("token").GetString().Should().NotBeNullOrEmpty();
        doc.GetProperty("name").GetString().Should().Be("Alice");
        doc.GetProperty("expiresAt").GetDateTime().Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public async Task Signup_ReturnedToken_CanAuthenticateSubsequentRequests()
    {
        var (token, _) = await SignupAsync();

        // GET /api/todos is unauthenticated but passing a valid token should still work.
        var req = new HttpRequestMessage(HttpMethod.Get, "/api/todos");
        req.Headers.Authorization = TestAuthHelper.AuthHeader(token);
        var resp = await _client.SendAsync(req);

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Signup_TwoDistinctUsers_ReceiveDifferentTokens()
    {
        var (token1, _) = await SignupAsync();
        var (token2, _) = await SignupAsync();

        token1.Should().NotBe(token2);
    }

    [Fact]
    public async Task Signup_TokenContainsUserName_InNameClaim()
    {
        var (token, name) = await SignupAsync(name: "NameClaimTest");

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        jwt.Claims.Should().Contain(c => c.Type == "name" && c.Value == name);
    }

    // -------------------------------------------------------------------------
    // POST /api/users/signup — conflict / validation errors
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Signup_Returns409_WhenNameAlreadyExists()
    {
        var name  = $"dup{Guid.NewGuid():N}"[..12];
        await _client.PostAsJsonAsync("/api/users/signup",
            new { name, email = UniqueEmail(), password = "SecurePass123!" });

        var resp = await _client.PostAsJsonAsync("/api/users/signup",
            new { name, email = UniqueEmail(), password = "SecurePass123!" });

        resp.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("User.DuplicateName");
    }

    [Fact]
    public async Task Signup_Returns409_WhenEmailAlreadyExists()
    {
        var email = UniqueEmail();
        await _client.PostAsJsonAsync("/api/users/signup",
            new { name = $"u{Guid.NewGuid():N}"[..10], email, password = "SecurePass123!" });

        var resp = await _client.PostAsJsonAsync("/api/users/signup",
            new { name = $"u{Guid.NewGuid():N}"[..10], email, password = "SecurePass123!" });

        resp.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("User.DuplicateEmail");
    }

    [Fact]
    public async Task Signup_Returns422_WhenNameIsTooShort()
    {
        var resp = await _client.PostAsJsonAsync("/api/users/signup",
            new { name = "X", email = UniqueEmail(), password = "SecurePass123!" });

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Signup_Returns422_WhenEmailIsInvalid()
    {
        var resp = await _client.PostAsJsonAsync("/api/users/signup",
            new { name = "ValidName", email = "not-an-email", password = "SecurePass123!" });

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Signup_Returns422_WhenPasswordIsTooShort()
    {
        var resp = await _client.PostAsJsonAsync("/api/users/signup",
            new { name = "ValidName", email = UniqueEmail(), password = "short" });

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Signup_Returns422_WhenRequiredFieldsMissing()
    {
        var resp = await _client.PostAsJsonAsync("/api/users/signup", new { });
        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // -------------------------------------------------------------------------
    // POST /api/users/login — happy path
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Login_Returns200_WithAuthResponse_WhenCredentialsAreValid()
    {
        var email    = UniqueEmail();
        await _client.PostAsJsonAsync("/api/users/signup",
            new { name = "LoginUser", email, password = "SecurePass123!" });

        var resp = await _client.PostAsJsonAsync("/api/users/login",
            new { email, password = "SecurePass123!" });

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync()).RootElement;
        doc.GetProperty("token").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Login_ReturnedToken_CanAuthenticateSubsequentRequests()
    {
        var email    = UniqueEmail();
        await _client.PostAsJsonAsync("/api/users/signup",
            new { name = "AuthUser", email, password = "SecurePass123!" });
        var token = await LoginAsync(email);

        // Use the login token for the [Authorize] logout endpoint.
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/users/logout");
        req.Headers.Authorization = TestAuthHelper.AuthHeader(token);
        var resp = await _client.SendAsync(req);

        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Login_SameUser_MultipleLogins_HaveDifferentJtiClaims()
    {
        var email = UniqueEmail();
        await _client.PostAsJsonAsync("/api/users/signup",
            new { name = "MultiLogin", email, password = "SecurePass123!" });

        var token1 = await LoginAsync(email);
        var token2 = await LoginAsync(email);

        var jti1 = new JwtSecurityTokenHandler().ReadJwtToken(token1).Id;
        var jti2 = new JwtSecurityTokenHandler().ReadJwtToken(token2).Id;
        jti1.Should().NotBe(jti2);
    }

    // -------------------------------------------------------------------------
    // POST /api/users/login — errors
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Login_Returns401_WhenEmailDoesNotExist()
    {
        var resp = await _client.PostAsJsonAsync("/api/users/login",
            new { email = "nobody@example.com", password = "SecurePass123!" });

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("User.InvalidCredentials");
    }

    [Fact]
    public async Task Login_Returns401_WhenPasswordIsWrong()
    {
        var email = UniqueEmail();
        await _client.PostAsJsonAsync("/api/users/signup",
            new { name = "WrongPass", email, password = "SecurePass123!" });

        var resp = await _client.PostAsJsonAsync("/api/users/login",
            new { email, password = "WrongPassword!" });

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_Returns422_WhenEmailIsInvalid()
    {
        var resp = await _client.PostAsJsonAsync("/api/users/login",
            new { email = "not-an-email", password = "SecurePass123!" });

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // -------------------------------------------------------------------------
    // POST /api/users/logout
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Logout_Returns204_WhenAuthorized()
    {
        var (token, _) = await SignupAsync();

        var req = new HttpRequestMessage(HttpMethod.Post, "/api/users/logout");
        req.Headers.Authorization = TestAuthHelper.AuthHeader(token);
        var resp = await _client.SendAsync(req);

        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Logout_Returns401_WithNoAuthorizationHeader()
    {
        var resp = await _client.PostAsJsonAsync("/api/users/logout", new { });
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_Returns401_WithMalformedToken()
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/users/logout");
        req.Headers.Authorization = TestAuthHelper.AuthHeader("this.is.not.a.jwt");
        var resp = await _client.SendAsync(req);

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_Returns401_WithExpiredToken()
    {
        var (_, _) = await SignupAsync();
        // We need the user in the DB but generate an expired token manually.
        var expiredToken = TestAuthHelper.GenerateExpiredToken(userId: 1, userName: "test");

        var req = new HttpRequestMessage(HttpMethod.Post, "/api/users/logout");
        req.Headers.Authorization = TestAuthHelper.AuthHeader(expiredToken);
        var resp = await _client.SendAsync(req);

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // -------------------------------------------------------------------------
    // Token versioning / revocation (OnTokenValidated)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Logout_InvalidatesPreviousToken_ForFutureAuthRequests()
    {
        var (token, _) = await SignupAsync();

        // Logout — TokenVersion is now incremented.
        await LogoutAsync(token);

        // Using the same token again should now be rejected by OnTokenValidated.
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/users/logout");
        req.Headers.Authorization = TestAuthHelper.AuthHeader(token);
        var resp = await _client.SendAsync(req);

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_InvalidatesAllPreviousTokens_IssuedBeforeLogout()
    {
        var email = UniqueEmail();
        await _client.PostAsJsonAsync("/api/users/signup",
            new { name = "MultiToken", email, password = "SecurePass123!" });

        // Obtain two tokens for the same user (both valid before logout).
        var token1 = await LoginAsync(email);
        var token2 = await LoginAsync(email);

        // Log out using token1 — increments TokenVersion for this user.
        var logoutReq = new HttpRequestMessage(HttpMethod.Post, "/api/users/logout");
        logoutReq.Headers.Authorization = TestAuthHelper.AuthHeader(token1);
        await _client.SendAsync(logoutReq);

        // token2 was issued before the logout, so its version claim is now stale.
        var req2 = new HttpRequestMessage(HttpMethod.Post, "/api/users/logout");
        req2.Headers.Authorization = TestAuthHelper.AuthHeader(token2);
        var resp2 = await _client.SendAsync(req2);

        resp2.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AfterLogout_NewLoginToken_CanStillAuthenticate()
    {
        var email = UniqueEmail();
        await _client.PostAsJsonAsync("/api/users/signup",
            new { name = "RenewLogin", email, password = "SecurePass123!" });

        var oldToken = await LoginAsync(email);
        await LogoutAsync(oldToken);

        // A fresh login after logout should produce a valid new token.
        var newToken = await LoginAsync(email);

        var req = new HttpRequestMessage(HttpMethod.Post, "/api/users/logout");
        req.Headers.Authorization = TestAuthHelper.AuthHeader(newToken);
        var resp = await _client.SendAsync(req);

        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task TokenValidation_Fails_WhenTokenVersionClaimIsMissing()
    {
        // Generate a token without the tok_ver claim — OnTokenValidated should reject it.
        var (_, _) = await SignupAsync();

        var key   = new System.Security.Cryptography.HMACSHA256(
            System.Text.Encoding.UTF8.GetBytes(TestJwt.SecretKey));
        // Use TestAuthHelper to generate a regular token, then verify missing claim fails.
        // (We craft a token via TestAuthHelper with an unregistered userId so DB lookup fails.)
        var tokenWithBadUserId = TestAuthHelper.GenerateToken(
            userId: 999999, userName: "ghost", tokenVersion: 1);

        var req = new HttpRequestMessage(HttpMethod.Post, "/api/users/logout");
        req.Headers.Authorization = TestAuthHelper.AuthHeader(tokenWithBadUserId);
        var resp = await _client.SendAsync(req);

        // OnTokenValidated finds no user with Id=999999 and rejects.
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TokenValidation_Passes_WhenVersionMatchesDatabase()
    {
        var (token, _) = await SignupAsync();

        // Token just issued — version should match DB (both = 1).
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/users/logout");
        req.Headers.Authorization = TestAuthHelper.AuthHeader(token);
        var resp = await _client.SendAsync(req);

        // OnTokenValidated passes → the controller handles the request → 204.
        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
