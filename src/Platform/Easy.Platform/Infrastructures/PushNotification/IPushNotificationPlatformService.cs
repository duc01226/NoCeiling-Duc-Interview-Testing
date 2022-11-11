using Easy.Platform.Infrastructures.Abstract;

namespace Easy.Platform.Infrastructures.PushNotification;

public interface IPushNotificationPlatformService : IPlatformInfrastructureService
{
    public Task SendAsync(PushNotificationPlatformMessage message, CancellationToken cancellationToken);
}
