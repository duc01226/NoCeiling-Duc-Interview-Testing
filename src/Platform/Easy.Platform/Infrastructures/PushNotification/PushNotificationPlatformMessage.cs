using Easy.Platform.Common.Dtos;
using Easy.Platform.Common.Validations;

namespace Easy.Platform.Infrastructures.PushNotification;

public class PushNotificationPlatformMessage : IPlatformDto<PushNotificationPlatformMessage>
{
    public string DeviceId { get; set; }
    public string Title { get; set; }
    public string Body { get; set; }
    public int? Badge { get; set; }
    public Dictionary<string, string> Data { get; set; }

    public PlatformValidationResult<PushNotificationPlatformMessage> Validate()
    {
        return PlatformValidationResult.Valid(this)
            .And(p => !string.IsNullOrEmpty(DeviceId), "DeviceId is missing")
            .And(p => !string.IsNullOrEmpty(Title), "Title is missing")
            .And(p => !string.IsNullOrEmpty(Body), "Body is missing");
    }
}
