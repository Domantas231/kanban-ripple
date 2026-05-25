using Microsoft.Extensions.Options;

namespace Kanban.Api.Configuration.Options;

public sealed class JwtOptionsValidator : IValidateOptions<JwtOptions>
{
    private const int MinimumKeyByteLength = 32;

    public ValidateOptionsResult Validate(string? name, JwtOptions options)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.Issuer))
        {
            failures.Add("Jwt:Issuer is missing.");
        }

        if (string.IsNullOrWhiteSpace(options.Audience))
        {
            failures.Add("Jwt:Audience is missing.");
        }

        if (string.IsNullOrWhiteSpace(options.Key))
        {
            failures.Add("Jwt:Key is missing. Set it via user-secrets (`dotnet user-secrets set \"Jwt:Key\" \"<random>\"`) or an environment variable.");
        }
        else if (System.Text.Encoding.UTF8.GetByteCount(options.Key) < MinimumKeyByteLength)
        {
            failures.Add($"Jwt:Key must be at least {MinimumKeyByteLength} bytes. Generate one with `openssl rand -base64 64`.");
        }

        if (options.AccessTokenMinutes <= 0)
        {
            failures.Add("Jwt:AccessTokenMinutes must be greater than 0.");
        }

        if (options.RefreshTokenDays <= 0)
        {
            failures.Add("Jwt:RefreshTokenDays must be greater than 0.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
