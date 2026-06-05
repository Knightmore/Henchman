using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Henchman.Multiboxing.Command;
using Henchman.Multiboxing.Transport;
using Henchman.TaskManager;

namespace Henchman.Multiboxing.Server;

public class MultiboxServer
{
    internal readonly List<ClientSession>                                                      clients       = [];
    private readonly  Channel<(ClientSession session, CommandType type, string data)>          globalChannel = Channel.CreateUnbounded<(ClientSession, CommandType, string)>();
    private readonly  IConnectionListener                                                      listener;
    private readonly  int                                                                      maxClients;
    private readonly  Func<ClientSession, CommandType, string, CancellationToken, Task<bool>>? parallelSessionHandler;
    private readonly  Func<ClientSession, CancellationToken, Task>?                            roundRobinSessionHandler;
    private readonly  SemaphoreSlim                                                            semaphore = new(1, 1);

    private readonly CancellationToken token;


    private         int  currentIndex;
    public volatile bool StartRequested = false;

    public MultiboxServer(
            string                                       featureName,
            int                                          maxClients,
            Func<ClientSession, CancellationToken, Task> sessionHandler,
            IConnectionListener                          listener,
            CancellationToken                            token)
    {
        this.maxClients          = maxClients;
        roundRobinSessionHandler = sessionHandler;
        this.token               = token;
        this.listener            = listener;

        MessageHandler.receivedMessages = 0;
        MessageHandler.sentMessages     = 0;
    }

    public MultiboxServer(
            string                                                                  featureName,
            int                                                                     maxClients,
            Func<ClientSession, CommandType, string, CancellationToken, Task<bool>> sessionHandler,
            IConnectionListener                                                     listener,
            CancellationToken                                                       token)
    {
        this.maxClients        = maxClients;
        parallelSessionHandler = sessionHandler;
        this.token             = token;
        this.listener          = listener;

        MessageHandler.receivedMessages = 0;
        MessageHandler.sentMessages     = 0;
    }

    /// <param name="preCheck">Connected client amount as function input</param>
    /// <returns></returns>
    public async Task StartParallelAsync(Func<int, bool>? preCheck = null)
    {
        var       keepRunning = true;
        using var scope       = new TaskDescriptionScope("Running MultiBox Server - Parallel");

        _ = Task.Run(() => AcceptLoopAsync(true, token), token);

        while (!StartRequested)
        {
            token.ThrowIfCancellationRequested();
            await Task.Delay(GeneralDelayMs, token);
        }

        if (clients.Count == 0 || (preCheck != null && !preCheck!.Invoke(clients.Count)))
        {
            await DisconnectAllAsync("Parallel pre-check failed.", token);
            Dispose();
            return;
        }


        await Broadcast(CommandType.ServerRequest, ServerRequest.StartParallel.ToJson(), token);

        while (!token.IsCancellationRequested || keepRunning)
        {
            var (session, type, data) = await globalChannel.Reader.ReadAsync(token);

            ErrorThrowIf(parallelSessionHandler == null, "SessionHandler for parallel multiboxing was not set!");
            keepRunning = await parallelSessionHandler!.Invoke(session, type, data, token);
        }

        await DisconnectAllAsync("Parallel server finished.", token);
        Dispose();
    }


