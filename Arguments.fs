module CliArguments

open Argu

type PlayArguments =
  | [<AltCommandLine("-f")>] File of string
  | [<AltCommandLine("-q")>] RoutingKey of string
  | [<AltCommandLine("-x")>] Exchange of string

  interface IArgParserTemplate with
    member this.Usage =
      match this with
      | File _ -> "The file containing an RMQRecorder recording. If no file is given, uncompressed input is expected at stdin."
      | Exchange _ -> "Override the Exchange for all messages. If not provided, the original Exchange of each message will be used."
      | RoutingKey _ -> "Override the Routing Key for all messages. If not provided, the original Routing Key of each message will be used."

  static member Validate (args : ParseResults<PlayArguments>) =
    ()

type RecordArguments =
  | [<AltCommandLine("-f")>] File of string
  | [<AltCommandLine("-q")>] Queue of string
  | NoAck

  interface IArgParserTemplate with
    member this.Usage =
      match this with
      | File _ -> "The file to write the recording to. If no file is given, uncompressed output will be given to stdout."
      | Queue _ -> "The queue to read messages from."
      | NoAck -> "Do not Ack messages – they will stay in the queue after being recorded."

  static member Validate (args : ParseResults<RecordArguments>) =
    ()

type PopulateArguments =
  | [<AltCommandLine("-c")>] Count of int
  | [<AltCommandLine("-x")>] Exchange of string
  | [<AltCommandLine("-q"); Mandatory>] RoutingKey of string
  | NoConfirm
  | Purge

  interface IArgParserTemplate with
    member this.Usage =
      match this with
      | Count _ -> "How many messages to populate. (Default 10,000)"
      | Exchange _ -> "What Exchange to publish the messages to. (Default \"\")"
      | RoutingKey _ -> "What Routing Key (/Queue) to use for the messages."
      | NoConfirm -> "Do not wait for publishes to be confirmed."
      | Purge -> "Purge target queue before populating."

type CliArguments =
  | HostName of string
  | Port of int
  | UserName of string
  | Password of string
  | [<CliPrefix(CliPrefix.None)>] Populate of ParseResults<PopulateArguments>
  | [<CliPrefix(CliPrefix.None)>] Record of ParseResults<RecordArguments>
  | [<CliPrefix(CliPrefix.None)>] Play of ParseResults<PlayArguments>

  interface IArgParserTemplate with
    member this.Usage =
      match this with
      | Populate _ -> "Populates empty messages to RMQ."
      | Record _ -> "Consume"
      | Play _ -> "Play"
      | HostName _ -> "The RMQ HostName to connect to. (Default \"localhost\")"
      | Port _ -> "The port of the RMQ cluster. (Default 5672)"
      | UserName _ -> "The UserName to use for the connection. (Default \"guest\")"
      | Password _ -> "The Password to use for the connection. (Default \"guest\")"
