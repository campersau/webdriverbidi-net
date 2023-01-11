namespace WebDriverBidi;

using System.Collections.Concurrent;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class ProtocolTransport
{
    private long nextCommandId = 0;
    private readonly ConcurrentDictionary<long, WebDriverBidiCommandData> pendingCommands = new();
    private readonly Connection connection;
    private readonly TimeSpan commandWaitTimeout;
    private readonly Dictionary<string, Type> eventTypes = new();

    public ProtocolTransport() : this(Timeout.InfiniteTimeSpan)
    {
    }

    public ProtocolTransport(TimeSpan commandWaitTimeout) : this(commandWaitTimeout, new Connection())
    {
    }

    public ProtocolTransport(TimeSpan commandWaitTimeout, Connection connection)
    {
        this.commandWaitTimeout = commandWaitTimeout;
        this.connection = connection;
        connection.DataReceived += this.OnMessageReceived;
        connection.LogMessage += this.OnConnectionLogMessage;
    }

    public event EventHandler<LogMessageEventArgs>? LogMessage;

    public event EventHandler<ProtocolEventReceivedEventArgs>? EventReceived;

    public event EventHandler<ProtocolErrorReceivedEventArgs>? ErrorEventReceived;

    public event EventHandler<ProtocolUnknownMessageReceivedEventArgs>? UnknownMessageReceived;

    public async Task Connect(string websocketUri)
    {
        await this.connection.Start(websocketUri);
    }

    public async Task Disconnect()
    {
        await this.connection.Stop();
    }

    public async Task<CommandResult> SendCommandAndWait(CommandSettings command)
    {
        long commandId = await SendCommand(command);
        this.WaitForCommandComplete(commandId, this.commandWaitTimeout);
        return this.GetCommandResponse(commandId);
    }

    public async Task<long> SendCommand(CommandSettings command)
    {
        long commandId  = Interlocked.Increment(ref this.nextCommandId);
        WebDriverBidiCommandData executionData = new(commandId, command);
        if (!this.pendingCommands.TryAdd(executionData.CommandId, executionData))
        {
            throw new WebDriverBidiException($"Could not add command with id {executionData.CommandId}, as id already exists");
        }

        await this.connection.SendData(JsonConvert.SerializeObject(executionData));
        return executionData.CommandId;
    }

    public void WaitForCommandComplete(long commandId, TimeSpan waitTimeout)
    {
        if (!this.pendingCommands.ContainsKey(commandId))
        {
            throw new WebDriverBidiException($"Unknown command id {commandId}");
        }
        else
        {
            if (!this.pendingCommands[commandId].SynchronizationEvent.WaitOne(waitTimeout))
            {
                throw new WebDriverBidiException($"Timed out waiting for response for command id {commandId}");
            }
        }
    }

    public CommandResult GetCommandResponse(long commandId)
    {
        if (this.pendingCommands.TryRemove(commandId, out WebDriverBidiCommandData? command))
        {
            if (command.Result is null)
            {
                if (command.ThrownException is null)
                {
                    throw new WebDriverBidiException($"Result and thrown exception for command with id {commandId} are both null");
                }

                throw command.ThrownException;
            }

            return command.Result;
        }

        throw new WebDriverBidiException($"Could not remove command with id {commandId}");
    }

    public void RegisterEvent(string eventName, Type eventArgsType)
    {
        this.eventTypes[eventName] = eventArgsType;
    }

    protected virtual void OnLogMessage(object? sender, LogMessageEventArgs e)
    {
        if (this.LogMessage is not null)
        {
            this.LogMessage(this, e);
        }
    }

    protected virtual void OnProtocolEventReceived(object? sender, ProtocolEventReceivedEventArgs e)
    {
        if (this.EventReceived is not null)
        {
            this.EventReceived(this, e);
        }
    }

    protected virtual void OnProtocolErrorEventReceived(object? sender, ProtocolErrorReceivedEventArgs e)
    {
        if (this.ErrorEventReceived is not null)
        {
            this.ErrorEventReceived(this, e);
        }
    }

    protected virtual void OnProtocolUnknownMessageReceived(object? sender, ProtocolUnknownMessageReceivedEventArgs e)
    {
        if (this.UnknownMessageReceived is not null)
        {
            this.UnknownMessageReceived(this, e);
        }
    }

    private void OnMessageReceived(object? sender, DataReceivedEventArgs e)
    {
        bool isProcessed = false;
        JObject message = JObject.Parse(e.Data);
        if (message.ContainsKey("id"))
        {
            JToken? idToken = message["id"];
            if (idToken is not null && idToken.Type != JTokenType.Null)
            {
                long? responseId = message["id"]!.Value<long>();
                if (this.pendingCommands.ContainsKey(responseId.Value))
                {
                    if (this.pendingCommands.TryGetValue(responseId.Value, out WebDriverBidiCommandData? executedCommand))
                    {
                        try
                        {
                            if (message.ContainsKey("result"))
                            {
                                executedCommand.Result = message["result"]!.ToObject(executedCommand.ResultType) as CommandResult;
                                isProcessed = true;
                            }
                            else if (message.ContainsKey("error"))
                            {
                                executedCommand.Result = message.ToObject<ErrorResponse>();
                                isProcessed = true;
                            }
                        }
                        catch (Exception ex)
                        {
                            executedCommand.ThrownException = ex;
                        }
                        finally
                        {
                            executedCommand.SynchronizationEvent.Set();
                        }
                    }
                }
            }
            else if (message.ContainsKey("error"))
            {
                // This is an error response, not connected to a command.
                var unexpectedError = message.ToObject<ErrorResponse>();
                isProcessed = true;
                this.OnProtocolErrorEventReceived(this, new ProtocolErrorReceivedEventArgs(unexpectedError));
            }
        }
        else if (message.ContainsKey("method") && message.ContainsKey("params"))
        {
            string? eventName = message["method"]!.Value<string>();
            JToken? eventData = message["params"];
            if (eventName is not null && eventData is not null)
            {
                if (this.eventTypes.ContainsKey(eventName))
                {
                    var eventArgs = eventData.ToObject(this.eventTypes[eventName]);
                    isProcessed = true;
                    this.OnProtocolEventReceived(this, new ProtocolEventReceivedEventArgs(eventName, eventArgs));
                }
            }
        }

        if (!isProcessed)
        {
            this.OnProtocolUnknownMessageReceived(this, new ProtocolUnknownMessageReceivedEventArgs(e.Data));
        }
    }

    private void OnConnectionLogMessage(object? sender, LogMessageEventArgs e)
    {
        this.OnLogMessage(sender, e);
    }
}