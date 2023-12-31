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
  let path = args.TryGetResult File
  let queue = args.GetResult Queue
  let ack = args.Contains NoAck |> not

  let mutable msgsWritten = 0
  let mutable msgsRemaining = 0u

  let serializer = new XmlSerializer(typeof<RmqMessage>)
  let file, deflate, writer =
    match path with
    | Some path ->
      let f = new FileStream(path, FileMode.Create, FileAccess.Write)
      let deflate = new Compression.BrotliStream(f, Compression.CompressionMode.Compress)
      let writer =
        let s = Xml.XmlWriterSettings()
        s.Indent <- false
        let w = Xml.XmlWriter.Create(deflate, s)
        w.WriteStartDocument ()
        w.WriteStartElement "Messages"
        w
      Some f, Some deflate, writer
    | None ->
      let writer =
        let s = Xml.XmlWriterSettings()
        s.Indent <- true
        let w = Xml.XmlWriter.Create(Console.Out, s)
        w.WriteStartDocument ()
        w.WriteStartElement "Messages"
        w
      None, None, writer

  use messages = new BlockingCollection<RmqMessage>(1000)

  let messageCreator =
    async {
      while messages.IsAddingCompleted |> not do
        let msg = channel.BasicGet(queue, ack)
        if msg |> isNull || cancellationToken.IsCancellationRequested then messages.CompleteAdding ()
        else
          msgsRemaining <- msg.MessageCount
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
                DeliveryMode = max p.DeliveryMode 1uy // RMQ tends to report DeliveryMode = 0, which is invalid
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
    ProgressBar.printProgressBar 0
    async  {
      for msg in messages.GetConsumingEnumerable () do
        serializer.Serialize(writer, msg)
        System.Threading.Interlocked.Increment(&msgsWritten) |> ignore
        if msgsWritten % 500 = 0 then
          ProgressBar.printProgressBar (msgsWritten * 100 / (msgsWritten + int msgsRemaining))
    }

  [ messageCreator; serializer ] |> Async.Parallel |> Async.RunSynchronously |> ignore

  writer.Dispose ()
  match file, deflate with
  | Some file, Some deflate ->
    file.Dispose ()
    deflate.Dispose ()
  | _ -> ()

  eprintfn ""
  eprintfn "%i messages written to file" msgsWritten