    public async Task StartRoundRobinAsync()
    {
        using var scope = new TaskDescriptionScope("Running MultiBox Server - RoundRobin");

        var acceptingCts   = new CancellationTokenSource();
        var acceptingToken = acceptingCts.Token;
        var linkedCts      = CancellationTokenSource.CreateLinkedTokenSource(token, acceptingToken);
        var linkedToken    = linkedCts.Token;

        try
        {
            _ = Task.Run(() => AcceptLoopAsync(false, linkedToken), token);

            while (!token.IsCancellationRequested)
            {
                ClientSession client = null;

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
                    VerboseSpecific("MultiboxServer", $"Starting turn for client {client.Id}");

                    var turnResult = await ProcessTurnAsync(client, token);

                    if (!turnResult.KeepClient)
                        await DisconnectClientAsync(client, turnResult.DisconnectReason ?? "Turn processing finished or failed.", token);
                }
                else
                    await Task.Delay(50, token);
            }
        }
        catch (OperationCanceledException)
        {
            VerboseSpecific("MultiboxServer", "Round-robin server canceled.");
            acceptingCts.Cancel();
        }
        catch (Exception ex)
        {
            VerboseSpecific("MultiboxServer", $"Round-robin server failed: {ex}");
            acceptingCts.Cancel();
        }
    }

    private async Task AcceptLoopAsync(bool useParallelMode, CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                var connection = await listener.AcceptAsync(token);

                await semaphore.WaitAsync(token);
                try
                {
                    if (clients.Count >= maxClients)
                    {
                        await MessageHandler.WriteMessageAsync(
                                                               connection.Stream,
                                                               CommandType.ServerRequest,
                                                               ServerRequest.ServerFull.ToJson(),
                                                               token);

                        connection.Dispose();
                        continue;
                    }
                } finally
                {
                    semaphore.Release();
                }

                var messagingCts = new CancellationTokenSource();
                var linkedCts    = CancellationTokenSource.CreateLinkedTokenSource(token, messagingCts.Token);
                var linkedToken  = linkedCts.Token;

                var messageChannel    = Channel.CreateUnbounded<(CommandType type, string data)>();
                var roundRobinChannel = Channel.CreateUnbounded<string>();

                var session = new ClientSession(
                                                connection,
                                                messageChannel,
                                                roundRobinChannel,
                                                null!,
                                                messagingCts,
                                                connection.Id
                                               );

                VerboseSpecific("MultiboxServer", $"Accepted client {session.Id}");

                await semaphore.WaitAsync(token);
                try
                {
                    clients.Add(session);
                } finally
                {
                    semaphore.Release();
                }

                var handlerTask = Task.Run(async () =>
                                           {
                                               try
                                               {
                                                   while (!linkedToken.IsCancellationRequested)
                                                   {
                                                       var msg = await MessageHandler.ReadMessageAsync(connection.Stream, linkedToken);

                                                       if (msg != null)
                                                       {
                                                           if (useParallelMode)
                                                               await globalChannel.Writer.WriteAsync((session, msg.Value.header, msg.Value.json), linkedToken);
                                                           else if (msg.Value.header == CommandType.RoundRobinResponse)
                                                               await roundRobinChannel.Writer.WriteAsync(msg.Value.json, linkedToken);
                                                           else
                                                               await messageChannel.Writer.WriteAsync((msg.Value.header, msg.Value.json), linkedToken);
                                                       }
                                                       else
                                                           return;
                                                   }
                                               }
                                               catch (OperationCanceledException)
                                               {
                                                   VerboseSpecific("MultiboxServer", $"Message handler canceled for client {session.Id}");
                                               }
                                               catch (Exception ex)
                                               {
                                                   VerboseSpecific("MultiboxServer", $"Message handler failed for client {session.Id}: {ex}");
                                               } finally
                                               {
                                                   messageChannel.Writer.TryComplete();
                                                   roundRobinChannel.Writer.TryComplete();
                                                   RemoveClient(session);
                                               }
                                           }, linkedToken);

                session = session with { MessageHandler = handlerTask };

                await semaphore.WaitAsync(token);
                try
                {
                    var index = clients.FindIndex(x => x.Id == session.Id);
                    if (index != -1)
                        clients[index] = session;
                } finally
                {
                    semaphore.Release();
                }
            }
        }
        catch (OperationCanceledException)
        {
            VerboseSpecific("MultiboxServer", "Accept loop canceled.");
        }
        catch (Exception ex)
        {
            VerboseSpecific("MultiboxServer", $"Accept loop failed: {ex}");
            Dispose();
        }
    }

    private async Task<TurnResult> ProcessTurnAsync(ClientSession client, CancellationToken token)
    {
        using var scope = new TaskDescriptionScope("Processing Turns");

        try
        {
            await MessageHandler.WriteMessageAsync(
                                                   client.Pipe,
                                                   CommandType.ServerRequest,
                                                   ServerRequest.Turn.ToJson(),
                                                   token);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            cts.CancelAfter(TimeSpan.FromSeconds(10));

            var answer = await client.RoundRobinChannel.Reader.ReadAsync(cts.Token);

            var response = JsonSerializer.Deserialize<string>(answer);

            if (!Enum.TryParse(response, out RoundRobinResponse result))
            {
                VerboseSpecific("MultiboxServer", $"Client {client.Id} sent invalid round-robin response: {answer}");
                return new TurnResult(false, "Client sent an invalid round-robin response.");
            }

            VerboseSpecific("MultiboxServer", $"Client {client.Id} responded: {result}");

            switch (result)
            {
                case RoundRobinResponse.Available:
                {
                    ErrorThrowIf(roundRobinSessionHandler == null, "SessionHandler for roundrobin multiboxing was not set!");
                    await roundRobinSessionHandler!.Invoke(client, token);

                    var keepRunning = false;

                    await foreach (var message in client.RoundRobinChannel.Reader.ReadAllAsync(token))
                    {
                        var r = message.FromJsonEnum<RoundRobinResponse>();

                        if (r == RoundRobinResponse.TurnDone)
                        {
                            keepRunning = true;
                            VerboseSpecific("MultiboxServer", $"Turn done for client {client.Id}");
                            break;
                        }

                        if (r == RoundRobinResponse.Finished)
                        {
                            VerboseSpecific("MultiboxServer", $"Client {client.Id} reported no remaining characters.");
                            break;
                        }
                    }

                    return keepRunning
                                   ? new TurnResult(true)
                                   : new TurnResult(false, "Client reported no remaining characters.");
                }

                case RoundRobinResponse.Finished:
                    return new TurnResult(false, "Client reported no remaining characters.");
            }

            return new TurnResult(true);
        }
        catch (OperationCanceledException ex)
        {
            VerboseSpecific("MultiboxServer", $"Turn processing canceled for client {client.Id}: {ex.Message}");
            return new TurnResult(false, "Turn processing canceled.");
        }
        catch (ChannelClosedException ex)
        {
            VerboseSpecific("MultiboxServer", $"Message channel closed for client {client.Id}: {ex.Message}");
            return new TurnResult(false, "Client message channel closed.");
        }
        catch (Exception ex)
        {
            VerboseSpecific("MultiboxServer", $"Turn processing failed for client {client.Id}: {ex}");
            return new TurnResult(false, "Turn processing failed.");
        }
    }

    internal void RemoveClient(ClientSession c)
    {
        VerboseSpecific("MultiboxServer", $"Removing client {c.Id}");

        semaphore.Wait();
        try
        {
            var index = clients.FindIndex(x => x.Id == c.Id);
            if (index == -1)
                return;

            clients.RemoveAt(index);

            if (currentIndex > index)
                currentIndex--;

            if (currentIndex < 0)
                currentIndex = 0;
        } finally
        {
            semaphore.Release();
        }

        c.HandlerTokenSource.Cancel();
        c.HandlerTokenSource.Dispose();
        c.Connection.Dispose();
    }

    private async Task DisconnectClientAsync(
            ClientSession     client,
            string            reason,
            CancellationToken token = default)
    {
        VerboseSpecific("MultiboxServer", $"Disconnecting client {client.Id}: {reason}");

        try
        {
            await MessageHandler.WriteMessageAsync(
                                                   client.Pipe,
                                                   CommandType.ServerRequest,
                                                   ServerRequest.Disconnect.ToJson(),
                                                   token);
        }
        catch (OperationCanceledException)
        {
            VerboseSpecific("MultiboxServer", $"Disconnect message canceled for client {client.Id}");
        }
        catch (Exception ex)
        {
            VerboseSpecific("MultiboxServer", $"Could not send disconnect to client {client.Id}: {ex.Message}");
        } finally
        {
            RemoveClient(client);
        }
    }

    private async Task DisconnectAllAsync(string reason, CancellationToken token = default)
    {
        foreach (var client in GetClientSnapshot())
            await DisconnectClientAsync(client, reason, token);
    }


    public void Disconnect() => GetClientSnapshot()
                                       .ForEach(x => RemoveClient(x));

    public void Kick(ClientSession client) => RemoveClient(client);

    public void Dispose()
    {
        listener.Dispose();
    }

    public Task SendToClient(
            ClientSession     client,
            CommandType       type,
            string            data,
            CancellationToken token = default) => MessageHandler.WriteMessageAsync(client.Pipe, type, data, token);

    public Task Broadcast(
            CommandType       type,
            string            data,
            CancellationToken token = default)
    {
        var tasks = GetClientSnapshot()
                   .Select(c =>
                                            MessageHandler.WriteMessageAsync(c.Pipe, type, data, token));

        return Task.WhenAll(tasks);
    }

    private List<ClientSession> GetClientSnapshot()
    {
        semaphore.Wait();
        try
        {
            return clients.ToList();
        } finally
        {
            semaphore.Release();
        }
    }


    public record ClientSession(
            IConnection                              Connection,
            Channel<(CommandType type, string data)> MessageChannel,
            Channel<string>                          RoundRobinChannel,
            Task                                     MessageHandler,
            CancellationTokenSource                  HandlerTokenSource,
            string                                   Id,
            PartyMemberType                          MemberType = PartyMemberType.None)
    {
        public Stream Pipe => Connection.Stream;
    }

    private record TurnResult(bool KeepClient, string? DisconnectReason = null);
}
