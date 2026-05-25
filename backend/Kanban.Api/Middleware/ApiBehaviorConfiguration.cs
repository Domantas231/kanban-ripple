using Microsoft.AspNetCore.Mvc;

namespace Kanban.Api.Middleware;

public static class ApiBehaviorConfiguration
{
    public static IServiceCollection AddApiBehavior(this IServiceCollection services)
    {
        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                var validationErrors = context.ModelState
                    .Where(entry => entry.Value is not null && entry.Value.Errors.Count > 0)
                    .SelectMany(entry => entry.Value!.Errors.Select(error => new
                    {
                        propertyName = entry.Key,
                        errorMessage = string.IsNullOrWhiteSpace(error.ErrorMessage) ? "Validation failed." : error.ErrorMessage,
                        attemptedValue = entry.Value.RawValue
                    }))
                    .ToArray();

                return new BadRequestObjectResult(new
                {
                    error = new
                    {
                        code = "VALIDATION_ERROR",
                        message = "Validation failed.",
                        timestamp = DateTimeOffset.UtcNow,
                        requestId = context.HttpContext.TraceIdentifier,
                        validationErrors
                    }
                });
            };
        });

        return services;
    }
}
