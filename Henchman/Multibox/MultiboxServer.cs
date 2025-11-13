using System.IO.Pipes;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Henchman.Helpers;
using Henchman.Multibox.Command;
using Henchman.TaskManager;

namespace Henchman.Multibox;

public class MultiboxServer
{
    private readonly List<ClientSession> clients = [];
    private readonly string              featureName;
    private readonly int                 maxClients;
    private readonly string?             pipeName;

    private readonly SemaphoreSlim semaphore = new(1, 1);

    private readonly Func<ClientSession, CancellationToken, Task> sessionHandler;
    private readonly CancellationToken                            token;
    private          int                                          currentIndex;

    public MultiboxServer(string featureName, int maxClients, Func<ClientSession, CancellationToken, Task> sessionHandler, CancellationToken token)
    {
        this.featureName                = featureName;
        this.maxClients                 = maxClients;
        this.sessionHandler             = sessionHandler;
        this.token                      = token;
        pipeName                        = $"Henchman_{this.featureName}";
        MessageHandler.receivedMessages = 0;
        MessageHandler.sentMessages     = 0;
    }

    /*
     * Round Robin Tasks
     */
    public async Task StartRoundRobinAsync()
    {
        using var scope          = new TaskDescriptionScope("Running MultiBox Server - RoundRobin");
        var       acceptingCts   = new CancellationTokenSource();
        var       acceptingToken = acceptingCts.Token;
        var       linkedCts      = CancellationTokenSource.CreateLinkedTokenSource(token, acceptingToken);
        var       linkedToken    = linkedCts.Token;
        try
        {
            _ = Task.Run(() => AcceptLoopAsync(linkedToken), token);

            while (!token.IsCancellationRequested)
            {
                ClientSession? client = null;

                await semaphore.WaitAsync(token);
                try
                {
                    if (clients.Count > 0)
                    {
                        if (currentIndex >= clients.Count)
                            currentIndex = 0;
                        client = clients[currentIndex];
                        currentIndex++;
                    }
                } finally
                {
                    semaphore.Release();
                }

                if (client != null)
                {
                    Verbose($"Begin Processing Turn for client {client.Id}");
                    var keepClient = await ProcessTurnAsync(client, token);
                    Verbose($"Keep Client {client.Id} running: {keepClient}");
                    if (!keepClient)
                    {
                        await semaphore.WaitAsync(token);
                        try
                        {
                            clients.Remove(client);
                        } finally
                        {
                            semaphore.Release();
                        }

                        client.Pipe.Dispose();
                        Log($"Disconnected {client.Id}");
                    }
                }
                else
                    await Task.Delay(GeneralDelayMs, token);
            }
        }
        catch (Exception e)
        {
            acceptingCts.Cancel();
            Verbose("RoundRobin stopped!");
        }
    }

    private async Task AcceptLoopAsync(CancellationToken token)
    {
        NamedPipeServerStream? pipe = null;

        try
        {
            while (!token.IsCancellationRequested)
            {
                var shouldWait = false;

                await semaphore.WaitAsync(token);
                try
                {
                    if (clients.Count >= maxClients) shouldWait = true;
                } finally
                {
                    semaphore.Release();
                }

                if (shouldWait)
                {
                    Verbose("Max amount of clients connected. Waiting...");
                    await Task.Delay(GeneralDelayMs * 2, token);
                    continue;
                }

                pipe = new NamedPipeServerStream(pipeName,
                                                 PipeDirection.InOut,
                                                 maxClients,
                                                 PipeTransmissionMode.Message,
                                                 PipeOptions.Asynchronous);

                Verbose("Pipe created. Waiting for Client to connect.");
                await pipe.WaitForConnectionAsync(token);

                var messagingCts   = new CancellationTokenSource();
                var messagingToken = messagingCts.Token;
                var linkedCts      = CancellationTokenSource.CreateLinkedTokenSource(token, messagingToken);
                var linkedToken    = linkedCts.Token;

                var messageChannel = Channel.CreateUnbounded<(CommandType type, string data)>();

                var handlerPipe = pipe;
                var messageHandler = Task.Run(async () =>
                                              {
                                                  try
                                                  {
                                                      VerboseSpecific("IncomingQueue", "Server message queue started!");
                                                      while (!linkedToken.IsCancellationRequested)
                                                      {
                                                          var message = await MessageHandler.ReadMessageAsync(handlerPipe, linkedToken);
                                                          VerboseSpecific("IncomingQueue", "Adding message to queue");
                                                          if (message != null)
                                                              await messageChannel.Writer.WriteAsync((message.Value.header, message.Value.json), linkedToken);
                                                          else
                                                              return;
                                                      }
                                                  }
                                                  catch (Exception ex)
                                                  {
                                                      VerboseSpecific("IncomingQueue", "Server message queue stopped!");
                                                      VerboseSpecific("MessageReader", $"Cancellation status — token: {token.IsCancellationRequested}, messagingToken: {messagingToken.IsCancellationRequested}");
                                                  }
                                              }, linkedToken);

                var session = new ClientSession(pipe, messageChannel, messageHandler, messagingCts, Guid.NewGuid()
                                                                                                        .ToString());

                await semaphore.WaitAsync(token);
                try
                {
                    clients.Add(session);
                } finally
                {
                    semaphore.Release();
                }

                pipe = null;
                Log($"Client {session.Id} connected");
            }
        }
        catch (OperationCanceledException)
        {
            Verbose("Accept Task stopped!");
            pipe?.Dispose();
            Disconnect();
            Dispose();
        }
        catch (Exception ex)
        {
            InternalError($"Error with server {pipeName}: {ex.Message}");
            pipe?.Dispose();
            Disconnect();
            Dispose();
        }
    }


