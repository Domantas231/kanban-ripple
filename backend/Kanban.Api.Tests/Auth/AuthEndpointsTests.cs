using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using Kanban.Api.Data;
using Kanban.Api.Models;
using Kanban.Api.Services.Auth;
using Kanban.Api.Services.Email;
using Kanban.Api.Tests.TestDoubles;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
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

        await ConfirmEmailAsync(email);

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        Assert.False(string.IsNullOrWhiteSpace(auth!.RefreshToken));

        await _factory.WithDbContextAsync(async dbContext =>
        {
            var refreshTokenHash = TokenService.HashToken(auth.RefreshToken);
            var tokenEntity = await dbContext.RefreshTokens.SingleAsync(x => x.TokenHash == refreshTokenHash);
            tokenEntity.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            await dbContext.SaveChangesAsync();
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        request.Headers.Add("Cookie", $"refreshToken={auth.RefreshToken}");

        var refreshResponse = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse.StatusCode);
    }

    [Fact]
    public async Task Login_GeneratesJwtAndSetsSecureRefreshCookie()
    {
        using var client = CreateClient();
        var email = $"checkpoint.{Guid.NewGuid():N}@example.com";
        var password = "Aa1!validPassword";

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new { email, password });
        registerResponse.EnsureSuccessStatusCode();

        await ConfirmEmailAsync(email);

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        loginResponse.EnsureSuccessStatusCode();

        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        Assert.False(string.IsNullOrWhiteSpace(auth!.AccessToken));

        var tokenHandler = new JwtSecurityTokenHandler();
        var jwt = tokenHandler.ReadJwtToken(auth.AccessToken);

        Assert.Equal("Kanban.Tests", jwt.Issuer);
        Assert.Contains("Kanban.Tests.Client", jwt.Audiences);
        Assert.Equal(auth.UserId.ToString(), jwt.Subject);
        Assert.True(auth.AccessTokenExpiresAt > DateTime.UtcNow);
        Assert.True(auth.AccessTokenExpiresAt <= DateTime.UtcNow.AddMinutes(16));

        Assert.True(loginResponse.Headers.TryGetValues("Set-Cookie", out var setCookieValues));
        var setCookie = string.Join(";", setCookieValues);

        Assert.Contains("refreshToken=", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("HttpOnly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Secure", setCookie, StringComparison.OrdinalIgnoreCase);
        // Factory uses Development env; cookie policy uses SameSite=Lax in dev, Strict in prod.
        Assert.Contains("SameSite=Lax", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConfirmEmail_WithEmptyToken_ReturnsBadRequest()
    {
        using var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/confirm-email", new
        {
            email = "user@example.com",
            token = ""
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ConfirmEmail_WithUnknownEmail_ReturnsBadRequest()
    {
        using var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/confirm-email", new
        {
            email = $"nobody.{Guid.NewGuid():N}@example.com",
            token = "abcd"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ConfirmEmail_AlreadyConfirmed_Returns200WithIdempotentMessage()
    {
        using var client = CreateClient();
        var email = $"already-confirmed.{Guid.NewGuid():N}@example.com";
        var password = "Aa1!validPassword";

        await client.PostAsJsonAsync("/api/auth/register", new { email, password });
        await ConfirmEmailAsync(email);

        var response = await client.PostAsJsonAsync("/api/auth/confirm-email", new
        {
            email,
            token = "anything"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<MessageResponse>();
        Assert.NotNull(result);
        Assert.Contains("already confirmed", result!.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResendConfirmation_AlwaysReturnsGenericMessage()
    {
        using var client = CreateClient();
        var email = $"resend.{Guid.NewGuid():N}@example.com";
        var password = "Aa1!validPassword";
        await client.PostAsJsonAsync("/api/auth/register", new { email, password });

        var response = await client.PostAsJsonAsync("/api/auth/resend-confirmation", new { email });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ResendConfirmation_WithUnknownEmail_StillReturnsGenericMessage()
    {
        using var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/resend-confirmation", new
        {
            email = $"unknown.{Guid.NewGuid():N}@example.com"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ResendConfirmation_WithBlankEmail_ReturnsBadRequest()
    {
        using var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/resend-confirmation", new
        {
            email = ""
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RequestPasswordReset_ReturnsGenericMessageForUnknownEmail()
    {
        using var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/password-reset", new
        {
            email = $"missing.{Guid.NewGuid():N}@example.com"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task RequestPasswordReset_WithBlankEmail_ReturnsBadRequest()
    {
        using var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/password-reset", new
        {
            email = ""
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RequestPasswordReset_KnownUser_PersistsResetTokenAndSendsEmail()
    {
        using var client = CreateClient();
        var email = $"reset-known.{Guid.NewGuid():N}@example.com";
        var password = "Aa1!validPassword";
        await client.PostAsJsonAsync("/api/auth/register", new { email, password });

        var response = await client.PostAsJsonAsync("/api/auth/password-reset", new { email });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await _factory.WithDbContextAsync(async db =>
        {
            var user = await db.Users.SingleAsync(u => u.Email == email);
            var tokens = await db.UserTokens.Where(t => t.UserId == user.Id).ToListAsync();
            Assert.Contains(tokens, t => t.Name == "PasswordResetToken");
            Assert.Contains(tokens, t => t.Name == "PasswordResetTokenExpiresAt");
        });
    }

    [Fact]
    public async Task ResetPassword_WithMissingTokenOrEmail_ReturnsBadRequest()
    {
        using var client = CreateClient();

        var blankToken = await client.PutAsJsonAsync("/api/auth/password-reset", new
        {
            email = "user@example.com",
            token = "",
            newPassword = "AnyValid1!"
        });
        Assert.Equal(HttpStatusCode.BadRequest, blankToken.StatusCode);

        var blankEmail = await client.PutAsJsonAsync("/api/auth/password-reset", new
        {
            email = "",
            token = "anything",
            newPassword = "AnyValid1!"
        });
        Assert.Equal(HttpStatusCode.BadRequest, blankEmail.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_WithUnknownEmail_ReturnsBadRequest()
    {
        using var client = CreateClient();

        var response = await client.PutAsJsonAsync("/api/auth/password-reset", new
        {
            email = $"missing.{Guid.NewGuid():N}@example.com",
            token = "anything",
            newPassword = "AnyValid1!"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_WithoutPriorRequest_ReturnsBadRequest()
    {
        using var client = CreateClient();
        var email = $"reset-no-request.{Guid.NewGuid():N}@example.com";
        var password = "Aa1!validPassword";
        await client.PostAsJsonAsync("/api/auth/register", new { email, password });

        var response = await client.PutAsJsonAsync("/api/auth/password-reset", new
        {
            email,
            token = "doesnt-matter",
            newPassword = "NewValid1!"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithoutCookie_ReturnsUnauthorized()
    {
        using var client = CreateClient();

        var response = await client.PostAsync("/api/auth/refresh", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithValidToken_RotatesTokenAndIssuesNewPair()
    {
        using var client = CreateClient();
        var email = $"refresh-rotate.{Guid.NewGuid():N}@example.com";
        var password = "Aa1!validPassword";

        await client.PostAsJsonAsync("/api/auth/register", new { email, password });
        await ConfirmEmailAsync(email);

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        loginResponse.EnsureSuccessStatusCode();
        var loggedIn = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(loggedIn);

        using var refreshRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        refreshRequest.Headers.Add("Cookie", $"refreshToken={loggedIn!.RefreshToken}");
        var refreshResponse = await client.SendAsync(refreshRequest);

        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        var rotated = await refreshResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(rotated);
        Assert.NotEqual(loggedIn.RefreshToken, rotated!.RefreshToken);

        await _factory.WithDbContextAsync(async db =>
        {
            var oldHash = TokenService.HashToken(loggedIn.RefreshToken);
            var oldToken = await db.RefreshTokens.SingleAsync(x => x.TokenHash == oldHash);
            Assert.True(oldToken.IsRevoked);
        });
    }

    [Fact]
    public async Task Logout_BlocklistsAccessTokenAndRemovesRefreshToken()
    {
        using var client = CreateClient();
        var email = $"logout.{Guid.NewGuid():N}@example.com";
        var password = "Aa1!validPassword";

        await client.PostAsJsonAsync("/api/auth/register", new { email, password });
        await ConfirmEmailAsync(email);

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        loginResponse.EnsureSuccessStatusCode();
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);

        using var logoutRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        logoutRequest.Headers.Add("Authorization", $"Bearer {auth!.AccessToken}");
        logoutRequest.Headers.Add("Cookie", $"refreshToken={auth.RefreshToken}");

        var logoutResponse = await client.SendAsync(logoutRequest);
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);

        await _factory.WithDbContextAsync(async db =>
        {
            var hash = TokenService.HashToken(auth.RefreshToken);
            var stillThere = await db.RefreshTokens.AnyAsync(x => x.TokenHash == hash);
            Assert.False(stillThere);
        });
    }

    [Fact]
    public async Task Me_WithValidJwt_ReturnsUser()
    {
        var (client, _) = await CreateAuthenticatedClientAsync("me");
        try
        {
            var response = await client.GetAsync("/api/auth/me");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        finally
        {
            client.Dispose();
        }
    }

    [Fact]
    public async Task Me_WithoutAuth_ReturnsUnauthorized()
    {
        using var client = CreateClient();

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdateDisplayName_TrimsAndPersistsName()
    {
        using var client = CreateClient();
        var email = $"display-name.{Guid.NewGuid():N}@example.com";
        var password = "Aa1!validPassword";

        await client.PostAsJsonAsync("/api/auth/register", new { email, password });
        await ConfirmEmailAsync(email);
        var auth = await LoginAsync(client, email, password);

        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/auth/display-name")
        {
            Content = JsonContent.Create(new { displayName = "  My New Display Name  " })
        };
        request.Headers.Add("Authorization", $"Bearer {auth.AccessToken}");
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await _factory.WithDbContextAsync(async db =>
        {
            var user = await db.Users.SingleAsync(x => x.Id == auth.UserId);
            Assert.Equal("My New Display Name", user.UserName);
        });
    }

    [Fact]
    public async Task UpdateDisplayName_WithEmptyName_ReturnsBadRequest()
    {
        using var client = CreateClient();
        var email = $"display-name-empty.{Guid.NewGuid():N}@example.com";
        var password = "Aa1!validPassword";

        await client.PostAsJsonAsync("/api/auth/register", new { email, password });
        await ConfirmEmailAsync(email);
        var auth = await LoginAsync(client, email, password);

        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/auth/display-name")
        {
            Content = JsonContent.Create(new { displayName = "   " })
        };
        request.Headers.Add("Authorization", $"Bearer {auth.AccessToken}");
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateDisplayName_WhenAlreadyTakenByAnotherUser_ReturnsConflictWithDuplicateNameCode()
    {
        using var client = CreateClient();
        var taken = $"Taken {Guid.NewGuid():N}";

        // First user claims the display name.
        var firstEmail = $"display-name-conflict-a.{Guid.NewGuid():N}@example.com";
        var password = "Aa1!validPassword";
        await client.PostAsJsonAsync("/api/auth/register", new { email = firstEmail, password });
        await ConfirmEmailAsync(firstEmail);
        var firstAuth = await LoginAsync(client, firstEmail, password);

        using (var firstRequest = new HttpRequestMessage(HttpMethod.Put, "/api/auth/display-name")
        {
            Content = JsonContent.Create(new { displayName = taken })
        })
        {
            firstRequest.Headers.Add("Authorization", $"Bearer {firstAuth.AccessToken}");
            var firstResponse = await client.SendAsync(firstRequest);
            Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        }

        // Second user tries to claim the same display name.
        var secondEmail = $"display-name-conflict-b.{Guid.NewGuid():N}@example.com";
        await client.PostAsJsonAsync("/api/auth/register", new { email = secondEmail, password });
        await ConfirmEmailAsync(secondEmail);
        var secondAuth = await LoginAsync(client, secondEmail, password);

        using var conflictRequest = new HttpRequestMessage(HttpMethod.Put, "/api/auth/display-name")
        {
            Content = JsonContent.Create(new { displayName = taken })
        };
        conflictRequest.Headers.Add("Authorization", $"Bearer {secondAuth.AccessToken}");
        var conflictResponse = await client.SendAsync(conflictRequest);

        Assert.Equal(HttpStatusCode.Conflict, conflictResponse.StatusCode);

        using var doc = JsonDocument.Parse(await conflictResponse.Content.ReadAsStringAsync());
        var error = doc.RootElement.GetProperty("error");
        Assert.Equal("DUPLICATE_NAME", error.GetProperty("code").GetString());
        Assert.Contains(taken, error.GetProperty("message").GetString());
    }

    [Fact]
    public async Task UpdateDisplayName_WhenSameUserKeepsName_ReturnsOk()
    {
        using var client = CreateClient();
        var email = $"display-name-same.{Guid.NewGuid():N}@example.com";
        var password = "Aa1!validPassword";
        var name = $"Same {Guid.NewGuid():N}";

        await client.PostAsJsonAsync("/api/auth/register", new { email, password });
        await ConfirmEmailAsync(email);
        var auth = await LoginAsync(client, email, password);

        using var firstRequest = new HttpRequestMessage(HttpMethod.Put, "/api/auth/display-name")
        {
            Content = JsonContent.Create(new { displayName = name })
        };
        firstRequest.Headers.Add("Authorization", $"Bearer {auth.AccessToken}");
        var firstResponse = await client.SendAsync(firstRequest);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        using var repeatRequest = new HttpRequestMessage(HttpMethod.Put, "/api/auth/display-name")
        {
            Content = JsonContent.Create(new { displayName = name })
        };
        repeatRequest.Headers.Add("Authorization", $"Bearer {auth.AccessToken}");
        var repeatResponse = await client.SendAsync(repeatRequest);

        Assert.Equal(HttpStatusCode.OK, repeatResponse.StatusCode);
    }

    [Fact]
    public async Task UpdateDisplayName_TooLong_ReturnsBadRequest()
    {
        using var client = CreateClient();
        var email = $"display-name-long.{Guid.NewGuid():N}@example.com";
        var password = "Aa1!validPassword";

        await client.PostAsJsonAsync("/api/auth/register", new { email, password });
        await ConfirmEmailAsync(email);
        var auth = await LoginAsync(client, email, password);

        var longName = new string('x', 51);
        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/auth/display-name")
        {
            Content = JsonContent.Create(new { displayName = longName })
        };
        request.Headers.Add("Authorization", $"Bearer {auth.AccessToken}");
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_WithCorrectCurrent_AllowsLoginWithNew()
    {
        using var client = CreateClient();
        var email = $"chpw.{Guid.NewGuid():N}@example.com";
        var password = "Aa1!validPassword";
        var newPassword = "Bb2@anotherPass";

        await client.PostAsJsonAsync("/api/auth/register", new { email, password });
        await ConfirmEmailAsync(email);
        var auth = await LoginAsync(client, email, password);

        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/auth/password")
        {
            Content = JsonContent.Create(new { currentPassword = password, newPassword })
        };
        request.Headers.Add("Authorization", $"Bearer {auth.AccessToken}");
        var changeResponse = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, changeResponse.StatusCode);

        var loginAttempt = await client.PostAsJsonAsync("/api/auth/login", new { email, password = newPassword });
        Assert.Equal(HttpStatusCode.OK, loginAttempt.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_WithWrongCurrent_ReturnsBadRequest()
    {
        using var client = CreateClient();
        var email = $"chpw-bad.{Guid.NewGuid():N}@example.com";
        var password = "Aa1!validPassword";

        await client.PostAsJsonAsync("/api/auth/register", new { email, password });
        await ConfirmEmailAsync(email);
        var auth = await LoginAsync(client, email, password);

        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/auth/password")
        {
            Content = JsonContent.Create(new { currentPassword = "WrongPass1!", newPassword = "Cc3#anotherPass" })
        };
        request.Headers.Add("Authorization", $"Bearer {auth.AccessToken}");
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_WithWeakNew_ReturnsBadRequest()
    {
        using var client = CreateClient();
        var email = $"chpw-weak.{Guid.NewGuid():N}@example.com";
        var password = "Aa1!validPassword";

        await client.PostAsJsonAsync("/api/auth/register", new { email, password });
        await ConfirmEmailAsync(email);
        var auth = await LoginAsync(client, email, password);

        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/auth/password")
        {
            Content = JsonContent.Create(new { currentPassword = password, newPassword = "weak" })
        };
        request.Headers.Add("Authorization", $"Bearer {auth.AccessToken}");
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetProfilePhoto_WithoutPhoto_ReturnsNoContent()
    {
        using var client = CreateClient();
        var email = $"photo-none.{Guid.NewGuid():N}@example.com";
        var password = "Aa1!validPassword";

        await client.PostAsJsonAsync("/api/auth/register", new { email, password });
        await ConfirmEmailAsync(email);
        var auth = await LoginAsync(client, email, password);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/profile-photo");
        request.Headers.Add("Authorization", $"Bearer {auth.AccessToken}");
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task GetUserProfilePhoto_OtherUser_ReturnsNoContentWhenAbsent()
    {
        using var client = CreateClient();
        var email = $"photo-other.{Guid.NewGuid():N}@example.com";
        var password = "Aa1!validPassword";

        await client.PostAsJsonAsync("/api/auth/register", new { email, password });
        await ConfirmEmailAsync(email);
        var auth = await LoginAsync(client, email, password);

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/auth/users/{auth.UserId}/profile-photo");
        request.Headers.Add("Authorization", $"Bearer {auth.AccessToken}");
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task UploadProfilePhoto_WithValidPng_PersistsAndIsRetrievable()
    {
        using var client = CreateClient();
        var email = $"photo-upload.{Guid.NewGuid():N}@example.com";
        var password = "Aa1!validPassword";

        await client.PostAsJsonAsync("/api/auth/register", new { email, password });
        await ConfirmEmailAsync(email);
        var auth = await LoginAsync(client, email, password);

        var pngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        using var content = new MultipartFormDataContent
        {
            { new ByteArrayContent(pngBytes) { Headers = { { "Content-Type", "image/png" } } }, "file", "avatar.png" }
        };

        using var uploadRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/profile-photo")
        {
            Content = content
        };
        uploadRequest.Headers.Add("Authorization", $"Bearer {auth.AccessToken}");
        var uploadResponse = await client.SendAsync(uploadRequest);

        Assert.Equal(HttpStatusCode.OK, uploadResponse.StatusCode);

        using var getRequest = new HttpRequestMessage(HttpMethod.Get, "/api/auth/profile-photo");
        getRequest.Headers.Add("Authorization", $"Bearer {auth.AccessToken}");
        var getResponse = await client.SendAsync(getRequest);

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Equal("image/png", getResponse.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task UploadProfilePhoto_EmptyFile_ReturnsBadRequest()
    {
        using var client = CreateClient();
        var email = $"photo-empty.{Guid.NewGuid():N}@example.com";
        var password = "Aa1!validPassword";

        await client.PostAsJsonAsync("/api/auth/register", new { email, password });
        await ConfirmEmailAsync(email);
        var auth = await LoginAsync(client, email, password);

        using var content = new MultipartFormDataContent
        {
            { new ByteArrayContent(Array.Empty<byte>()) { Headers = { { "Content-Type", "image/png" } } }, "file", "avatar.png" }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/profile-photo") { Content = content };
        request.Headers.Add("Authorization", $"Bearer {auth.AccessToken}");
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UploadProfilePhoto_DisallowedExtension_ReturnsBadRequest()
    {
        using var client = CreateClient();
        var email = $"photo-bad-ext.{Guid.NewGuid():N}@example.com";
        var password = "Aa1!validPassword";

        await client.PostAsJsonAsync("/api/auth/register", new { email, password });
        await ConfirmEmailAsync(email);
        var auth = await LoginAsync(client, email, password);

        using var content = new MultipartFormDataContent
        {
            { new ByteArrayContent(new byte[] { 1, 2, 3 }) { Headers = { { "Content-Type", "application/octet-stream" } } }, "file", "evil.exe" }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/profile-photo") { Content = content };
        request.Headers.Add("Authorization", $"Bearer {auth.AccessToken}");
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DeleteProfilePhoto_WithoutPhoto_IsIdempotent()
    {
        using var client = CreateClient();
        var email = $"photo-del.{Guid.NewGuid():N}@example.com";
        var password = "Aa1!validPassword";

        await client.PostAsJsonAsync("/api/auth/register", new { email, password });
        await ConfirmEmailAsync(email);
        var auth = await LoginAsync(client, email, password);

        using var request = new HttpRequestMessage(HttpMethod.Delete, "/api/auth/profile-photo");
        request.Headers.Add("Authorization", $"Bearer {auth.AccessToken}");
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DeleteProfilePhoto_AfterUpload_RemovesIt()
    {
        using var client = CreateClient();
        var email = $"photo-roundtrip.{Guid.NewGuid():N}@example.com";
        var password = "Aa1!validPassword";

        await client.PostAsJsonAsync("/api/auth/register", new { email, password });
        await ConfirmEmailAsync(email);
        var auth = await LoginAsync(client, email, password);

        using var uploadContent = new MultipartFormDataContent
        {
            { new ByteArrayContent(new byte[] { 0xFF, 0xD8, 0xFF }) { Headers = { { "Content-Type", "image/jpeg" } } }, "file", "avatar.jpg" }
        };

        using var uploadRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/profile-photo") { Content = uploadContent };
        uploadRequest.Headers.Add("Authorization", $"Bearer {auth.AccessToken}");
        var uploadResponse = await client.SendAsync(uploadRequest);
        uploadResponse.EnsureSuccessStatusCode();

        using var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, "/api/auth/profile-photo");
        deleteRequest.Headers.Add("Authorization", $"Bearer {auth.AccessToken}");
        var deleteResponse = await client.SendAsync(deleteRequest);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        using var getRequest = new HttpRequestMessage(HttpMethod.Get, "/api/auth/profile-photo");
        getRequest.Headers.Add("Authorization", $"Bearer {auth.AccessToken}");
        var getResponse = await client.SendAsync(getRequest);
        Assert.Equal(HttpStatusCode.NoContent, getResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteAccount_WithoutOwnedProjects_RemovesUser()
    {
        using var client = CreateClient();
        var email = $"delete-account.{Guid.NewGuid():N}@example.com";
        var password = "Aa1!validPassword";

        await client.PostAsJsonAsync("/api/auth/register", new { email, password });
        await ConfirmEmailAsync(email);
        var auth = await LoginAsync(client, email, password);

        using var request = new HttpRequestMessage(HttpMethod.Delete, "/api/auth/account");
        request.Headers.Add("Authorization", $"Bearer {auth.AccessToken}");
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        await _factory.WithDbContextAsync(async db =>
        {
            var stillThere = await db.Users.AnyAsync(u => u.Id == auth.UserId);
            Assert.False(stillThere);
        });
    }

    [Fact]
    public async Task DeleteAccount_WithOwnedProjects_ReturnsConflict()
    {
        using var client = CreateClient();
        var email = $"delete-account-owner.{Guid.NewGuid():N}@example.com";
        var password = "Aa1!validPassword";

        await client.PostAsJsonAsync("/api/auth/register", new { email, password });
        await ConfirmEmailAsync(email);
        var auth = await LoginAsync(client, email, password);

        await _factory.WithDbContextAsync(async db =>
        {
            db.Projects.Add(new Project
            {
                Id = Guid.NewGuid(),
                Name = "Owned Project",
                OwnerId = auth.UserId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        });

        using var request = new HttpRequestMessage(HttpMethod.Delete, "/api/auth/account");
        request.Headers.Add("Authorization", $"Bearer {auth.AccessToken}");
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    private async Task<AuthResponse> LoginAsync(HttpClient client, string email, string password)
    {
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        loginResponse.EnsureSuccessStatusCode();
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return auth;
    }

    private async Task<(HttpClient Client, AuthResponse Auth)> CreateAuthenticatedClientAsync(string emailPrefix)
    {
        var client = CreateClient();
        var email = $"{emailPrefix}.{Guid.NewGuid():N}@example.com";
        var password = "Aa1!validPassword";

        await client.PostAsJsonAsync("/api/auth/register", new { email, password });
        await ConfirmEmailAsync(email);
        var auth = await LoginAsync(client, email, password);
        return (client, auth);
    }

    private Task ConfirmEmailAsync(string email)
    {
        return _factory.WithDbContextAsync(async dbContext =>
        {
            var user = await dbContext.Users.SingleAsync(u => u.Email == email);
            user.EmailConfirmed = true;
            await dbContext.SaveChangesAsync();
        });
    }

    private HttpClient CreateClient()
    {
        return _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
    }

    private sealed record AuthResponse(Guid UserId, string Email, string AccessToken, DateTime AccessTokenExpiresAt, string RefreshToken, DateTime RefreshTokenExpiresAt);
    private sealed record MessageResponse(string Message);
}

public sealed class AuthApiFactory : Kanban.Api.Tests.Infrastructure.KanbanApiFactoryBase, IAsyncLifetime
{
    // Auth tests exercise the real JWT pipeline, not the lightweight TestAuthHandler.
    protected override bool UseTestAuthHandler => false;

    public Task InitializeAsync() => Task.CompletedTask;

    async Task IAsyncLifetime.DisposeAsync()
    {
        await Task.CompletedTask;
    }

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        // Force JWT bearer validation params to match the in-memory test config
        // (AddJwtAuth captures these eagerly during Program.cs registration, before our
        // ConfigureAppConfiguration override has applied — so we patch them post-hoc here).
        services.PostConfigure<Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerOptions>(
            Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme,
            options =>
            {
                options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = "Kanban.Tests",
                    ValidAudience = "Kanban.Tests.Client",
                    IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                        System.Text.Encoding.UTF8.GetBytes("super_secret_test_key_12345678901234567890")),
                    ClockSkew = TimeSpan.Zero
                };
            });
    }
}
