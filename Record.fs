module Consume

open System
open System.Collections.Concurrent
open System.Xml.Serialization

open Argu
open RabbitMQ.Client

open CliArguments
open Types


let consume (args : ParseResults<ConsumeArguments>) (channel : IModel) (cancellationToken : System.Threading.CancellationToken) =
  let queue = args.GetResult Queue
  let ack = args.Contains NoAck |> not

  let serializer = new XmlSerializer(typeof<RmqMessage>)
  use writer = 
    let s = Xml.XmlWriterSettings()
    s.Indent <- true
    let w = Xml.XmlWriter.Create(Console.Out, s)
    w
  writer.WriteStartDocument ()
  writer.WriteStartElement "Messages"
  use messages = new BlockingCollection<RmqMessage>(1000)

  let messageCreator = 
    async {
      while messages.IsAddingCompleted |> not do
        let msg = channel.BasicGet(queue, ack)
        if msg |> isNull || cancellationToken.IsCancellationRequested then messages.CompleteAdding ()
        else
          let body = msg.Body.ToArray ()
          let props = 
            let p = msg.BasicProperties
            if p  |> isNull then 
              {
                ContentEncoding = ""
                CorrelationId = ""
                DeliveryMode = 0uy
                Expiration = ""
                MessageId = ""
                Priority = 0uy
                ReplyTo = ""
                Type = ""
                UserId = ""
              }
            else 
              {
                ContentEncoding = p.ContentEncoding
                CorrelationId = p.CorrelationId
                DeliveryMode = p.DeliveryMode
                Expiration = p.Expiration
                MessageId = p.MessageId
                Priority = p.Priority
                ReplyTo = p.ReplyTo
                Type = p.Type
                UserId = p.UserId
              }
          {
            Body = body
            Exchange = msg.Exchange
            RoutingKey = msg.RoutingKey
            BasicProperties = props
          }
          |> messages.Add
    }

  let serializer =
    async  {
      for msg in messages.GetConsumingEnumerable () do
        serializer.Serialize(writer, msg)
    }

  [ messageCreator; serializer ] |> Async.Parallel |> Async.RunSynchronously |> ignore
