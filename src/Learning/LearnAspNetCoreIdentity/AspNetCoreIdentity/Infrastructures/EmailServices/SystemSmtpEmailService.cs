using System.Net;
using System.Net.Mail;
using AspNetCoreIdentity.Infrastructures.Abstract;
using AspNetCoreIdentity.Settings;
using Microsoft.Extensions.Options;

namespace AspNetCoreIdentity.Infrastructures.EmailServices;

public class SystemSmtpEmailService : InfrastructureService, IEmailService
{
    private readonly IOptions<SmtpSetting> smtpSetting;

    public SystemSmtpEmailService(IOptions<SmtpSetting> smtpSetting)
    {
        this.smtpSetting = smtpSetting;
    }

    public async Task SendAsync(MailMessage emailMessage)
    {
        using (var smtpClient = new SmtpClient(
            smtpSetting.Value.Host,
            smtpSetting.Value.Port))
        {
            smtpClient.Credentials = new NetworkCredential(
                userName: smtpSetting.Value.User, // The sender
                password: smtpSetting.Value.Password); // The sender authentication password

            await smtpClient.SendMailAsync(emailMessage);
        }
    }
}
