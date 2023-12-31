module Play

open System
open System.Collections.Concurrent
open System.IO
open System.Threading
open System.Xml
open System.Xml.Serialization

open Argu
open RabbitMQ.Client

open CliArguments
open Types

let play (args : ParseResults<PlayArguments>) (channel : IModel) (cancellationToken : CancellationToken) =

  let path = args.TryGetResult PlayArguments.File
  let queueOverride = args.TryGetResult PlayArguments.RoutingKey
  let exchangeOverride = args.TryGetResult PlayArguments.Exchange

  use messages = new BlockingCollection<RmqMessage>(1000)

  let serializer = new XmlSerializer(typeof<RmqMessage>)

  let file =
    match path with
    | Some path -> new FileStream(path, FileMode.Open, FileAccess.Read) |> Some
    | None -> None

  use reader =
    match file with
    | Some file ->
      let stream = new Compression.BrotliStream(file, Compression.CompressionMode.Decompress)
      XmlReader.Create stream
    | None ->
      eprintfn "No progress can be shown for stdin input"
      XmlReader.Create Console.In

  channel.ConfirmSelect  ()

  let reader =
    async {
      while reader.Read () && cancellationToken.IsCancellationRequested |> not do
        if reader.LocalName = "RmqMessage" then
          let r = reader.ReadSubtree ()
          serializer.Deserialize(r) :?> RmqMessage |> messages.Add

      messages.CompleteAdding ()
    }

  let publisher =
    async {
      messages.GetConsumingEnumerable () |> Seq.iteri (fun i msg ->
        if i % 500 = 0 then
          channel.WaitForConfirmsOrDie ()
          match file with
          | Some file ->
            let percent = int (file.Position * 100L / file.Length) |> min 99 // Cap at 99% to not freak out the Progressbar printing.
            ProgressBar.printProgressBar percent
          | None -> // Can't print a progress bar, when we do not know, how far into the stream we are.
            '\b' |> Array.replicate 100 |> String |> eprintf "%s"
            eprintf "Published %i messages" i
        let props =
          let p = channel.CreateBasicProperties ()
          p.ContentEncoding <- msg.BasicProperties.ContentEncoding
          p.CorrelationId <- msg.BasicProperties.CorrelationId
          p.DeliveryMode <- msg.BasicProperties.DeliveryMode
          p.Expiration <- msg.BasicProperties.Expiration
          p.Priority <- msg.BasicProperties.Priority
          p.ReplyTo <- msg.BasicProperties.ReplyTo
          p.Type <- msg.BasicProperties.Type
          p.UserId <- msg.BasicProperties.UserId

          p

        let rKey = queueOverride |> Option.defaultValue msg.RoutingKey
        let exchange = exchangeOverride |> Option.defaultValue msg.Exchange

        channel.BasicPublish(exchange, rKey, props, msg.Body |> ReadOnlyMemory<byte>)
      )
      if file.IsSome then ProgressBar.printProgressBar 100 else eprintfn ""
      eprintfn "Waiting for final publisher confirmations…"
      channel.WaitForConfirmsOrDie ()
    }

  [ reader; publisher ] |> Async.Parallel |> Async.RunSynchronously |> ignore

  match file with
  | Some file -> file.Dispose ()
  | None -> ()
