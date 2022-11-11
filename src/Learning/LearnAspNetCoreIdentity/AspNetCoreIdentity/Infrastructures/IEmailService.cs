using System.Net.Mail;
using AspNetCoreIdentity.Infrastructures.Abstract;

namespace AspNetCoreIdentity.Infrastructures;

public interface IEmailService : IInfrastructureService
{
    Task SendAsync(MailMessage emailMessage);
}
