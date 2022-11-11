The process main started from PlatformRabbitMqHostedService.

PlatformRabbitMqHostedService:

This service main purpose is to configure RabbitMq Exchange, Declare Queue for each Consumer based on Consumer Name/Consumer Message Name via RoutingKey.
Then start to connect listening messages, execute consumer which handle the suitable message

Send message via PlatformRabbitMqMessageBusProducer
