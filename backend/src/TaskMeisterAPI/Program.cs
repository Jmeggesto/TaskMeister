using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using TaskMeisterAPI.Configuration;
using TaskMeisterAPI.Data;
using TaskMeisterAPI.Services;
using TaskMeisterAPI.Infrastructure.Auth;
using TaskMeisterAPI.Infrastructure.ModelBinding;

public class Program
{
    public static void Main(string[] args) => new Program().Run(args);

    public void Run(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        ConfigureOptions(builder);
        ConfigureDatabase(builder);
        ConfigureServices(builder);
        ConfigureAuth(builder);
        ConfigureWeb(builder);

        var app = builder.Build();

        InitialiseDatabase(app);
        ConfigurePipeline(app);

        app.Run();
    }

    // ---------------------------------------------------------------------------
    // Builder configuration
    // ---------------------------------------------------------------------------

    private static void ConfigureOptions(WebApplicationBuilder builder)
    {
        builder.Services
            .AddOptions<DatabaseOptions>()
            .Bind(builder.Configuration.GetSection(DatabaseOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services
            .AddOptions<AppOptions>()
            .Bind(builder.Configuration.GetSection(AppOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services
            .AddOptions<JwtOptions>()
            .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentUser, CurrentUser>();           
        builder.Services.AddScoped<RequireUserFilter>(); 
    }

    private static void ConfigureDatabase(WebApplicationBuilder builder)
    {
        var connectionString = builder.Configuration
            .GetSection(DatabaseOptions.SectionName)
            .Get<DatabaseOptions>()?.ConnectionString ?? "Data Source=todos.db";

        builder.Services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite(connectionString));
    }

    private static void ConfigureServices(WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<ITodoService, TodoService>();
        builder.Services.AddScoped<IUserService, UserService>();
        builder.Services.AddScoped<ITokenValidator, TokenVersionValidator>();
    }

    private static void ConfigureAuth(WebApplicationBuilder builder)
    {
        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                var jwtOptions = builder.Configuration
                    .GetSection(JwtOptions.SectionName)
                    .Get<JwtOptions>()!;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Issuer,

                    ValidateAudience = true,
                    ValidAudience = jwtOptions.Audience,

                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtOptions.SecretKey)),

                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };

                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var validator = context.HttpContext.RequestServices.GetRequiredService<ITokenValidator>();
                        var userIdClaim = context.Principal?.FindFirstValue(AppClaims.UserId);
                        var versionClaim = context.Principal?.FindFirstValue(AppClaims.TokenVersion);
                        
                        if (!int.TryParse(userIdClaim, out var userId) ||
                            !int.TryParse(versionClaim, out var tokenVersion) ||
                            !await validator.IsTokenValidAsync(userId, tokenVersion))
                        {
                            context.Fail("Token is invalid or has been revoked.");
                        }
                    }
                };
            });

        builder.Services.AddAuthorization();
    }

    private static void ConfigureWeb(WebApplicationBuilder builder)
    {
        builder.Services.AddControllers(options =>
        {
            options.ModelBinderProviders.Insert(0, new UserEntityBinderProvider());
        })
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.Converters.Add(
                new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
        });

        // Return 422 (not 400) for model-binding / data-annotation failures so that
        // every validation error — whether caught by [ApiController] automatically or
        // by service-level ErrorOr Validation errors — uses the same status code.
        builder.Services.Configure<ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = ctx =>
                new UnprocessableEntityObjectResult(
                    new ValidationProblemDetails(ctx.ModelState))
                {
                    StatusCode = StatusCodes.Status422UnprocessableEntity,
                };
        });

        builder.Services.AddEndpointsApiExplorer();

        builder.Services.AddSwaggerGen(options =>
        {
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "Bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Enter your JWT token."
            });

            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    []
                }
            });
        });

        var allowedOrigins = builder.Configuration
            .GetSection(AppOptions.SectionName)
            .Get<AppOptions>()?.AllowedOrigins ?? [];

        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AppCors", policy =>
                policy.WithOrigins(allowedOrigins)
                      .AllowAnyHeader()
                      .AllowAnyMethod());
        });
    }

    // ---------------------------------------------------------------------------
    // App pipeline
    // ---------------------------------------------------------------------------

    private static void InitialiseDatabase(WebApplication app)
    {
        // In the Test environment the DbContext uses an InMemory database which is
        // created automatically on first access — EnsureCreated() is not needed and
        // would cause a dual-provider conflict with the SQLite registration removed
        // by TestWebApplicationFactory.
        if (app.Environment.IsEnvironment("Test")) return;

        // Auto-create the DB schema on startup.
        // Replace with EF migrations when the schema needs to evolve.
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.EnsureCreated();
    }

    private static void ConfigurePipeline(WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseCors("AppCors");
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
    }
}
