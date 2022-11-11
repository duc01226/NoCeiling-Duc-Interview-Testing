using Microsoft.Extensions.Logging;
using PlatformExampleApp.TextSnippet.Application.Infrastructures;

namespace PlatformExampleApp.TextSnippet.Infrastructure;

/// <summary>
/// Example of implementation directly in the application module. It is ok but not recommended.
/// </summary>
public class AwsSendMailService : ISendMailService
{
    private readonly ILogger<AwsSendMailService> logger;

    public AwsSendMailService(ILogger<AwsSendMailService> logger)
    {
        this.logger = logger;
    }

    public void SendEmail(string toEmail, string mailHeader, string mailContent)
    {
        logger.LogInformation(
            "Demo implemented AwsSendMailService directly in application module. " +
            $"ToEmail: {toEmail}. Header: {mailHeader}. MailContent: {mailContent}");
    }
}
