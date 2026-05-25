using Kanban.Api.Data;
using Kanban.Api.Models;
using Kanban.Api.Services.Email;
using Kanban.Api.Services.Google;
using Kanban.Api.Tests.TestDoubles;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kanban.Api.Tests.Infrastructure;

/// <summary>
/// Shared base for every <see cref="WebApplicationFactory{TEntryPoint}"/> in the test project.
///
/// Centralizes the wiring that was previously duplicated across ProjectsApiFactory,
/// JwtProjectsApiFactory, ErrorHandlingApiFactory, AuthApiFactory, and friends:
///   - Postgres connection string (Testcontainers via <see cref="PostgresTestContainer"/>)
///   - Test JWT configuration
///   - Replacing real outbound services (Email, Google Calendar) with deterministic doubles
///   - Optional <see cref="TestAuthHandler"/> registration
///
/// Subclasses override only what they specifically need (auth scheme, extra services,
/// startup filters, etc.). Each factory instance resets the database once on first use,
/// preserving the per-class isolation the previous InMemory factories provided.
///
/// Tests that need stricter per-test isolation can inherit from
/// <see cref="IntegrationTestBase{TFactory}"/>.
/// </summary>
public abstract class KanbanApiFactoryBase : WebApplicationFactory<Program>
{
    private TestDatabaseHandle? _database;

    /// <summary>
    /// Whether to register the lightweight <see cref="TestAuthHandler"/> as the default
    /// authentication scheme. Disable for tests that need the real JWT pipeline (e.g. SignalR).
    /// </summary>
    protected virtual bool UseTestAuthHandler => true;

    /// <summary>
    /// Reset the Postgres schema for this factory's database. Call between tests for
    /// full per-test isolation.
    /// </summary>
    public Task ResetDatabaseAsync()
    {
        if (_database is null)
        {
            throw new InvalidOperationException(
                "Factory has not been initialized. Call CreateClient() at least once before resetting.");
        }
        return _database.ResetAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Block synchronously here — host construction must complete before WebApplicationFactory
        // can serve requests, and ConfigureWebHost is not async. Each factory owns its own
        // database so test classes that run in parallel never collide.
        _database ??= PostgresTestContainer.CreateDatabaseAsync().GetAwaiter().GetResult();

        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _database.ConnectionString,
                ["Jwt:Issuer"] = "Kanban.Tests",
                ["Jwt:Audience"] = "Kanban.Tests.Client",
                ["Jwt:Key"] = "super_secret_test_key_12345678901234567890",
                ["Jwt:AccessTokenMinutes"] = "15",
                ["Jwt:RefreshTokenDays"] = "7",
                ["Frontend:Url"] = "http://localhost:5173"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll(typeof(DbContextOptions<ApplicationDbContext>));
            services.RemoveAll<ApplicationDbContext>();
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(_database!.ConnectionString));

            // CRITICAL: never let tests hit real SMTP (user-secrets may set Email:Provider=Smtp).
            services.RemoveAll<IEmailService>();
            services.AddSingleton<IEmailService, RecordingEmailService>();

            services.RemoveAll<IGoogleCalendarService>();
            services.AddScoped<IGoogleCalendarService, NoOpGoogleCalendarService>();

            if (UseTestAuthHandler)
            {
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthHandler.AuthenticationScheme;
                    options.DefaultChallengeScheme = TestAuthHandler.AuthenticationScheme;
                    options.DefaultScheme = TestAuthHandler.AuthenticationScheme;
                }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.AuthenticationScheme, _ => { });
            }

            ConfigureTestServices(services);
        });
    }

    /// <summary>
    /// Hook for subclass-specific overrides (extra services, startup filters, etc.).
    /// </summary>
    protected virtual void ConfigureTestServices(IServiceCollection services)
    {
    }

    /// <summary>
    /// Creates an authenticated client. Requires <see cref="UseTestAuthHandler"/> to be true.
    /// </summary>
    public HttpClient CreateClient(Guid userId)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeaderName, userId.ToString());
        return client;
    }

    public async Task WithDbContextAsync(Func<ApplicationDbContext, Task> action)
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await action(dbContext);
    }

    public async Task<T> WithDbContextSelectAsync<T>(Func<ApplicationDbContext, Task<T>> action)
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await action(dbContext);
    }

    /// <summary>
    /// Persists a confirmed <see cref="ApplicationUser"/> directly via EF Core, bypassing the
    /// Identity password pipeline. Use when a test only cares that a user exists, not how
    /// they were created.
    /// </summary>
    public async Task<Guid> CreateUserAsync(string email)
    {
        var userId = Guid.NewGuid();

        await WithDbContextAsync(async dbContext =>
        {
            dbContext.Users.Add(new ApplicationUser
            {
                Id = userId,
                Email = email,
                UserName = email,
                NormalizedEmail = email.ToUpperInvariant(),
                NormalizedUserName = email.ToUpperInvariant(),
                EmailConfirmed = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                SecurityStamp = Guid.NewGuid().ToString("N")
            });

            await dbContext.SaveChangesAsync();
        });

        return userId;
    }
}