    private async Task<bool> ProcessTurnAsync(ClientSession client, CancellationToken token)
    {
        using var scope = new TaskDescriptionScope("Processing Turns");
        try
        {
            await MessageHandler.WriteMessageAsync(client.Pipe, CommandType.ServerRequest, ServerRequest.Turn.ToJson(), token);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            cts.CancelAfter(TimeSpan.FromSeconds(10));

            var answer = await client.MessageChannel.Reader.ReadAsync(cts.Token);
            Verbose("Processing message from queue.");

            if (answer.type == CommandType.RoundRobinResponse)
            {
                var response = JsonSerializer.Deserialize<string>(answer.data);
                if (Enum.TryParse<RoundRobinResponse>(response, out var result))
                {
                    switch (result)
                    {
                        case RoundRobinResponse.Available:
                        {
                            await sessionHandler.Invoke(client, token);
                            var keepRunning = false;

                            await foreach (var message in client.MessageChannel.Reader.ReadAllAsync(token))
                            {
                                if (message.type == CommandType.RoundRobinResponse)
                                {
                                    var responseData = message.data.FromJsonEnum<RoundRobinResponse>();
                                    if (responseData == RoundRobinResponse.TurnDone)
                                    {
                                        keepRunning = true;
                                        break;
                                    }

                                    if (responseData == RoundRobinResponse.Finished) break;
                                }
                            }

                            if (!keepRunning) return false;
                            break;
                        }
                        case RoundRobinResponse.Finished:
                            return false;
                    }
                }
                else
                    return false;
            }
            else
            {
                InternalError($"Received message has no valid CommandType for processing turns. ({answer.type})");
                return false;
            }

            return true;
        }
        catch (OperationCanceledException ex)
        {
            client.HandlerTokenSource.Cancel();
            Verbose("CANCEL CALLED ON messagingCts");
            client.HandlerTokenSource.Dispose();
            Verbose("Current server turn stopped!");
            InternalError(ex.ToString());
            InternalError(ex.StackTrace);
            return false;
        }
        catch (Exception ex)
        {
            InternalError($"Error with client {client.Id}: {ex.Message}");
            InternalError($"""
                           StackTrace:
                           {ex.StackTrace}
                           """);
            client.Pipe.Dispose();
            client.HandlerTokenSource.Cancel();
            Verbose("CANCEL CALLED ON messagingCts");
            client.HandlerTokenSource.Dispose();
            return false;
        }
    }

    public void Disconnect() => clients.ForEach(x => x.Pipe.Disconnect());
    public void Dispose()    => clients.ForEach(x => x.Pipe.Dispose());

    public record ClientSession(NamedPipeServerStream Pipe, Channel<(CommandType type, string data)> MessageChannel, Task MessageHandler, CancellationTokenSource HandlerTokenSource, string Id);
}
