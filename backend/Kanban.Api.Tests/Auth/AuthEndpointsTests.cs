using System.Net;
using System.Net.Http.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using Kanban.Api.Data;
using Kanban.Api.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kanban.Api.Tests.Auth;

public sealed class AuthEndpointsTests : IClassFixture<AuthApiFactory>
{
    private readonly AuthApiFactory _factory;

    public AuthEndpointsTests(AuthApiFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("/api/auth/register", "{}")]
    [InlineData("/api/auth/register", "{\"email\":\"not-an-email\",\"password\":\"StrongPass1!\"}")]
    [InlineData("/api/auth/register", "{\"email\":\"user@example.com\",\"password\":\"short\"}")]
    [InlineData("/api/auth/login", "{}")]
    [InlineData("/api/auth/login", "{\"email\":\"bad\",\"password\":\"x\"}")]
    public async Task ValidationErrors_ReturnBadRequest(string endpoint, string json)
    {
        using var client = CreateClient();
        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        var response = await client.PostAsync(endpoint, content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DuplicateEmailRegistration_ReturnsConflict()
    {
        using var client = CreateClient();
        var email = $"duplicate.{Guid.NewGuid():N}@example.com";
        var password = "Aa1!validPassword";

        var first = await client.PostAsJsonAsync("/api/auth/register", new { email, password });
        var second = await client.PostAsJsonAsync("/api/auth/register", new { email, password });

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task InvalidCredentials_ReturnUnauthorized()
    {
        using var client = CreateClient();
        var email = $"login.{Guid.NewGuid():N}@example.com";
        var password = "Aa1!validPassword";

        await client.PostAsJsonAsync("/api/auth/register", new { email, password });

        var wrongPasswordResponse = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "WrongPass1!" });
        var missingUserResponse = await client.PostAsJsonAsync("/api/auth/login", new { email = $"missing.{Guid.NewGuid():N}@example.com", password });

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPasswordResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, missingUserResponse.StatusCode);
    }

    [Fact]
    public async Task RefreshWithInvalidToken_ReturnsUnauthorized()
    {
        using var client = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        request.Headers.Add("Cookie", "refreshToken=not-a-valid-token");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RefreshWithExpiredToken_ReturnsUnauthorized()
    {
        using var client = CreateClient();
        var email = $"refresh.{Guid.NewGuid():N}@example.com";
        var password = "Aa1!validPassword";

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new { email, password });
        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

        var auth = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        Assert.False(string.IsNullOrWhiteSpace(auth!.RefreshToken));

        await _factory.WithDbContextAsync(async dbContext =>
        {
            var tokenEntity = await dbContext.RefreshTokens.SingleAsync(x => x.Token == auth.RefreshToken);
            tokenEntity.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            await dbContext.SaveChangesAsync();
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        request.Headers.Add("Cookie", $"refreshToken={auth.RefreshToken}");

        var refreshResponse = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse.StatusCode);
    }

    [Fact]
    public async Task Register_GeneratesJwtAndSetsSecureRefreshCookie()
    {
        using var client = CreateClient();
        var email = $"checkpoint.{Guid.NewGuid():N}@example.com";
        var password = "Aa1!validPassword";

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new { email, password });
        registerResponse.EnsureSuccessStatusCode();

        var auth = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        Assert.False(string.IsNullOrWhiteSpace(auth!.AccessToken));

        var tokenHandler = new JwtSecurityTokenHandler();
        var jwt = tokenHandler.ReadJwtToken(auth.AccessToken);

        Assert.Equal("Kanban.Tests", jwt.Issuer);
        Assert.Contains("Kanban.Tests.Client", jwt.Audiences);
        Assert.Equal(auth.UserId.ToString(), jwt.Subject);
        Assert.True(auth.AccessTokenExpiresAt > DateTime.UtcNow);
        Assert.True(auth.AccessTokenExpiresAt <= DateTime.UtcNow.AddMinutes(16));

        Assert.True(registerResponse.Headers.TryGetValues("Set-Cookie", out var setCookieValues));
        var setCookie = string.Join(";", setCookieValues);

        Assert.Contains("refreshToken=", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("HttpOnly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Secure", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SameSite=Strict", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    private HttpClient CreateClient()
    {
        return _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
    }

    private sealed record AuthResponse(Guid UserId, string Email, string AccessToken, DateTime AccessTokenExpiresAt, string RefreshToken, DateTime RefreshTokenExpiresAt);
}

public sealed class AuthApiFactory : WebApplicationFactory<Program>
{
    private string _databaseName = $"auth-endpoints-{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
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

            _databaseName = $"auth-endpoints-{Guid.NewGuid():N}";
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));

            using var scope = services.BuildServiceProvider().CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            dbContext.Database.EnsureCreated();
        });
    }

    public async Task WithDbContextAsync(Func<ApplicationDbContext, Task> action)
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await action(dbContext);
    }
}
