using System.Text.Json.Serialization;

namespace Kanban.Api.Configuration;

public static class SignalRServiceCollectionExtensions
{
    public static IServiceCollection AddProjectSignalR(this IServiceCollection services, IConfiguration configuration)
    {
        var builder = services.AddSignalR()
            .AddJsonProtocol(options =>
            {
                options.PayloadSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
            });

        var connectionString = configuration["Azure:SignalR:ConnectionString"];
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            builder.AddAzureSignalR(options =>
            {
                options.ConnectionString = connectionString;
            });
        }

        return services;
    }
}
