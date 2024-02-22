using Easy.Platform.FirebasePushNotification.GoogleFcm;
using Easy.Platform.Infrastructures;
using Easy.Platform.Infrastructures.PushNotification;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Easy.Platform.FirebasePushNotification;

public abstract class FireBasePushNotificationPlatformModule : PlatformInfrastructureModule
{
    public FireBasePushNotificationPlatformModule(IServiceProvider serviceProvider, IConfiguration configuration) :
        base(serviceProvider, configuration)
    {
    }

    protected override void InternalRegister(IServiceCollection serviceCollection)
    {
        base.InternalRegister(serviceCollection);

        serviceCollection.Register<IPushNotificationPlatformService, FireBasePushNotificationService>();
        serviceCollection.Register(FireBasePushNotificationSettingsProvider);
        serviceCollection.Register<IFcmSender, FcmSender>();
        serviceCollection.AddHttpClient<FcmSender>();
    }

    protected abstract FireBasePushNotificationSettings FireBasePushNotificationSettingsProvider(
        IServiceProvider serviceProvider);
}
