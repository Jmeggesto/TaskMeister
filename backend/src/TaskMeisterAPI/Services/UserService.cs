using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TaskMeisterAPI.Configuration;
using TaskMeisterAPI.Data;
using TaskMeisterAPI.Models.Entities;
using TaskMeisterAPI.Models.Requests;
using TaskMeisterAPI.Models.Responses;

namespace TaskMeisterAPI.Services;

public class UserService : IUserService
{
    private readonly AppDbContext _db;
    private readonly JwtOptions _jwt;
    private readonly SigningCredentials _signingCredentials;
    private readonly ILogger<UserService> _logger;

    private static readonly JwtSecurityTokenHandler _tokenHandler = new();

    // PBKDF2 parameters
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 310_000;
    private static readonly HashAlgorithmName HashAlgorithm = HashAlgorithmName.SHA256;

    public UserService(AppDbContext db, IOptions<JwtOptions> jwtOptions, ILogger<UserService> logger)
    {
        _db = db;
        _jwt = jwtOptions.Value;
        _logger = logger;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.SecretKey));
        _signingCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    }

    public async Task<ErrorOr<AuthResponse>> SignupAsync(SignupRequest request)
    {
        if (await _db.Users.AnyAsync(u => u.Name == request.Name))
        {
            _logger.LogWarning("Signup rejected: duplicate username.");
            return Error.Conflict("User.DuplicateName", "A user with that name already exists.");
        }

        if (await _db.Users.AnyAsync(u => u.Email == request.Email))
        {
            _logger.LogWarning("Signup rejected: duplicate email.");
            return Error.Conflict("User.DuplicateEmail", "An account with that email already exists.");
        }

        var now = DateTime.UtcNow;
        var user = new User
        {
            Name = request.Name,
            Email = request.Email,
            Password = HashPassword(request.Password),
            CreatedOn = now,
            UpdatedOn = now,
        };

        _db.Users.Add(user);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // A concurrent request beat us past the AnyAsync checks and inserted
            // the same name or email first. The unique DB index enforces the
            // constraint; we just need to surface it as a 409 rather than a 500.
            _logger.LogWarning("Signup race condition: concurrent duplicate detected.");
            return Error.Conflict("User.DuplicateEntry", "An account with that name or email already exists.");
        }

        _logger.LogInformation("New user registered. UserId={UserId}", user.Id);
        return BuildAuthResponse(user);
    }

    public async Task<ErrorOr<AuthResponse>> LoginAsync(LoginRequest request)
    {
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == request.Email);

        // Always run the full PBKDF2 computation regardless of whether the user exists.
        // Skipping it for unknown emails creates a measurable timing difference that
        // allows an attacker to enumerate which addresses are registered.
        if (user is null)
        {
            DeriveHash(request.Password, new byte[SaltSize]);
            _logger.LogWarning("Failed login: email not found.");
            return Error.Unauthorized("User.InvalidCredentials", "Invalid email or password.");
        }

        if (!VerifyPassword(request.Password, user.Password))
        {
            _logger.LogWarning("Failed login: incorrect password. UserId={UserId}", user.Id);
            return Error.Unauthorized("User.InvalidCredentials", "Invalid email or password.");
        }

        return BuildAuthResponse(user);
    }

    public async Task<ErrorOr<Success>> LogoutAsync(User currentUser)
    {
        var user = await _db.Users.FindAsync(currentUser.Id);

        if (user is null)
        {
            _logger.LogWarning(
                "Logout: user not found in database. UserId={UserId}", currentUser.Id);
            return Error.NotFound("User.NotFound", "User not found.");
        }

        user.TokenVersion++;
        await _db.SaveChangesAsync();

        _logger.LogInformation("User logged out. UserId={UserId}", user.Id);
        return Result.Success;
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = DeriveHash(password, salt);

        var combined = new byte[SaltSize + HashSize];
        salt.CopyTo(combined, 0);
        hash.CopyTo(combined, SaltSize);
        return Convert.ToBase64String(combined);
    }

    private static bool VerifyPassword(string password, string stored)
    {
        var combined = Convert.FromBase64String(stored);
        var salt = combined[..SaltSize];
        var expectedHash = combined[SaltSize..];
        var actualHash = DeriveHash(password, salt);

        return CryptographicOperations.FixedTimeEquals(expectedHash, actualHash);
    }

    private static byte[] DeriveHash(string password, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            Iterations,
            HashAlgorithm,
            HashSize);

    private AuthResponse BuildAuthResponse(User user)
    {
        var now = DateTime.UtcNow;
        var expiresAt = now.AddMinutes(_jwt.ExpiryMinutes);

        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: [
                new Claim(AppClaims.TokenVersion, user.TokenVersion.ToString()),
                new Claim(AppClaims.UserId, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Name, user.Name),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            ],
            notBefore: now,
            expires: expiresAt,
            signingCredentials: _signingCredentials);

        return new AuthResponse(_tokenHandler.WriteToken(token), user.Name, expiresAt);
    }
}
