module Types

[<CLIMutable>]
type RmqMessageProperties =
  {
    ContentEncoding : string
    CorrelationId : string
    DeliveryMode : byte
    Expiration : string
    MessageId : string
    Priority : byte
    ReplyTo : string
    Type : string
    UserId : string
  }

[<CLIMutable>]
type RmqMessage =
  {
    Body : byte array
    Exchange : string
    RoutingKey : string
    BasicProperties : RmqMessageProperties
  }
