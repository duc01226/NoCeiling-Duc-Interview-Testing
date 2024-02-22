using System.Net.Http;
using System.Text;
using System.Text.Json.Serialization;
using Easy.Platform.Common.JsonSerialization;
using Easy.Platform.Infrastructures.PushNotification;

namespace Easy.Platform.FirebasePushNotification.GoogleFcm;

internal interface IFcmSender
{
    Task<FcmResponse> SendAsync(
        string deviceId,
        GoogleNotification payload,
        CancellationToken cancellationToken = default);

    Task<FcmResponse> SendAsync(object payload, CancellationToken cancellationToken = default);
}

/// <summary>
/// Firebase Cloud message sender
/// </summary>
internal sealed class FcmSender : IFcmSender
{
    private readonly FireBasePushNotificationSettings settings;
    private readonly HttpClient http;

    public FcmSender(FireBasePushNotificationSettings settings, HttpClient http)
    {
        this.settings = settings;
        this.http = http;
    }

    /// <summary>
    /// Send Firebase notification.
    /// Please check out payload formats:
    /// https://firebase.google.com/docs/cloud-messaging/concept-options#notifications
    /// The SendAsync method will add/replace "to" value with deviceId
    /// </summary>
    /// <param name="deviceId">Device token (will add `to` to the payload)</param>
    /// <param name="payload">Notification payload that will be serialized using Newtonsoft.Json package</param>
    /// <param name="cancellationToken">cancellationToken</param>
    public async Task<FcmResponse> SendAsync(
        string deviceId,
        GoogleNotification payload,
        CancellationToken cancellationToken = default)
    {
        payload.To = deviceId;
        return await SendAsync(payload, cancellationToken);
    }

    /// <summary>
    /// Send firebase notification.
    /// Please check out payload formats:
    /// https://firebase.google.com/docs/cloud-messaging/concept-options#notifications
    /// The SendAsync method will add/replace "to" value with deviceId
    /// </summary>
    /// <param name="payload">Notification payload that will be serialized using Newtonsoft.Json package</param>
    /// <param name="cancellationToken"></param>
    /// <exception cref="HttpRequestException">Throws exception when not successful</exception>
    public async Task<FcmResponse> SendAsync(object payload, CancellationToken cancellationToken = default)
    {
        var serialized = PlatformJsonSerializer.Serialize(payload);

        using (var httpRequest = new HttpRequestMessage(HttpMethod.Post, settings.ServerUrl))
        {
            httpRequest.Headers.Add("Authorization", $"key = {settings.ServerKey}");

            if (settings.SenderId.IsNotNullOrEmpty())
                httpRequest.Headers.Add("Sender", $"id = {settings.SenderId}");

            httpRequest.Content = new StringContent(serialized, Encoding.UTF8, "application/json");

            using (var response = await http.SendAsync(httpRequest, cancellationToken))
            {
                var responseString = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                    throw new HttpRequestException("Firebase notification error: " + responseString);

                return PlatformJsonSerializer.Deserialize<FcmResponse>(responseString);
            }
        }
    }
}

public class GoogleNotification
{
    [JsonPropertyName("to")]
    public string To { get; set; }

    [JsonPropertyName("priority")]
    public string Priority { get; set; } = "high";

    [JsonPropertyName("data")]
    public Dictionary<string, string> Data { get; set; }

    [JsonPropertyName("notification")]
    public DataPayload Notification { get; set; }

    public static GoogleNotification Create(PushNotificationPlatformMessage message)
    {
        return new GoogleNotification
        {
            Notification = DataPayload.Create(message),
            Data = message.Data
        };
    }

    public class DataPayload
    {
        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("body")]
        public string Body { get; set; }

        /// <summary>
        /// please check out Notification payload support
        /// https://firebase.google.com/docs/cloud-messaging/http-server-ref#notification-payload-support
        /// The value will be returned for iOS by message, and the value is value of the badge on the home screen app icon.
        /// </summary>
        [JsonPropertyName("badge")]
        public int? Badge { get; set; }

        public static DataPayload Create(PushNotificationPlatformMessage message)
        {
            return new DataPayload
            {
                Title = message.Title,
                Body = message.Body,
                Badge = message.Badge
            };
        }
    }
}
