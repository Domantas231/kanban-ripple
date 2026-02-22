using Kanban.Api.Data;
using Kanban.Api.Models;
using Kanban.Api.Services.Auth;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kanban.Api.Tests.Auth;

public class AuthServicePropertyTests
{
    [Fact]
    public async Task Property_1_ValidRegistrationCreatesAccount()
    {
        for (var i = 0; i < 15; i++)
        {
            var fixture = CreateFixture();
            var email = $"user{i}.{Guid.NewGuid():N}@example.com";
            var password = $"Aa1!{Guid.NewGuid():N}";

            var result = await fixture.AuthService.RegisterAsync(new RegisterRequest(email, password));

            var createdUser = await fixture.UserManager.FindByEmailAsync(email);
            Assert.NotNull(createdUser);
            Assert.Equal(createdUser!.Id, result.UserId);
            Assert.Equal(email, result.Email);
            Assert.False(string.IsNullOrWhiteSpace(result.AccessToken));
            Assert.False(string.IsNullOrWhiteSpace(result.RefreshToken));
            Assert.True(fixture.HttpContext.Response.Headers.TryGetValue("Set-Cookie", out var setCookie));
            Assert.Contains("refreshToken=", setCookie.ToString());
            Assert.Contains("HttpOnly", setCookie.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Secure", setCookie.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.Contains("SameSite=Strict", setCookie.ToString(), StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Property_2_DuplicateEmailRegistrationFails()
    {
        var fixture = CreateFixture();
        var email = $"duplicate.{Guid.NewGuid():N}@example.com";
        var password = $"Aa1!{Guid.NewGuid():N}";

        await fixture.AuthService.RegisterAsync(new RegisterRequest(email, password));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.AuthService.RegisterAsync(new RegisterRequest(email, password)));

        Assert.Contains("already exists", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Aa1!a")]
    [InlineData("Aa1!aa")]
    [InlineData("Aa1!aaa")]
    public async Task Property_3_PasswordLengthValidation(string shortPassword)
    {
        var fixture = CreateFixture();
        var email = $"shortpwd.{Guid.NewGuid():N}@example.com";

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.AuthService.RegisterAsync(new RegisterRequest(email, shortPassword)));

        Assert.Contains("at least", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Property_5_ValidLoginCreatesSession()
    {
        for (var i = 0; i < 10; i++)
        {
            var fixture = CreateFixture();
            var email = $"login{i}.{Guid.NewGuid():N}@example.com";
            var password = $"Aa1!{Guid.NewGuid():N}";

            await fixture.AuthService.RegisterAsync(new RegisterRequest(email, password));
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
            Assert.True(tokenCount >= 2);
        }
    }

    [Fact]
    public async Task Property_6_InvalidLoginDeniesAccess()
    {
        var fixture = CreateFixture();
        var email = $"invalidlogin.{Guid.NewGuid():N}@example.com";
        var password = $"Aa1!{Guid.NewGuid():N}";

        await fixture.AuthService.RegisterAsync(new RegisterRequest(email, password));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => fixture.AuthService.LoginAsync(new LoginRequest(email, password + "x")));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => fixture.AuthService.LoginAsync(new LoginRequest($"missing.{Guid.NewGuid():N}@example.com", password)));
    }

    [Fact]
    public async Task Property_7_LogoutInvalidatesTokens()
    {
        var fixture = CreateFixture();
        var email = $"logout.{Guid.NewGuid():N}@example.com";
        var password = $"Aa1!{Guid.NewGuid():N}";

        var auth = await fixture.AuthService.RegisterAsync(new RegisterRequest(email, password));
        fixture.HttpContext.Request.Headers.Authorization = $"Bearer {auth.AccessToken}";

        await fixture.AuthService.LogoutAsync(auth.RefreshToken);

        var refreshTokenExists = await fixture.DbContext.RefreshTokens.AnyAsync(x => x.Token == auth.RefreshToken);
        Assert.False(refreshTokenExists);
        Assert.True(fixture.AccessTokenBlocklist.IsBlocked(auth.AccessToken));
        Assert.True(fixture.HttpContext.Response.Headers.TryGetValue("Set-Cookie", out var setCookie));
        Assert.Contains("refreshToken=", setCookie.ToString());
    }

    [Fact]
    public async Task Property_8_AccountDeletionRequiresOwnershipTransfer()
    {
        var fixture = CreateFixture();
        var email = $"owner.{Guid.NewGuid():N}@example.com";
        var password = $"Aa1!{Guid.NewGuid():N}";

        var auth = await fixture.AuthService.RegisterAsync(new RegisterRequest(email, password));
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

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.AuthService.DeleteAccountAsync(auth.UserId));

        Assert.Contains("Transfer ownership", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Property_9_AccountDeletionRemovesAccountAndInvalidatesTokens()
    {
        var fixture = CreateFixture();
        var email = $"delete.{Guid.NewGuid():N}@example.com";
        var password = $"Aa1!{Guid.NewGuid():N}";

        var auth = await fixture.AuthService.RegisterAsync(new RegisterRequest(email, password));
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

    private static TestFixture CreateFixture()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddDataProtection();
        services.AddMemoryCache();

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase($"auth-tests-{Guid.NewGuid():N}"));

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

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "Kanban.Tests",
                ["Jwt:Audience"] = "Kanban.Tests.Client",
                ["Jwt:Key"] = "super_secret_test_key_12345678901234567890",
                ["Jwt:AccessTokenMinutes"] = "15",
                ["Jwt:RefreshTokenDays"] = "7"
            })
            .Build();

        var httpContext = new DefaultHttpContext();
        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var blocklist = new MemoryAccessTokenBlocklist(provider.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>());

        var authService = new AuthService(userManager, dbContext, accessor, blocklist, configuration);

        return new TestFixture(authService, blocklist, userManager, dbContext, httpContext);
    }

    private sealed record TestFixture(
        AuthService AuthService,
        IAccessTokenBlocklist AccessTokenBlocklist,
        UserManager<ApplicationUser> UserManager,
        ApplicationDbContext DbContext,
        HttpContext HttpContext);
}
