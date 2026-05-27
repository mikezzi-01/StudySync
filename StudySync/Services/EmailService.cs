using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using MimeKit.Text;

namespace StudySync.Services
{
    public class EmailService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration config, ILogger<EmailService> logger)
        {
            _config = config;
            _logger = logger;
        }

        public async Task SendPasswordResetEmailAsync(
            string recipientEmail,
            string recipientName,
            string resetToken)
        {
            try
            {
                var settings = _config.GetSection("EmailSettings");
                var smtpHost = settings["SmtpHost"]!;
                var smtpPort = int.Parse(settings["SmtpPort"]!);
                var senderEmail = settings["SenderEmail"]!;
                var senderName = settings["SenderName"]!;
                var appPassword = settings["AppPassword"]!;

                // Build the email message
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(senderName, senderEmail));
                message.To.Add(new MailboxAddress(recipientName, recipientEmail));
                message.Subject = "StudySync — Password Reset Token";

                // Build HTML body
                var bodyBuilder = new BodyBuilder
                {
                    HtmlBody = BuildResetEmailHtml(recipientName, resetToken),
                    TextBody = BuildResetEmailText(recipientName, resetToken)
                };

                message.Body = bodyBuilder.ToMessageBody();

                // Send via Gmail SMTP
                using var client = new SmtpClient();

                await client.ConnectAsync(
                    smtpHost, smtpPort, SecureSocketOptions.StartTls);

                await client.AuthenticateAsync(senderEmail, appPassword);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);

                _logger.LogInformation(
                    "[EmailService] Password reset email sent to {Email}.",
                    recipientEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    "[EmailService] Failed to send email to {Email}: {Message}",
                    recipientEmail, ex.Message);

                // Rethrow so the caller can handle it
                throw;
            }
        }

        // ── HTML email template ───────────────────────────────────────────
        private static string BuildResetEmailHtml(string name, string token)
        {
            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8' />
    <style>
        body       {{ font-family: Verdana, Geneva, Tahoma, sans-serif;
                      background: #f4f4f8; margin: 0; padding: 20px; }}
        .container {{ max-width: 480px; margin: 0 auto;
                      background: #ffffff; border-radius: 12px;
                      overflow: hidden;
                      box-shadow: 0 4px 20px rgba(0,0,0,0.08); }}
        .header    {{ background: #3D3BF3; color: white;
                      padding: 2rem; text-align: center; }}
        .header h1 {{ font-size: 1.4rem; margin: 0; }}
        .body      {{ padding: 2rem; }}
        .body p    {{ font-size: 0.88rem; color: #555577;
                      line-height: 1.7; margin-bottom: 1rem; }}
        .token-box {{ background: #f0f0fe;
                      border: 2px dashed #3D3BF3;
                      border-radius: 10px;
                      padding: 1.5rem;
                      text-align: center;
                      margin: 1.5rem 0; }}
        .token     {{ font-size: 2rem; font-weight: 800;
                      letter-spacing: 0.3em; color: #3D3BF3;
                      font-family: 'Courier New', monospace; }}
        .expiry    {{ font-size: 0.75rem; color: #8888aa;
                      margin-top: 0.5rem; }}
        .btn       {{ display: block; width: fit-content;
                      margin: 1rem auto;
                      background: #3D3BF3; color: white;
                      padding: 0.8rem 2rem; border-radius: 8px;
                      text-decoration: none; font-weight: 700;
                      font-size: 0.88rem; }}
        .footer    {{ background: #f4f4f8; padding: 1rem 2rem;
                      text-align: center; font-size: 0.72rem;
                      color: #8888aa; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>&#128214; StudySync</h1>
            <p style='margin:0.3rem 0 0; opacity:0.8; font-size:0.85rem;'>
                Password Reset Request
            </p>
        </div>
        <div class='body'>
            <p>Hello <strong>{name}</strong>,</p>
            <p>
                We received a request to reset your StudySync password.
                Use the token below on the Reset Password page.
            </p>
            <div class='token-box'>
                <div class='token'>{token}</div>
                <div class='expiry'>
                    &#128336; This token expires in <strong>30 minutes</strong>.
                </div>
            </div>
            <a href='https://localhost:7123/ResetPassword' class='btn'>
                Reset My Password
            </a>
            <p style='font-size:0.78rem; color:#8888aa;'>
                If you did not request a password reset, you can safely
                ignore this email. Your password will not be changed.
            </p>
        </div>
        <div class='footer'>
            &copy; 2024 StudySync. Designed for Computer Science Scholars.
        </div>
    </div>
</body>
</html>";
        }

        // ── Plain text fallback ───────────────────────────────────────────
        private static string BuildResetEmailText(string name, string token)
        {
            return $@"Hello {name},

We received a request to reset your StudySync password.

Your reset token is: {token}

This token expires in 30 minutes.

Go to https://localhost:7123/ResetPassword and enter your email 
address along with this token to reset your password.

If you did not request a password reset, ignore this email.

-- StudySync Team";
        }
    }
}
