using Easy.Platform.FirebasePushNotification.GoogleFcm;
using Easy.Platform.Infrastructures.PushNotification;
using Microsoft.Extensions.Logging;

namespace Easy.Platform.FirebasePushNotification;

internal sealed class FireBasePushNotificationService : IPushNotificationPlatformService
{
    private readonly IFcmSender fcmSender;
    private readonly ILogger<FireBasePushNotificationService> logger;

    public FireBasePushNotificationService(IFcmSender fcmSender, ILogger<FireBasePushNotificationService> logger)
    {
        this.fcmSender = fcmSender;
        this.logger = logger;
    }

    public async Task SendAsync(PushNotificationPlatformMessage message, CancellationToken cancellationToken)
    {
        message.Validate().EnsureValid();

        var result = await fcmSender.SendAsync(
            deviceId: message.DeviceId,
            payload: GoogleNotification.Create(message),
            cancellationToken);

        if (!result.IsSuccess())
        {
            logger.LogError(
                "Firebase notification error with Device Token Id: {DeviceId} - {Error}",
                message.DeviceId,
                result.Results[0].Error);
        }
    }
}
