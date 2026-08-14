using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace StudyAssistant.Services;

public class SmtpEmailService : IEmailService
{
    private readonly string _host;
    private readonly int    _port;
    private readonly string _username;
    private readonly string _password;
    private readonly string _from;

    public SmtpEmailService(IConfiguration config)
    {
        _host     = config["Smtp:Host"]     ?? throw new InvalidOperationException("Smtp:Host is not configured.");
        _port     = int.Parse(config["Smtp:Port"] ?? "587");
        _username = config["Smtp:Username"] ?? throw new InvalidOperationException("Smtp:Username is not configured.");
        _password = config["Smtp:Password"] ?? throw new InvalidOperationException("Smtp:Password is not configured.");
        _from     = config["Smtp:From"]     ?? throw new InvalidOperationException("Smtp:From is not configured.");
    }

    public async Task SendPasswordResetCodeAsync(string toEmail, string code)
    {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(_from));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = "Your StudyAssist password reset code";

        var body = new BodyBuilder
        {
            TextBody = $"Your password reset code is: {code}\n\nThis code expires in 10 minutes.\n\nIf you did not request a password reset, you can safely ignore this email.",
            HtmlBody = $"""
                        <p>Your StudyAssist password reset code is:</p>
                        <h2 style="letter-spacing:4px">{code}</h2>
                        <p>This code expires in <strong>10 minutes</strong>.</p>
                        <p style="color:#888">If you did not request a password reset, you can safely ignore this email.</p>
                        """
        };
        message.Body = body.ToMessageBody();

        using var client = new SmtpClient();
        // Some local networks/VPNs block outbound OCSP/CRL lookups, which makes .NET's
        // TLS stack reject an otherwise-valid certificate with an "incomplete certificate
        // revocation check" SslHandshakeException. Skip the revocation check rather than
        // the chain/trust validation itself.
        client.CheckCertificateRevocation = false;
        await client.ConnectAsync(_host, _port, SecureSocketOptions.StartTls);
        await client.AuthenticateAsync(_username, _password);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }
}
