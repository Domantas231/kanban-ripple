namespace Kanban.Api.Services.Email;

public interface IEmailService
{
    Task SendAsync(string toEmail, string subject, string body, CancellationToken cancellationToken = default);
}
