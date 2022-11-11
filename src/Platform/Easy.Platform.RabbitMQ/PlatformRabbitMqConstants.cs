namespace Easy.Platform.RabbitMQ;

public static class PlatformRabbitMqConstants
{
    /// <summary>
    /// When a queue is bound with "#" (hash) binding key - it will receive all the messages, regardless of the routing key - like in fanout exchange.
    /// When special characters "*" (star) and "#" (hash) aren't used in bindings, the topic exchange will behave just like a direct one.
    /// Example: "Abc.*" match only "Abc.xxx". "Abc.#" match "Abc.xxx", "Abc.xxx.anythingAfter"
    /// </summary>
    public const string FanoutBindingChar = "#";
}
