# RMQ Recorder

… a utility to record messages from a RabbitMQ Queues, save them to a file, and replay them
- later
- to a different exchange, routing key, cluster, etc.
- more than once.

It is intended to make testing of RMQ-using applications easier.
Like when there is a chain of components, taking messages from RabbitMQ, doing some calculation and handing the result off to a different queue for another component to process.
When testing solutions like this it is sometimes useful to be able to skip components in the chain, or be able to share testing messages with the team.
RMQ Recorder can be used for this.
It is inspired by [cassette](https://github.com/uber/cassette).

## General

All commands make use of the connection information provided before the command (see `./RMQRecorder --help`):
```
OPTIONS:

    --hostname <string>   The RMQ HostName to connect to. (Default "localhost")
    --port <int>          The port of the RMQ cluster. (Default 5672)
    --username <string>   The UserName to use for the connection. (Default "guest")
    --password <string>   The Password to use for the connection. (Default "guest")
    --help                display this list of options.
```
### Record

To produce a "recording" of messages currently in a queue, use `record`:
```sh
./RMQRecorder record --queue thequeue -f myfile.rmqr
```
You can add `--noack` to not acknoledge received messages, so that the recorded queue will not be empty after the record.
If no file is specified, (uncompressed) output will be written to stdout.

### Play

To publish messages from a file use `play`:
```sh
./RMQRecorder play -f myfile.rmqr
```

This will publish the stored messages to the exachange and routing key they were originally published to.
Exchange and routing key can be overriden with the `-x` and `-q` arguments.
If no file is specified, input will be taken from stdin.
