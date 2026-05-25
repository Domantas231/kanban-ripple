using System.Text;
using Kanban.Api.Configuration.Options;
using Kanban.Api.Data;
using Kanban.Api.Exceptions;
using Kanban.Api.Models;
using Kanban.Api.Services.Auth;
using Kanban.Api.Services.Email;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Kanban.Api.Tests.TestDoubles;

namespace Kanban.Api.Tests.Auth;

public class PasswordResetPropertyTests
{
    private const string AppTokenLoginProvider = "Kanban.Api";
    private const string PasswordResetTokenExpiresAtName = "PasswordResetTokenExpiresAt";

    [Fact]
    public async Task Property_10_PasswordResetSendsLink()
    {
        var fixture = CreateFixture();
        var email = $"reset-link.{Guid.NewGuid():N}@example.com";
        var password = $"Aa1!{Guid.NewGuid():N}";

        await RegisterAndConfirmAsync(fixture, email, password);
        fixture.EmailService.SentEmails.Clear();

        var response = await fixture.AuthService.RequestPasswordResetAsync(new PasswordResetRequest(email));

        Assert.Equal("If an account with that email exists, a password reset link has been sent.", response.Message);
        var sentEmail = Assert.Single(fixture.EmailService.SentEmails);
        Assert.Equal(email, sentEmail.ToEmail);
        Assert.Contains("Reset", sentEmail.Subject, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("password", sentEmail.Subject, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("token=", sentEmail.Body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("email=", sentEmail.Body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Property_11_ValidResetLinkAllowsPasswordChange()
    {
        var fixture = CreateFixture();
        var email = $"valid-reset.{Guid.NewGuid():N}@example.com";
        var password = $"Aa1!{Guid.NewGuid():N}";
        var newPassword = $"Bb2!{Guid.NewGuid():N}";

        await RegisterAndConfirmAsync(fixture, email, password);
        fixture.EmailService.SentEmails.Clear();
        await fixture.AuthService.RequestPasswordResetAsync(new PasswordResetRequest(email));

        var sentEmail = Assert.Single(fixture.EmailService.SentEmails);
        var (encodedEmail, token) = ParseResetEmail(sentEmail.Body);
        Assert.Equal(email, encodedEmail);

        var result = await fixture.AuthService.ResetPasswordAsync(new ResetPasswordRequest(email, token, newPassword));

        Assert.Contains("successfully", result.Message, StringComparison.OrdinalIgnoreCase);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => fixture.AuthService.LoginAsync(new LoginRequest(email, password)));

        var auth = await fixture.AuthService.LoginAsync(new LoginRequest(email, newPassword));
        Assert.Equal(email, auth.Email);
    }

    [Fact]
    public async Task Property_12_PasswordResetInvalidatesLink()
    {
        var fixture = CreateFixture();
        var email = $"invalidate-reset.{Guid.NewGuid():N}@example.com";
        var password = $"Aa1!{Guid.NewGuid():N}";
        var newPassword = $"Cc3!{Guid.NewGuid():N}";

        await RegisterAndConfirmAsync(fixture, email, password);
        fixture.EmailService.SentEmails.Clear();
        await fixture.AuthService.RequestPasswordResetAsync(new PasswordResetRequest(email));

        var sentEmail = Assert.Single(fixture.EmailService.SentEmails);
        var (_, token) = ParseResetEmail(sentEmail.Body);

        await fixture.AuthService.ResetPasswordAsync(new ResetPasswordRequest(email, token, newPassword));

        await Assert.ThrowsAsync<BadRequestException>(
            () => fixture.AuthService.ResetPasswordAsync(new ResetPasswordRequest(email, token, $"Dd4!{Guid.NewGuid():N}")));
    }

    [Fact]
    public async Task Property_13_ResetLinkExpiration()
    {
        var fixture = CreateFixture();
        var email = $"expired-reset.{Guid.NewGuid():N}@example.com";
        var password = $"Aa1!{Guid.NewGuid():N}";

        await RegisterAndConfirmAsync(fixture, email, password);
        fixture.EmailService.SentEmails.Clear();
        await fixture.AuthService.RequestPasswordResetAsync(new PasswordResetRequest(email));

        var sentEmail = Assert.Single(fixture.EmailService.SentEmails);
        var (_, token) = ParseResetEmail(sentEmail.Body);

        var user = await fixture.UserManager.FindByEmailAsync(email);
        Assert.NotNull(user);

        await fixture.UserManager.SetAuthenticationTokenAsync(
            user!,
            AppTokenLoginProvider,
            PasswordResetTokenExpiresAtName,
            DateTime.MinValue.ToString("O"));

        var persistedExpiry = await fixture.UserManager.GetAuthenticationTokenAsync(
            user!,
            AppTokenLoginProvider,
            PasswordResetTokenExpiresAtName);

        Assert.Equal(DateTime.MinValue.ToString("O"), persistedExpiry);

        await Assert.ThrowsAsync<BadRequestException>(
            () => fixture.AuthService.ResetPasswordAsync(new ResetPasswordRequest(email, token, $"Ee5!{Guid.NewGuid():N}")));
    }

    [Fact]
    public async Task Property_14_UnregisteredEmailDoesNotLeakInformation()
    {
        var fixture = CreateFixture();
        var existingEmail = $"known.{Guid.NewGuid():N}@example.com";
        var missingEmail = $"missing.{Guid.NewGuid():N}@example.com";
        var password = $"Aa1!{Guid.NewGuid():N}";

        await RegisterAndConfirmAsync(fixture, existingEmail, password);
        fixture.EmailService.SentEmails.Clear();

        var existingResponse = await fixture.AuthService.RequestPasswordResetAsync(new PasswordResetRequest(existingEmail));
        var missingResponse = await fixture.AuthService.RequestPasswordResetAsync(new PasswordResetRequest(missingEmail));

        Assert.Equal(existingResponse.Message, missingResponse.Message);
        Assert.Single(fixture.EmailService.SentEmails);
        Assert.Equal(existingEmail, fixture.EmailService.SentEmails[0].ToEmail);
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

    private static (string Email, string Token) ParseResetEmail(string body)
    {
        var urlMatch = System.Text.RegularExpressions.Regex.Match(body, @"(https?://\S+)");
        Assert.True(urlMatch.Success, "No URL found in email body");
        var url = urlMatch.Groups[1].Value;

        var uri = new Uri(url);
        var query = QueryHelpers.ParseQuery(uri.Query);

        var encodedToken = Assert.Single(query["token"]);
        var encodedEmail = Assert.Single(query["email"]);

        Assert.False(string.IsNullOrWhiteSpace(encodedToken));
        Assert.False(string.IsNullOrWhiteSpace(encodedEmail));

        var token = encodedToken!;
        var email = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(encodedEmail!));

        return (email, token);
    }

    private static TestFixture CreateFixture()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddDataProtection();
        services.AddMemoryCache();

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase($"password-reset-tests-{Guid.NewGuid():N}"));

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
        var emailService = new RecordingEmailService();
        var fileStorage = new NoOpFileStorageService();
        var hostEnvironment = new TestWebHostEnvironment
        {
            EnvironmentName = Environments.Production
        };

        var profilePhotoOptions = Options.Create(new ProfilePhotoOptions());
        var profileService = new AuthProfileService(userManager, fileStorage, profilePhotoOptions);
        var tokenService = new TokenService(userManager, dbContext, accessor, blocklist, hostEnvironment, jwtOptions, NullLogger<TokenService>.Instance);
        var accountService = new AccountService(userManager, dbContext, tokenService, emailService, profileService, frontendOptions, NullLogger<AccountService>.Instance);

        return new TestFixture(accountService, userManager, dbContext, emailService);
    }

    private sealed record TestFixture(
        AccountService AuthService,
        UserManager<ApplicationUser> UserManager,
        ApplicationDbContext DbContext,
        RecordingEmailService EmailService);

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
