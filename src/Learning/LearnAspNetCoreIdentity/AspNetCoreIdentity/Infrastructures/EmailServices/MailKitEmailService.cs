using System.Net.Mail;
using AspNetCoreIdentity.Infrastructures.Abstract;
using AspNetCoreIdentity.Settings;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using MimeKit.Text;
using SmtpClient = MailKit.Net.Smtp.SmtpClient;

namespace AspNetCoreIdentity.Infrastructures.EmailServices;

public class MailKitEmailService : InfrastructureService, IEmailService
{
    private readonly IOptions<SmtpSetting> smtpSetting;

    public MailKitEmailService(IOptions<SmtpSetting> smtpSetting)
    {
        this.smtpSetting = smtpSetting;
    }

    public async Task SendAsync(MailMessage emailMessage)
    {
        var mimeMessage = new MimeMessage();
        mimeMessage.From.Add(new MailboxAddress(name: emailMessage.From!.DisplayName, address: emailMessage.From.Address));
        emailMessage.To.ToList().ForEach(toEmailAddress => mimeMessage.To.Add(MailboxAddress.Parse(toEmailAddress.Address)));
        mimeMessage.Subject = emailMessage.Subject;
        mimeMessage.Body = new TextPart(emailMessage.IsBodyHtml ? TextFormat.Html : TextFormat.Plain)
        {
            Text = emailMessage.Body
        };

        using (var smtpClient = new SmtpClient())
        {
            await smtpClient.ConnectAsync(
                smtpSetting.Value.Host,
                smtpSetting.Value.Port,
                SecureSocketOptions.StartTls);

            await smtpClient.AuthenticateAsync(
                userName: smtpSetting.Value.User,
                password: smtpSetting.Value.Password);

            await smtpClient.SendAsync(mimeMessage);

            await smtpClient.DisconnectAsync(quit: true);
        }
    }
}
