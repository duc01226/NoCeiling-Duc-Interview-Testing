using Easy.Platform.Infrastructures;
using Easy.Platform.Infrastructures.Abstract;
using Microsoft.Extensions.Logging;

namespace PlatformExampleApp.TextSnippet.Application.Infrastructures;

/// <summary>
/// This for demo the best practice example for implementing an infrastructure services.
/// We could implement the implementation right in the application module. But we recommend that it should be
/// a separated project infrastructure module from <see cref="PlatformInfrastructureModule"/>
/// </summary>
public interface ISendMailService : IPlatformInfrastructureService
{
    void SendEmail(string toEmail, string mailHeader, string mailContent);
}

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
            "ToEmail: {ToEmail}. Header: {MailHeader}. MailContent: {MailContent}",
            toEmail,
            mailHeader,
            mailContent);
    }
}
