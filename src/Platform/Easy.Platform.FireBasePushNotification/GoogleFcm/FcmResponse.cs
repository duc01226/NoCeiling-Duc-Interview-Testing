using System.Text.Json.Serialization;

namespace Easy.Platform.FireBasePushNotification.GoogleFcm;

/// <summary>
/// Firebase cloud message response
/// </summary>
internal class FcmResponse
{
    [JsonPropertyName("multicast_id")]
    public long MulticastId { get; set; }

    [JsonPropertyName("canonical_ids")]
    public int CanonicalIds { get; set; }

    /// <summary>
    /// Success count
    /// </summary>
    [JsonPropertyName("success")]
    public int Success { get; set; }

    /// <summary>
    /// Failure count
    /// </summary>
    [JsonPropertyName("failure")]
    public int Failure { get; set; }

    /// <summary>
    /// Results
    /// </summary>
    [JsonPropertyName("results")]
    public List<FcmResult> Results { get; set; }

    /// <summary>
    /// Returns value indicating notification sent success or failure
    /// </summary>
    public bool IsSuccess()
    {
        return Success > 0 && Failure == 0;
    }
}
