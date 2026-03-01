using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TaskMeisterAPI.Data;

namespace TaskMeisterAPI.Tests.Fixtures;

/// <summary>
/// Custom WebApplicationFactory that replaces production infrastructure with
/// test-safe equivalents:
///   - SQLite replaced with EF Core InMemory database (fresh per factory instance)
///   - All required options injected via in-memory configuration
///
/// Each test class that uses the constructor pattern (no IClassFixture) receives a
/// fresh instance — and therefore an isolated in-memory database — per test method,
/// because xUnit instantiates a new test class per test by default.
/// </summary>
public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    // Unique name per factory instance so tests never share in-memory state.
    private readonly string _dbName = Guid.NewGuid().ToString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");
        
        // Inject all values required by ValidateDataAnnotations / ValidateOnStart.
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"]                = TestJwt.Issuer,
                ["Jwt:Audience"]              = TestJwt.Audience,
                ["Jwt:SecretKey"]             = TestJwt.SecretKey,
                ["Jwt:ExpiryMinutes"]         = TestJwt.ExpiryMinutes.ToString(),
                ["App:Name"]                  = "TaskMeister Test",
                ["App:AllowedOrigins:0"]      = "*",
                // Satisfies DatabaseOptions [Required] — the real connection is
                // overridden below via ConfigureTestServices.
                ["Database:ConnectionString"] = "Data Source=:memory:",
            });
        });

        // ConfigureTestServices runs after all other service configuration so our
        // registrations take priority over those in Program.cs.
        builder.ConfigureTestServices(services =>
        {
            // Remove ALL EF Core descriptors tied to AppDbContext so no SQLite
            // configuration bleeds through into the test service provider.
            //
            // Crucially this must include IDbContextOptionsConfiguration<AppDbContext>:
            // AddDbContext stores its options lambda as a singleton of this type, and
            // when DbContextOptions<T> is resolved EF Core calls EVERY registered
            // IDbContextOptionsConfiguration<T> — so leaving the SQLite one in place
            // means both SQLite and InMemory extensions end up on the same options
            // object, which EF Core rejects with "two providers registered".
            var toRemove = services
                .Where(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>)
                         || d.ServiceType == typeof(AppDbContext)
                         || d.ServiceType == typeof(IDbContextOptionsConfiguration<AppDbContext>))
                .ToList();
            foreach (var d in toRemove)
                services.Remove(d);

            // Register fresh in-memory database — isolated per factory instance.
            // Service provider caching is left at its default (enabled) so that all
            // DbContext instances within the same factory share the same EF Core
            // internal service provider — and therefore the same InMemoryDatabaseRoot.
            // (EnableServiceProviderCaching(false) would give each context its own root,
            // making data written by one request invisible to the next.)
            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase(_dbName));
        });
    }

    /// <summary>
    /// Seeds the in-memory database before a test runs.
    /// Call after CreateClient() to ensure the app has been built.
    /// </summary>
    public async Task SeedAsync(Func<AppDbContext, Task> seed)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await seed(db);
    }
}
