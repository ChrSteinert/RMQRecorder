module Record

open System
open System.Collections.Concurrent
open System.IO
open System.Xml.Serialization

open Argu
open RabbitMQ.Client

open CliArguments
open Types


let record (args : ParseResults<RecordArguments>) (channel : IModel) (cancellationToken : System.Threading.CancellationToken) =
  let path = args.GetResult File
  let queue = args.GetResult Queue
  let ack = args.Contains NoAck |> not

  let mutable msgsWritten = 0

  let serializer = new XmlSerializer(typeof<RmqMessage>)
  use file = new FileStream(path, FileMode.Create, FileAccess.Write)
  use deflate = new Compression.BrotliStream(file, Compression.CompressionMode.Compress)
  use writer = 
    let s = Xml.XmlWriterSettings()
    s.Indent <- true
    let w = Xml.XmlWriter.Create(deflate, s)
    w.WriteStartDocument ()
    w.WriteStartElement "Messages"
    w

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
                DeliveryMode = 1uy
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
        System.Threading.Interlocked.Increment(&msgsWritten) |> ignore
    }

  [ messageCreator; serializer ] |> Async.Parallel |> Async.RunSynchronously |> ignore

  printfn "%i messages written to file" msgsWritten
