using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Kanban.Api.Services.Email;

public sealed class SmtpEmailService : IEmailService
{
    private readonly SmtpSettings _smtp;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(
        IOptions<SmtpSettings> smtpOptions,
        IConfiguration configuration,
        ILogger<SmtpEmailService> logger)
    {
        _smtp = smtpOptions.Value;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendAsync(string toEmail, string subject, string body, CancellationToken cancellationToken = default)
    {
        var from = _configuration["Email:From"] ?? "noreply@kanban.local";
        var fromDisplayName = _configuration["Email:FromDisplayName"] ?? "Kanban Ripple";

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(fromDisplayName, from));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };

        await SendMessageAsync(message, toEmail, subject, cancellationToken);
    }

    public async Task SendAsync(string toEmail, string subject, EmailBody body, CancellationToken cancellationToken = default)
    {
        var from = _configuration["Email:From"] ?? "noreply@kanban.local";
        var fromDisplayName = _configuration["Email:FromDisplayName"] ?? "Kanban Ripple";

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(fromDisplayName, from));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;

        var multipart = new MultipartAlternative
        {
            new TextPart("plain") { Text = body.PlainText },
            new TextPart("html") { Text = body.Html }
        };
        message.Body = multipart;

        await SendMessageAsync(message, toEmail, subject, cancellationToken);
    }

    private async Task SendMessageAsync(MimeMessage message, string toEmail, string subject, CancellationToken cancellationToken)
    {
        const int maxAttempts = 2;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                using var client = new SmtpClient();

                var secureSocketOptions = _smtp.UseSsl
                    ? SecureSocketOptions.StartTls
                    : SecureSocketOptions.None;

                await client.ConnectAsync(_smtp.Host, _smtp.Port, secureSocketOptions, cancellationToken);
                await client.AuthenticateAsync(_smtp.Username, _smtp.Password, cancellationToken);
                await client.SendAsync(message, cancellationToken);
                await client.DisconnectAsync(true, cancellationToken);

                _logger.LogInformation("Email sent successfully to {Recipient}, subject: {Subject}", toEmail, subject);
                return;
            }
            catch (Exception ex) when (attempt < maxAttempts && IsTransient(ex))
            {
                _logger.LogWarning(ex, "Transient SMTP error on attempt {Attempt} for {Recipient}, retrying in 2s", attempt, toEmail);
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {Recipient}, subject: {Subject}", toEmail, subject);
                throw;
            }
        }
    }

    private static bool IsTransient(Exception ex)
    {
        if (ex is SmtpCommandException cmd
            && (int)cmd.StatusCode >= 400
            && (int)cmd.StatusCode < 500)
        {
            return true;
        }

        return ex is SmtpProtocolException
            or System.IO.IOException
            or TimeoutException;
    }
}
