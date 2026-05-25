using Kanban.Api.Configuration.Options;
using Microsoft.Extensions.Options;

namespace Kanban.Api.Configuration;

public static class OptionsServiceCollectionExtensions
{
    public static IServiceCollection AddTypedOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<JwtOptions>, JwtOptionsValidator>();

        services.Configure<FrontendOptions>(configuration.GetSection(FrontendOptions.SectionName));
        services.Configure<InvitationOptions>(configuration.GetSection(InvitationOptions.SectionName));
        services.Configure<ProfilePhotoOptions>(configuration.GetSection(ProfilePhotoOptions.SectionName));
        services.Configure<GoogleOAuthOptions>(configuration.GetSection(GoogleOAuthOptions.SectionName));

        return services;
    }
}
