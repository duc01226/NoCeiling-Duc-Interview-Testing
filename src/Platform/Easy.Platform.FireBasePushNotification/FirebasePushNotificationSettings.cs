namespace Easy.Platform.FirebasePushNotification;

public class FireBasePushNotificationSettings
{
    /// <summary>
    /// FCM Sender ID
    /// </summary>
    public string SenderId { get; set; }

    /// <summary>
    /// FCM Server Key
    /// </summary>
    public string ServerKey { get; set; }

    /// <summary>
    /// FCM Server Key
    /// </summary>
    public string ServerUrl { get; set; } = "https://fcm.googleapis.com/fcm/send";
}
