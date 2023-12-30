
open System
open System.Threading

open Argu

open RabbitMQ.Client

open CliArguments

[<EntryPoint>]
let main _ =
  let parser = ArgumentParser.Create<CliArguments>(errorHandler = new ProcessExiter ())
  let args = parser.ParseCommandLine ()
  let f =
    let f =  new ConnectionFactory()
    f.HostName <- args.GetResult(HostName, "localhost")
    f.Port <- args.GetResult(Port, 5672)
    f.UserName <- args.GetResult(UserName, "guest")
    f.Password <- args.GetResult(Password, "guest")
    f

  let c = f.CreateConnection () 
  let channel = c.CreateModel ()

  use cts = new CancellationTokenSource ()

  Console.CancelKeyPress.Add (fun c ->
    c.Cancel <- true
    cts.Cancel ()
  )

  if args.Contains Record then 
    args.PostProcessResult(Record, RecordArguments.Validate)
    Record.record (args.GetResult Record) channel cts.Token
  elif args.Contains Populate then Populate.populate (args.GetResult Populate) channel cts.Token
  elif args.Contains Play then 
    args.PostProcessResult(Play, PlayArguments.Validate)
    Play.play (args.GetResult Play) channel cts.Token
  else failwith "No known command!"

  0
