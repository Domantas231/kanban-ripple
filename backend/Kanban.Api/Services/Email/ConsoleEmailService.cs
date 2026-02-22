namespace Kanban.Api.Services.Email;

public class ConsoleEmailService(ILogger<ConsoleEmailService> logger) : IEmailService
{
    public Task SendAsync(string toEmail, string subject, string body, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Email dispatch (dev): To={ToEmail}; Subject={Subject}; Body={Body}", toEmail, subject, body);
        return Task.CompletedTask;
    }
}
