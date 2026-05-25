using System.Collections.Concurrent;
using Kanban.Api.Services.Email;

namespace Kanban.Api.Tests.TestDoubles;

public sealed class RecordingEmailService : IEmailService
{
    public ConcurrentBag<RecordedEmail> SentEmails { get; } = new();

    public Task SendAsync(string toEmail, string subject, string body, CancellationToken cancellationToken = default)
    {
        SentEmails.Add(new RecordedEmail(toEmail, subject, body, null));
        return Task.CompletedTask;
    }

    public Task SendAsync(string toEmail, string subject, EmailBody body, CancellationToken cancellationToken = default)
    {
        SentEmails.Add(new RecordedEmail(toEmail, subject, body.Html, body.PlainText));
        return Task.CompletedTask;
    }
}

public sealed record RecordedEmail(string ToEmail, string Subject, string Body, string? PlainText);
