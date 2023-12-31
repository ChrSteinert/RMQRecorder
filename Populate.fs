module Populate

open System

open RabbitMQ.Client
open Argu

open CliArguments

let populate (args : ParseResults<PopulateArguments>) (channel : IModel) (cancellationToken : System.Threading.CancellationToken)=

  let count = args.GetResult(Count, 10000)
  let exchange = args.GetResult(Exchange, "")
  let queue = args.GetResult RoutingKey
  let confirm = args.Contains NoConfirm |> not
  let purge = args.Contains Purge

  if confirm then channel.ConfirmSelect ()
  if purge then
    eprintf "Puring messages from queue \"%s\" " queue
    channel.QueuePurge queue |> eprintfn "(%i messages purged)"
  try
    async {
      for i in [1..count] do
        if i % 500 = 0 then
          if confirm then channel.WaitForConfirmsOrDie ()
          eprintfn "Published %i messages (%i%%)" i (i * 100 / count)
        channel.BasicPublish(exchange, queue, channel.CreateBasicProperties(), ReadOnlyMemory.Empty)
    }
    |> fun c -> Async.RunSynchronously(c, -1, cancellationToken)
  with :? OperationCanceledException -> eprintfn "Aborted publish – will persist queued messages"

  if confirm then
    eprintfn "Waiting for publish to persist"
    channel.WaitForConfirmsOrDie ()
  else
    eprintfn "Delaying a bit so queued messages can be persisted"
    Async.Sleep (TimeSpan.FromSeconds 5) |> Async.RunSynchronously
