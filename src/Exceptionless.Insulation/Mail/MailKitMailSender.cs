using System;
using System.Threading.Tasks;
using Exceptionless.Core;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Mail;
using Foundatio.Utility;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using MailMessage = Exceptionless.Core.Queues.Models.MailMessage;

namespace Exceptionless.Insulation.Mail {
    public class MailKitMailSender : IMailSender {
        private readonly IAppService _app;

        public MailKitMailSender(IAppService app) {
            _app = app;
        }

        public async Task SendAsync(MailMessage model) {
            var message = CreateMailMessage(model);
            message.Headers.Add("X-Mailer-Machine", Environment.MachineName);
            message.Headers.Add("X-Mailer-Date", SystemClock.UtcNow.ToString());
            message.Headers.Add("X-Auto-Response-Suppress", "All");
            message.Headers.Add("Auto-Submitted", "auto-generated");

            using (var client = new SmtpClient()) {
                client.ServerCertificateValidationCallback = (s, c, h, e) => true;

                await client.ConnectAsync(_app.Config.SmtpHost, _app.Config.SmtpPort, GetSecureSocketOption(_app.Config.SmtpEncryption)).AnyContext();

                // Note: since we don't have an OAuth2 token, disable the XOAUTH2 authentication mechanism.
                client.AuthenticationMechanisms.Remove("XOAUTH2");

                if (!String.IsNullOrEmpty(_app.Config.SmtpUser))
                    await client.AuthenticateAsync(_app.Config.SmtpUser, _app.Config.SmtpPassword).AnyContext();

                await client.SendAsync(message).AnyContext();
                await client.DisconnectAsync(true).AnyContext();
            }
        }

        private SecureSocketOptions GetSecureSocketOption(SmtpEncryption encryption) {
            switch (encryption) {
                case SmtpEncryption.StartTLS:
                    return SecureSocketOptions.StartTls;
                case SmtpEncryption.SSL:
                    return SecureSocketOptions.SslOnConnect;
                default:
                    return SecureSocketOptions.Auto;
            }
        }

        private MimeMessage CreateMailMessage(MailMessage notification) {
            var message = new MimeMessage { Subject = notification.Subject };
            var builder = new BodyBuilder();

            if (!String.IsNullOrEmpty(notification.To))
                message.To.AddRange(InternetAddressList.Parse(notification.To));

            if (!String.IsNullOrEmpty(notification.From))
                message.From.AddRange(InternetAddressList.Parse(notification.From));
            else
                message.From.AddRange(InternetAddressList.Parse(AppConfiguration.Current.SmtpFrom));

            if (!String.IsNullOrEmpty(notification.Body))
                builder.HtmlBody = notification.Body;

            message.Body = builder.ToMessageBody();
            return message;
        }
    }
}