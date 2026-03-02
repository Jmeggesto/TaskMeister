using System.IdentityModel.Tokens.Jwt;
using ErrorOr;
using FluentAssertions;
using Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TaskMeisterAPI.Configuration;
using TaskMeisterAPI.Data;
using TaskMeisterAPI.Models.Requests;
using TaskMeisterAPI.Services;
using TaskMeisterAPI.Tests.Fixtures;

namespace TaskMeisterAPI.Tests.Unit.Services;

public class UserServiceTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static AppDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static UserService CreateService(AppDbContext db) =>
        new(db, Options.Create(new JwtOptions
        {
            Issuer        = TestJwt.Issuer,
            Audience      = TestJwt.Audience,
            SecretKey     = TestJwt.SecretKey,
            ExpiryMinutes = TestJwt.ExpiryMinutes,
        }), NullLogger<UserService>.Instance);

    private static SignupRequest ValidSignup(
        string name     = "alice",
        string email    = "alice@example.com",
        string password = "SecurePass123!") =>
        new(name, email, password);

    private static LoginRequest ValidLogin(
        string email    = "alice@example.com",
        string password = "SecurePass123!") =>
        new(email, password);

    // -------------------------------------------------------------------------
    // SignupAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SignupAsync_ReturnsAuthResponse_WhenRequestIsValid()
    {
        using var db  = CreateDbContext();
        var svc = CreateService(db);

        var result = await svc.SignupAsync(ValidSignup());

        result.IsError.Should().BeFalse();
        result.Value.Token.Should().NotBeNullOrEmpty();
        result.Value.Name.Should().Be("alice");
        result.Value.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public async Task SignupAsync_ReturnsConflict_WhenNameAlreadyExists()
    {
        using var db  = CreateDbContext();
        var svc = CreateService(db);
        await svc.SignupAsync(ValidSignup(name: "bob", email: "bob@example.com"));

        var result = await svc.SignupAsync(ValidSignup(name: "bob", email: "other@example.com"));

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("User.DuplicateName");
    }

    [Fact]
    public async Task SignupAsync_ReturnsConflict_WhenEmailAlreadyExists()
    {
        using var db  = CreateDbContext();
        var svc = CreateService(db);
        await svc.SignupAsync(ValidSignup(name: "alice", email: "shared@example.com"));

        var result = await svc.SignupAsync(ValidSignup(name: "carol", email: "shared@example.com"));

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("User.DuplicateEmail");
    }

    [Fact]
    public async Task SignupAsync_DoesNotStorePlaintextPassword()
    {
        using var db  = CreateDbContext();
        var svc = CreateService(db);
        const string plaintext = "SecurePass123!";

        await svc.SignupAsync(ValidSignup(password: plaintext));

        var user = db.Users.Single();
        user.Password.Should().NotBe(plaintext);
        // Stored value is base64-encoded PBKDF2 salt+hash.
        var bytes = Convert.FromBase64String(user.Password);
        bytes.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public async Task SignupAsync_CreatedUser_CanLoginWithSameCredentials()
    {
        using var db  = CreateDbContext();
        var svc = CreateService(db);
        await svc.SignupAsync(ValidSignup());

        var login = await svc.LoginAsync(ValidLogin());

        login.IsError.Should().BeFalse();
        login.Value.Name.Should().Be("alice");
    }

    [Fact]
    public async Task SignupAsync_SetsTokenVersionTo1()
    {
        using var db  = CreateDbContext();
        var svc = CreateService(db);

        await svc.SignupAsync(ValidSignup());

        db.Users.Single().TokenVersion.Should().Be(1);
    }

    [Fact]
    public async Task SignupAsync_SetsCreatedOnAndUpdatedOn_ToNearUtcNow()
    {
        using var db  = CreateDbContext();
        var svc = CreateService(db);
        var before = DateTime.UtcNow;

        await svc.SignupAsync(ValidSignup());

        var user = db.Users.Single();
        user.CreatedOn.Should().BeOnOrAfter(before).And.BeOnOrBefore(DateTime.UtcNow);
        user.UpdatedOn.Should().BeOnOrAfter(before).And.BeOnOrBefore(DateTime.UtcNow);
    }

    [Fact]
    public async Task SignupAsync_TokenContainsExpectedClaims()
    {
        using var db  = CreateDbContext();
        var svc = CreateService(db);

        var result = await svc.SignupAsync(ValidSignup());

        var handler = new JwtSecurityTokenHandler();
        var jwt     = handler.ReadJwtToken(result.Value.Token);

        jwt.Claims.Should().Contain(c => c.Type == AppClaims.UserId);      // sub
        jwt.Claims.Should().Contain(c => c.Type == "name");              // name
        jwt.Claims.Should().Contain(c => c.Type == AppClaims.TokenVersion); // tok_ver
        jwt.Id.Should().NotBeNullOrEmpty();                              // jti
    }

    [Fact]
    public async Task SignupAsync_TokenExpiresAt_EqualsNowPlusExpiryMinutes()
    {
        using var db  = CreateDbContext();
        var svc = CreateService(db);
        var before = DateTime.UtcNow;

        var result = await svc.SignupAsync(ValidSignup());

        var expected = before.AddMinutes(TestJwt.ExpiryMinutes);
        result.Value.ExpiresAt.Should().BeCloseTo(expected, TimeSpan.FromSeconds(5));
    }

    // -------------------------------------------------------------------------
    // LoginAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task LoginAsync_ReturnsAuthResponse_WithValidCredentials()
    {
        using var db  = CreateDbContext();
        var svc = CreateService(db);
        await svc.SignupAsync(ValidSignup());

        var result = await svc.LoginAsync(ValidLogin());

        result.IsError.Should().BeFalse();
        result.Value.Token.Should().NotBeNullOrEmpty();
        result.Value.Name.Should().Be("alice");
    }

    [Fact]
    public async Task LoginAsync_ReturnsUnauthorized_WhenEmailNotFound()
    {
        using var db  = CreateDbContext();
        var svc = CreateService(db);

        var result = await svc.LoginAsync(ValidLogin("ghost@example.com"));

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("User.InvalidCredentials");
    }

    [Fact]
    public async Task LoginAsync_ReturnsUnauthorized_WhenPasswordIsWrong()
    {
        using var db  = CreateDbContext();
        var svc = CreateService(db);
        await svc.SignupAsync(ValidSignup());

        var result = await svc.LoginAsync(ValidLogin(password: "WrongPassword!"));

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("User.InvalidCredentials");
    }

    [Fact]
    public async Task LoginAsync_ReturnsSameErrorCode_ForMissingUserAndWrongPassword()
    {
        // Both failure modes must return the same code to prevent user enumeration.
        using var db  = CreateDbContext();
        var svc = CreateService(db);
        await svc.SignupAsync(ValidSignup());

        var missingUser  = await svc.LoginAsync(ValidLogin("nobody@example.com"));
        var wrongPassword = await svc.LoginAsync(ValidLogin(password: "BadPassword!"));

        missingUser.FirstError.Code.Should().Be(wrongPassword.FirstError.Code);
    }

    [Fact]
    public async Task LoginAsync_TokenIncludesCurrentTokenVersion()
    {
        using var db  = CreateDbContext();
        var svc = CreateService(db);
        await svc.SignupAsync(ValidSignup());

        // Logout twice to increment TokenVersion to 3.
        var user = db.Users.Single();
        await svc.LogoutAsync(user);
        await svc.LogoutAsync(user);

        var loginResult = await svc.LoginAsync(ValidLogin());
        var jwt         = new JwtSecurityTokenHandler().ReadJwtToken(loginResult.Value.Token);
        var versionClaim = jwt.Claims.First(c => c.Type == AppClaims.TokenVersion).Value;

        versionClaim.Should().Be("3");
    }

    // -------------------------------------------------------------------------
    // LogoutAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task LogoutAsync_ReturnsSuccess_WhenUserExists()
    {
        using var db  = CreateDbContext();
        var svc = CreateService(db);
        await svc.SignupAsync(ValidSignup());
        var user = db.Users.Single();

        var result = await svc.LogoutAsync(user);

        result.IsError.Should().BeFalse();
    }

    [Fact]
    public async Task LogoutAsync_IncrementsTokenVersion()
    {
        using var db  = CreateDbContext();
        var svc = CreateService(db);
        await svc.SignupAsync(ValidSignup());
        var user = db.Users.Single();
        user.TokenVersion.Should().Be(1);

        await svc.LogoutAsync(user);

        db.Entry(user).Reload();
        user.TokenVersion.Should().Be(2);
    }

    [Fact]
    public async Task LogoutAsync_MultipleLogouts_IncrementVersionSequentially()
    {
        using var db  = CreateDbContext();
        var svc = CreateService(db);
        await svc.SignupAsync(ValidSignup());
        var user = db.Users.Single();

        await svc.LogoutAsync(user);
        await svc.LogoutAsync(user);
        await svc.LogoutAsync(user);

        db.Users.Single().TokenVersion.Should().Be(4);
    }

    [Fact]
    public async Task LogoutAsync_PreventsLoginTokenFromBeingReused()
    {
        // Verify that after logout the token version in the DB doesn't match
        // a pre-logout token's version claim any more.
        using var db  = CreateDbContext();
        var svc = CreateService(db);
        await svc.SignupAsync(ValidSignup());
        var user = db.Users.Single();

        var preLogoutLogin = await svc.LoginAsync(ValidLogin());
        var preLogoutJwt   = new JwtSecurityTokenHandler().ReadJwtToken(preLogoutLogin.Value.Token);
        var preLogoutVer   = int.Parse(preLogoutJwt.Claims.First(c => c.Type == AppClaims.TokenVersion).Value);

        await svc.LogoutAsync(user);

        var currentVersion = db.Users.Single().TokenVersion;
        currentVersion.Should().NotBe(preLogoutVer);
    }
}
