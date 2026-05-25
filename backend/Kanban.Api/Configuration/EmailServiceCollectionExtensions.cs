using Kanban.Api.Services.Email;

namespace Kanban.Api.Configuration;

public static class EmailServiceCollectionExtensions
{
    public static IServiceCollection AddEmail(this IServiceCollection services, IConfiguration configuration)
    {
        var emailProvider = configuration["Email:Provider"] ?? "Console";
        if (string.Equals(emailProvider, "Smtp", StringComparison.OrdinalIgnoreCase))
        {
            services.Configure<SmtpSettings>(configuration.GetSection("Email:Smtp"));
            services.AddScoped<IEmailService, SmtpEmailService>();
        }
        else
        {
            services.AddScoped<IEmailService, ConsoleEmailService>();
        }

        return services;
    }
}
