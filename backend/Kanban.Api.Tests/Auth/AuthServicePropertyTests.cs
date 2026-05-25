using Kanban.Api.Configuration.Options;
using Kanban.Api.Data;
using Kanban.Api.Exceptions;
using Kanban.Api.Models;
using Kanban.Api.Services.Auth;
using Kanban.Api.Services.Email;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Kanban.Api.Tests.TestDoubles;

namespace Kanban.Api.Tests.Auth;

public class AuthServicePropertyTests
{
    [Fact]
    public async Task Property_1_ValidRegistrationCreatesAccount()
    {
        for (var i = 0; i < 15; i++)
        {
            using var fixture = CreateFixture();
            var email = $"user{i}.{Guid.NewGuid():N}@example.com";
            var password = $"Aa1!{Guid.NewGuid():N}";

            var result = await fixture.AuthService.RegisterAsync(new RegisterRequest(email, password));

            var createdUser = await fixture.UserManager.FindByEmailAsync(email);
            Assert.NotNull(createdUser);
            Assert.Equal(email, result.Email);
            Assert.False(string.IsNullOrWhiteSpace(result.Message));
            Assert.False(await fixture.UserManager.IsEmailConfirmedAsync(createdUser!));
            Assert.Single(fixture.EmailService.SentEmails);
            Assert.Contains("confirm", fixture.EmailService.SentEmails[0].Subject, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Property_2_DuplicateEmailRegistrationFails()
    {
        using var fixture = CreateFixture();
        var email = $"duplicate.{Guid.NewGuid():N}@example.com";
        var password = $"Aa1!{Guid.NewGuid():N}";

        await fixture.AuthService.RegisterAsync(new RegisterRequest(email, password));

        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => fixture.AuthService.RegisterAsync(new RegisterRequest(email, password)));

        Assert.Contains("already exists", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Aa1!a")]
    [InlineData("Aa1!aa")]
    [InlineData("Aa1!aaa")]
    public async Task Property_3_PasswordLengthValidation(string shortPassword)
    {
        using var fixture = CreateFixture();
        var email = $"shortpwd.{Guid.NewGuid():N}@example.com";

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => fixture.AuthService.RegisterAsync(new RegisterRequest(email, shortPassword)));

        Assert.Contains("at least", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Property_5_ValidLoginCreatesSession()
    {
        for (var i = 0; i < 10; i++)
        {
            using var fixture = CreateFixture();
            var email = $"login{i}.{Guid.NewGuid():N}@example.com";
            var password = $"Aa1!{Guid.NewGuid():N}";

            await RegisterAndConfirmAsync(fixture, email, password);
            var result = await fixture.AuthService.LoginAsync(new LoginRequest(email, password));

            Assert.False(string.IsNullOrWhiteSpace(result.AccessToken));
            Assert.False(string.IsNullOrWhiteSpace(result.RefreshToken));
            Assert.True(result.AccessTokenExpiresAt > DateTime.UtcNow);
            Assert.True(result.RefreshTokenExpiresAt > result.AccessTokenExpiresAt);

            var tokenHandler = new JwtSecurityTokenHandler();
            var jwt = tokenHandler.ReadJwtToken(result.AccessToken);
            Assert.Equal(result.UserId.ToString(), jwt.Subject);
            Assert.Equal("Kanban.Tests", jwt.Issuer);
            Assert.Contains("Kanban.Tests.Client", jwt.Audiences);
            Assert.True(result.AccessTokenExpiresAt <= DateTime.UtcNow.AddMinutes(16));

            var tokenCount = await fixture.DbContext.RefreshTokens.CountAsync(x => x.UserId == result.UserId);
            Assert.True(tokenCount >= 1);
        }
    }

    [Fact]
    public async Task Property_6_InvalidLoginDeniesAccess()
    {
        using var fixture = CreateFixture();
        var email = $"invalidlogin.{Guid.NewGuid():N}@example.com";
        var password = $"Aa1!{Guid.NewGuid():N}";

        await RegisterAndConfirmAsync(fixture, email, password);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => fixture.AuthService.LoginAsync(new LoginRequest(email, password + "x")));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => fixture.AuthService.LoginAsync(new LoginRequest($"missing.{Guid.NewGuid():N}@example.com", password)));
    }

    [Fact]
    public async Task Property_6b_UnconfirmedLoginDeniesAccess()
    {
        using var fixture = CreateFixture();
        var email = $"unconfirmed.{Guid.NewGuid():N}@example.com";
        var password = $"Aa1!{Guid.NewGuid():N}";

        await fixture.AuthService.RegisterAsync(new RegisterRequest(email, password));

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => fixture.AuthService.LoginAsync(new LoginRequest(email, password)));

        Assert.Contains("confirm", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Property_7_LogoutInvalidatesTokens()
    {
        using var fixture = CreateFixture();
        var email = $"logout.{Guid.NewGuid():N}@example.com";
        var password = $"Aa1!{Guid.NewGuid():N}";

        await RegisterAndConfirmAsync(fixture, email, password);
        var auth = await fixture.AuthService.LoginAsync(new LoginRequest(email, password));
        fixture.HttpContext.Request.Headers.Authorization = $"Bearer {auth.AccessToken}";

        await fixture.TokenService.LogoutAsync(auth.RefreshToken);

        var refreshTokenHash = TokenService.HashToken(auth.RefreshToken);
        var refreshTokenExists = await fixture.DbContext.RefreshTokens.AnyAsync(x => x.TokenHash == refreshTokenHash);
        Assert.False(refreshTokenExists);
        Assert.True(fixture.AccessTokenBlocklist.IsBlocked(auth.AccessToken));
        Assert.True(fixture.HttpContext.Response.Headers.TryGetValue("Set-Cookie", out var setCookie));
        Assert.Contains("refreshToken=", setCookie.ToString());
    }

    [Fact]
    public async Task Property_8_AccountDeletionRequiresOwnershipTransfer()
    {
        using var fixture = CreateFixture();
        var email = $"owner.{Guid.NewGuid():N}@example.com";
        var password = $"Aa1!{Guid.NewGuid():N}";

        await RegisterAndConfirmAsync(fixture, email, password);
        var auth = await fixture.AuthService.LoginAsync(new LoginRequest(email, password));
        var user = await fixture.UserManager.FindByIdAsync(auth.UserId.ToString());
        Assert.NotNull(user);

        fixture.DbContext.Projects.Add(new Project
        {
            Id = Guid.NewGuid(),
            Name = "Owned project",
            OwnerId = auth.UserId,
            Owner = user!,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await fixture.DbContext.SaveChangesAsync();

        var eligibility = await fixture.AuthService.CanDeleteAccountAsync(auth.UserId);
        Assert.False(eligibility.CanDelete);
        Assert.True(eligibility.OwnedProjectCount > 0);

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => fixture.AuthService.DeleteAccountAsync(auth.UserId));

        Assert.Contains("Transfer ownership", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Property_9_AccountDeletionRemovesAccountAndInvalidatesTokens()
    {
        using var fixture = CreateFixture();
        var email = $"delete.{Guid.NewGuid():N}@example.com";
        var password = $"Aa1!{Guid.NewGuid():N}";

        await RegisterAndConfirmAsync(fixture, email, password);
        var auth = await fixture.AuthService.LoginAsync(new LoginRequest(email, password));
        fixture.HttpContext.Request.Headers.Authorization = $"Bearer {auth.AccessToken}";

        var eligibility = await fixture.AuthService.CanDeleteAccountAsync(auth.UserId);
        Assert.True(eligibility.CanDelete);

        await fixture.AuthService.DeleteAccountAsync(auth.UserId);

        var user = await fixture.UserManager.FindByIdAsync(auth.UserId.ToString());
        Assert.Null(user);

        var tokenCount = await fixture.DbContext.RefreshTokens.CountAsync(x => x.UserId == auth.UserId);
        Assert.Equal(0, tokenCount);

        Assert.True(fixture.AccessTokenBlocklist.IsBlocked(auth.AccessToken));
        Assert.True(fixture.HttpContext.Response.Headers.TryGetValue("Set-Cookie", out var setCookie));
        Assert.Contains("refreshToken=", setCookie.ToString());
    }

    private static async Task RegisterAndConfirmAsync(TestFixture fixture, string email, string password)
    {
        await fixture.AuthService.RegisterAsync(new RegisterRequest(email, password));

        var user = await fixture.UserManager.FindByEmailAsync(email);
        Assert.NotNull(user);

        var token = await fixture.UserManager.GenerateEmailConfirmationTokenAsync(user!);
        var confirmResult = await fixture.UserManager.ConfirmEmailAsync(user!, token);
        Assert.True(confirmResult.Succeeded);
    }

    private static TestFixture CreateFixture()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddDataProtection();
        services.AddMemoryCache();

        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlite(connection));

        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        var provider = services.BuildServiceProvider();
        var dbContext = provider.GetRequiredService<ApplicationDbContext>();
        dbContext.Database.EnsureCreated();

        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();

        var jwtOptions = Options.Create(new JwtOptions
        {
            Issuer = "Kanban.Tests",
            Audience = "Kanban.Tests.Client",
            Key = "super_secret_test_key_12345678901234567890",
            AccessTokenMinutes = 15,
            RefreshTokenDays = 7
        });
        var frontendOptions = Options.Create(new FrontendOptions { Url = "http://localhost:5173" });

        var httpContext = new DefaultHttpContext();
        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var blocklist = new MemoryAccessTokenBlocklist(provider.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>());
        var hostEnvironment = new TestWebHostEnvironment
        {
            EnvironmentName = Environments.Production
        };

        var emailService = new RecordingEmailService();
        var fileStorage = new NoOpFileStorageService();
        var profilePhotoOptions = Options.Create(new ProfilePhotoOptions());
        var profileService = new AuthProfileService(userManager, fileStorage, profilePhotoOptions);
        var tokenService = new TokenService(userManager, dbContext, accessor, blocklist, hostEnvironment, jwtOptions, NullLogger<TokenService>.Instance);
        var accountService = new AccountService(userManager, dbContext, tokenService, emailService, profileService, frontendOptions, NullLogger<AccountService>.Instance);

        return new TestFixture(accountService, tokenService, blocklist, userManager, dbContext, httpContext, emailService, connection);
    }

    private sealed class TestFixture : IDisposable
    {
        public TestFixture(
            AccountService authService,
            TokenService tokenService,
            IAccessTokenBlocklist accessTokenBlocklist,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext dbContext,
            HttpContext httpContext,
            RecordingEmailService emailService,
            SqliteConnection connection)
        {
            AuthService = authService;
            TokenService = tokenService;
            AccessTokenBlocklist = accessTokenBlocklist;
            UserManager = userManager;
            DbContext = dbContext;
            HttpContext = httpContext;
            EmailService = emailService;
            _connection = connection;
        }

        private readonly SqliteConnection _connection;

        public AccountService AuthService { get; }
        public TokenService TokenService { get; }
        public IAccessTokenBlocklist AccessTokenBlocklist { get; }
        public UserManager<ApplicationUser> UserManager { get; }
        public ApplicationDbContext DbContext { get; }
        public HttpContext HttpContext { get; }
        public RecordingEmailService EmailService { get; }

        public void Dispose()
        {
            DbContext.Dispose();
            _connection.Dispose();
        }
    }

    private sealed class RecordingEmailService : IEmailService
    {
        public List<SentEmail> SentEmails { get; } = [];

        public Task SendAsync(string toEmail, string subject, string body, CancellationToken cancellationToken = default)
        {
            SentEmails.Add(new SentEmail(toEmail, subject, body));
            return Task.CompletedTask;
        }

        public Task SendAsync(string toEmail, string subject, EmailBody body, CancellationToken cancellationToken = default)
        {
            SentEmails.Add(new SentEmail(toEmail, subject, body.PlainText));
            return Task.CompletedTask;
        }
    }

    private sealed record SentEmail(string ToEmail, string Subject, string Body);
}
