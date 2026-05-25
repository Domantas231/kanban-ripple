namespace Kanban.Api.Services.Email;

public record EmailBody(string PlainText, string Html);

public static class EmailTemplates
{
    private const string PrimaryColor = "#0D9488";
    private const string BackgroundColor = "#F8FAFB";
    private const string CardColor = "#FFFFFF";
    private const string TextColor = "#0F172A";
    private const string MutedTextColor = "#475569";
    private const string FontFamily = "'Plus Jakarta Sans', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif";

    public static EmailBody PasswordReset(string resetUrl, string expiresIn)
    {
        var plainText = $"""
            Hi,

            We received a request to reset your password.

            Reset your password using this link: {resetUrl}

            This link expires in {expiresIn}.

            If you didn't request this, you can safely ignore this email.

            — Kanban Ripple
            """;

        var html = WrapInLayout($"""
            <h1 style="margin:0 0 16px;font-size:24px;color:{TextColor};">Reset your password</h1>
            <p style="margin:0 0 12px;font-size:16px;color:{TextColor};">Hi,</p>
            <p style="margin:0 0 24px;font-size:16px;color:{TextColor};">We received a request to reset your password.</p>
            <table role="presentation" cellpadding="0" cellspacing="0" style="margin:0 auto 24px;">
              <tr>
                <td style="border-radius:8px;background-color:{PrimaryColor};">
                  <a href="{resetUrl}" target="_blank" style="display:inline-block;padding:14px 32px;font-size:16px;font-family:{FontFamily};font-weight:600;color:#ffffff;text-decoration:none;">Reset Password</a>
                </td>
              </tr>
            </table>
            <p style="margin:0 0 12px;font-size:14px;color:{MutedTextColor};">This link expires in {expiresIn}.</p>
            <p style="margin:0 0 12px;font-size:14px;color:{MutedTextColor};">If you didn't request this, you can safely ignore this email.</p>
            <p style="margin:0;font-size:12px;color:{MutedTextColor};word-break:break-all;">If the button doesn't work, copy and paste this URL into your browser: {resetUrl}</p>
            """);

        return new EmailBody(plainText, html);
    }

    public static EmailBody EmailConfirmation(string confirmUrl)
    {
        var plainText = $"""
            Hi,

            Thanks for signing up for Kanban Ripple!

            Confirm your email address using this link: {confirmUrl}

            If you didn't create an account, you can safely ignore this email.

            — Kanban Ripple
            """;

        var html = WrapInLayout($"""
            <h1 style="margin:0 0 16px;font-size:24px;color:{TextColor};">Confirm your email</h1>
            <p style="margin:0 0 12px;font-size:16px;color:{TextColor};">Hi,</p>
            <p style="margin:0 0 24px;font-size:16px;color:{TextColor};">Thanks for signing up for Kanban Ripple! Please confirm your email address to activate your account.</p>
            <table role="presentation" cellpadding="0" cellspacing="0" style="margin:0 auto 24px;">
              <tr>
                <td style="border-radius:8px;background-color:{PrimaryColor};">
                  <a href="{confirmUrl}" target="_blank" style="display:inline-block;padding:14px 32px;font-size:16px;font-family:{FontFamily};font-weight:600;color:#ffffff;text-decoration:none;">Confirm Email</a>
                </td>
              </tr>
            </table>
            <p style="margin:0 0 12px;font-size:14px;color:{MutedTextColor};">If you didn't create an account, you can safely ignore this email.</p>
            <p style="margin:0;font-size:12px;color:{MutedTextColor};word-break:break-all;">If the button doesn't work, copy and paste this URL into your browser: {confirmUrl}</p>
            """);

        return new EmailBody(plainText, html);
    }

    public static EmailBody ProjectInvitation(string invitationUrl, string projectName, string inviterName, int expiresInDays)
    {
        var expiryText = expiresInDays == 1 ? "1 day" : $"{expiresInDays} days";

        var plainText = $"""
            Hi,

            {inviterName} has invited you to collaborate on {projectName}.

            Accept the invitation using this link: {invitationUrl}

            This invitation expires in {expiryText}.

            If you don't recognize this invitation, you can safely ignore this email.

            — Kanban Ripple
            """;

        var html = WrapInLayout($"""
            <h1 style="margin:0 0 16px;font-size:24px;color:{TextColor};">You're invited!</h1>
            <p style="margin:0 0 12px;font-size:16px;color:{TextColor};">Hi,</p>
            <p style="margin:0 0 24px;font-size:16px;color:{TextColor};"><strong>{HtmlEncode(inviterName)}</strong> has invited you to collaborate on <strong>{HtmlEncode(projectName)}</strong>.</p>
            <table role="presentation" cellpadding="0" cellspacing="0" style="margin:0 auto 24px;">
              <tr>
                <td style="border-radius:8px;background-color:{PrimaryColor};">
                  <a href="{invitationUrl}" target="_blank" style="display:inline-block;padding:14px 32px;font-size:16px;font-family:{FontFamily};font-weight:600;color:#ffffff;text-decoration:none;">Accept Invitation</a>
                </td>
              </tr>
            </table>
            <p style="margin:0 0 12px;font-size:14px;color:{MutedTextColor};">This invitation expires in {expiryText}.</p>
            <p style="margin:0 0 12px;font-size:14px;color:{MutedTextColor};">If you don't recognize this invitation, you can safely ignore this email.</p>
            <p style="margin:0;font-size:12px;color:{MutedTextColor};word-break:break-all;">If the button doesn't work, copy and paste this URL into your browser: {invitationUrl}</p>
            """);

        return new EmailBody(plainText, html);
    }

    private static string WrapInLayout(string content)
    {
        return $"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
              <meta charset="utf-8" />
              <meta name="viewport" content="width=device-width, initial-scale=1.0" />
              <title>Kanban Ripple</title>
            </head>
            <body style="margin:0;padding:0;background-color:{BackgroundColor};font-family:{FontFamily};">
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background-color:{BackgroundColor};">
                <tr>
                  <td align="center" style="padding:40px 16px;">
                    <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:560px;">
                      <!-- Logo -->
                      <tr>
                        <td align="center" style="padding-bottom:24px;">
                          <span style="font-size:28px;font-weight:700;color:{PrimaryColor};letter-spacing:-0.02em;font-family:{FontFamily};">Kanban Ripple</span>
                        </td>
                      </tr>
                      <!-- Card -->
                      <tr>
                        <td style="background-color:{CardColor};border-radius:8px;padding:40px 32px;text-align:center;">
                          {content}
                        </td>
                      </tr>
                      <!-- Footer -->
                      <tr>
                        <td align="center" style="padding-top:24px;">
                          <p style="margin:0;font-size:12px;color:{MutedTextColor};">You received this email because of your account on Kanban Ripple. If you didn't expect this, you can safely ignore it.</p>
                        </td>
                      </tr>
                    </table>
                  </td>
                </tr>
              </table>
            </body>
            </html>
            """;
    }

    private static string HtmlEncode(string value)
    {
        return System.Net.WebUtility.HtmlEncode(value);
    }
}
