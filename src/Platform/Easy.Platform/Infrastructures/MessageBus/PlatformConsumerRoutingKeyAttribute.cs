using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.Validations;

namespace Easy.Platform.Infrastructures.MessageBus;

/// <summary>
/// Define message routing key for <inheritdoc cref="IPlatformMessageBusConsumer" />.
/// If not defined, consumer will bind to default free format message routing key from <inheritdoc cref="PlatformBusMessageRoutingKey.BuildDefaultRoutingKey" />
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class PlatformConsumerRoutingKeyAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PlatformConsumerRoutingKeyAttribute" /> class.
    /// Pattern value support <see cref="PlatformBusMessageRoutingKey.MatchAllSingleGroupLevelChar" /> to match startWith (*XXX), endWith (XXX*) and contains (*XXX*)
    /// </summary>
    /// <param name="messageGroup">
    ///     <see cref="PlatformBusMessageRoutingKey.MessageGroup" />
    /// </param>
    /// <param name="producerContext">
    ///     <see cref="PlatformBusMessageRoutingKey.ProducerContext" />
    /// </param>
    /// <param name="messageType">
    ///     <see cref="PlatformBusMessageRoutingKey.MessageType" />
    /// </param>
    /// <param name="messageAction">
    ///     <see cref="PlatformBusMessageRoutingKey.MessageAction" />
    /// </param>
    public PlatformConsumerRoutingKeyAttribute(
        string messageGroup,
        string producerContext,
        string messageType,
        string messageAction = null)
    {
        MessageGroup = PlatformBusMessageRoutingKey.AutoFixKeyPart(messageGroup);
        ProducerContext = PlatformBusMessageRoutingKey.AutoFixKeyPart(producerContext) ?? PlatformBusMessageRoutingKey.MatchAllSingleGroupLevelChar;
        MessageType = PlatformBusMessageRoutingKey.AutoFixKeyPart(messageType) ?? PlatformBusMessageRoutingKey.MatchAllSingleGroupLevelChar;
        MessageAction = PlatformBusMessageRoutingKey.AutoFixKeyPart(messageAction) ?? PlatformBusMessageRoutingKey.MatchAllSingleGroupLevelChar;

        EnsureValid();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PlatformConsumerRoutingKeyAttribute" /> class.
    /// Input combined ket pattern. Example: "AA.BB.CC", "AA.*.CC.*", ...
    /// </summary>
    /// <param name="combinedKeyPattern">Combined key pattern string represent for {messageGroup}.{producerContext}.{messageType}.{messageAction}</param>
    public PlatformConsumerRoutingKeyAttribute(string combinedKeyPattern)
    {
        var combinedPatternParts = combinedKeyPattern.Split(".").ToList();

        MessageGroup = combinedPatternParts.ElementAtOrDefault(0);
        ProducerContext = combinedPatternParts.ElementAtOrDefault(1) ?? PlatformBusMessageRoutingKey.MatchAllSingleGroupLevelChar;
        MessageType = combinedPatternParts.ElementAtOrDefault(2) ?? PlatformBusMessageRoutingKey.MatchAllSingleGroupLevelChar;
        MessageAction = combinedPatternParts.ElementAtOrDefault(3) ?? PlatformBusMessageRoutingKey.MatchAllSingleGroupLevelChar;

        EnsureValid();
    }

    public PlatformConsumerRoutingKeyAttribute(string messageGroup, string customRoutingKey)
    {
        MessageGroup = messageGroup;
        CustomRoutingKey = customRoutingKey;

        EnsureValid();
    }

    /// <summary>
    ///     <see cref="PlatformBusMessageRoutingKey.MessageGroup" />
    /// </summary>
    public string MessageGroup { get; }

    /// <summary>
    ///     <see cref="PlatformBusMessageRoutingKey.ProducerContext" />
    /// </summary>
    public string ProducerContext { get; }

    /// <summary>
    ///     <see cref="PlatformBusMessageRoutingKey.MessageType" />
    /// </summary>
    public string MessageType { get; }

    /// <summary>
    ///     <see cref="PlatformBusMessageRoutingKey.MessageAction" />
    /// </summary>
    public string MessageAction { get; }

    /// <summary>
    /// Custom free text routing key for this consumer to consume messages
    /// </summary>
    public string CustomRoutingKey { get; set; }

    public static bool CanMessageBusConsumerProcess(Type eventBusConsumerType, string routingKey)
    {
        var consumerAttributes = eventBusConsumerType
            .GetCustomAttributes(typeof(PlatformConsumerRoutingKeyAttribute), true)
            .Select(p => (PlatformConsumerRoutingKeyAttribute)p)
            .ToList();

        return consumerAttributes.Any(p => p.IsMatchMessageRoutingKey(routingKey));
    }

    public bool IsMatchMessageRoutingKey(string messageRoutingKey)
    {
        if (CustomRoutingKey.IsNotNullOrEmpty())
            return PlatformBusMessageRoutingKey.IsMatchRoutingKeyPattern(
                       routingKeyPattern: CustomRoutingKey,
                       messageRoutingKey) ||
                   PlatformBusMessageRoutingKey.IsMatchRoutingKeyPattern(
                       routingKeyPattern: PlatformBusMessageRoutingKey.BuildCombinedStringKey(
                           MessageGroup,
                           CustomRoutingKey),
                       messageRoutingKey);
        return ToPlatformRoutingKey().Match(messageRoutingKey);
    }

    public PlatformBusMessageRoutingKey ToPlatformRoutingKey()
    {
        var patternRoutingKey = PlatformBusMessageRoutingKey.New(
            messageGroup: MessageGroup,
            producerContext: ProducerContext,
            messageType: MessageType,
            messageAction: MessageAction);
        return patternRoutingKey;
    }

    public string ConsumerBindingRoutingKey()
    {
        return CustomRoutingKey.IsNotNullOrEmpty()
            ? PlatformBusMessageRoutingKey.BuildCombinedStringKey(MessageGroup, CustomRoutingKey)
            : PlatformBusMessageRoutingKey.BuildCombinedStringKey(MessageGroup, ProducerContext, MessageType);
    }

    private void EnsureValid(Func<PlatformValidationResult, Exception> exceptionProvider = null)
    {
        if (CustomRoutingKey.IsNotNullOrEmpty())
            PlatformBusMessageRoutingKey
                .New(
                    MessageGroup,
                    PlatformBusMessageRoutingKey.MatchAllSingleGroupLevelChar,
                    CustomRoutingKey)
                .EnsureValid(forMatchingPattern: true, exceptionProvider);
        else
            ToPlatformRoutingKey().EnsureValid(forMatchingPattern: true, exceptionProvider);
    }
}
