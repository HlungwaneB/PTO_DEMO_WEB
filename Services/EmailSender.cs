using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using PTO_Management_System.Interfaces;

namespace PTO_Management_System.Services
{
    public class EmailSender : IEmailSender
    {
        private readonly IConfiguration _config;

        public EmailSender(IConfiguration config)
        {
            _config = config;
        }

        public bool IsEnabled =>
            string.Equals(_config["Email:Enabled"], "true", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(_config["Email:Username"])
            && !string.IsNullOrWhiteSpace(_config["Email:Password"]);

        public async Task SendAsync(string toEmail, string toName, string subject, string title, string message)
        {
            var from = _config["Email:FromAddress"];
            if (string.IsNullOrWhiteSpace(from)) from = _config["Email:Username"];

            var mail = new MimeMessage();
            mail.From.Add(new MailboxAddress(_config["Email:FromName"] ?? "myPTO Mangaung", from));
            mail.To.Add(new MailboxAddress(toName ?? "", toEmail));
            mail.Subject = subject;

            var siteUrl = (_config["Email:SiteUrl"] ?? "").TrimEnd('/');
            var body = new BodyBuilder
            {
                HtmlBody = BuildHtml(title, message, siteUrl + "/MyPto/Notifications"),
                TextBody = title + "\n\n" + message + "\n\nmyPTO - Mangaung Municipality"
            };
            mail.Body = body.ToMessageBody();

            int port = int.TryParse(_config["Email:Port"], out var p) ? p : 587;
            using (var client = new SmtpClient())
            {
                await client.ConnectAsync(_config["Email:Host"] ?? "smtp.gmail.com", port, SecureSocketOptions.StartTls);
                await client.AuthenticateAsync(_config["Email:Username"], _config["Email:Password"]);
                await client.SendAsync(mail);
                await client.DisconnectAsync(true);
            }
        }
        public async Task SendCodeAsync(string toEmail, string toName, string code)
        {
            var from = _config["Email:FromAddress"];
            if (string.IsNullOrWhiteSpace(from)) from = _config["Email:Username"];

            var mail = new MimeMessage();
            mail.From.Add(new MailboxAddress(_config["Email:FromName"] ?? "myPTO Mangaung", from));
            mail.To.Add(new MailboxAddress(toName ?? "", toEmail));
            mail.Subject = "myPTO: your verification code";

            string name = System.Net.WebUtility.HtmlEncode(toName ?? "");
            var body = new BodyBuilder
            {
                HtmlBody = $@"
<div style='font-family:Segoe UI,Arial,sans-serif;max-width:560px;margin:auto;border:1px solid #e5e5e5;border-radius:10px;overflow:hidden'>
  <div style='background:#ff9a52;padding:16px 24px;color:#fff;font-size:20px;font-weight:bold'>myPTO &middot; Mangaung</div>
  <div style='padding:24px;color:#222'>
    <h2 style='margin:0 0 12px 0'>Reset your password</h2>
    <p style='font-size:15px;margin:0 0 16px 0'>Hello {name}, use this code to reset your password:</p>
    <div style='font-size:34px;letter-spacing:10px;font-weight:bold;text-align:center;background:#fff4ea;border:1px dashed #ff9a52;border-radius:8px;padding:14px'>{code}</div>
    <p style='font-size:13px;color:#555;margin:16px 0 0 0'>The code is valid for about 10 minutes. Never share it with anyone. If you did not ask to reset your password, ignore this e-mail: your password stays the same.</p>
  </div>
</div>",
                TextBody = "Your myPTO verification code is " + code + ". It is valid for about 10 minutes. If you did not ask for it, ignore this e-mail."
            };
            mail.Body = body.ToMessageBody();
            await Deliver(mail);
        }

        private async Task Deliver(MimeMessage mail)
        {
            int port = int.TryParse(_config["Email:Port"], out var p) ? p : 587;
            using (var client = new SmtpClient())
            {
                await client.ConnectAsync(_config["Email:Host"] ?? "smtp.gmail.com", port, SecureSocketOptions.StartTls);
                await client.AuthenticateAsync(_config["Email:Username"], _config["Email:Password"]);
                await client.SendAsync(mail);
                await client.DisconnectAsync(true);
            }
        }

        private static string BuildHtml(string title, string message, string link)
        {
            string t = System.Net.WebUtility.HtmlEncode(title);
            string m = System.Net.WebUtility.HtmlEncode(message);
            return $@"
<div style='font-family:Segoe UI,Arial,sans-serif;max-width:560px;margin:auto;border:1px solid #e5e5e5;border-radius:10px;overflow:hidden'>
  <div style='background:#ff9a52;padding:16px 24px;color:#fff;font-size:20px;font-weight:bold'>myPTO &middot; Mangaung</div>
  <div style='padding:24px;color:#222'>
    <h2 style='margin:0 0 12px 0'>{t}</h2>
    <p style='font-size:15px;line-height:1.5;margin:0 0 20px 0'>{m}</p>
    <a href='{link}' style='background:#ff9a52;color:#fff;text-decoration:none;padding:10px 18px;border-radius:6px;font-weight:bold'>Open my notifications</a>
  </div>
  <div style='background:#f7f7f7;padding:12px 24px;color:#888;font-size:12px'>This is an automatic message from the Permission To Occupy system. Please do not reply.</div>
</div>";
        }
    }
}