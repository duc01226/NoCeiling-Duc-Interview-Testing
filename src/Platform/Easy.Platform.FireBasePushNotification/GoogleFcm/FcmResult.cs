using System.Text.Json.Serialization;

namespace Easy.Platform.FireBasePushNotification.GoogleFcm;

/// <summary>
/// Firebase cloud message result
/// </summary>
internal sealed class FcmResult
{
    [JsonPropertyName("message_id")]
    public string MessageId { get; set; }

    [JsonPropertyName("registration_id")]
    public string RegistrationId { get; set; }

    [JsonPropertyName("error")]
    public string Error { get; set; }
}
