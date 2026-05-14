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
            Disconnect();
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

        Disconnect();
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
                    var keepClient = await ProcessTurnAsync(client, token);

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
                    }
                }
                else
                    await Task.Delay(50, token);
            }
        }
        catch
        {
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

                var messageChannel = Channel.CreateUnbounded<(CommandType type, string data)>();

                var session = new ClientSession(
                                                connection,
                                                messageChannel,
                                                null!,
                                                messagingCts,
                                                connection.Id
                                               );

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
                                                           else
                                                               await messageChannel.Writer.WriteAsync((msg.Value.header, msg.Value.json), linkedToken);
                                                       }
                                                       else
                                                           return;
                                                   }
                                               } finally
                                               {
                                                   RemoveClient(session);
                                               }
                                           }, linkedToken);

                session = session with { MessageHandler = handlerTask };
            }
        }
        catch
        {
            Dispose();
        }
    }

    private async Task<bool> ProcessTurnAsync(ClientSession client, CancellationToken token)
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

            var answer = await client.MessageChannel.Reader.ReadAsync(cts.Token);

            if (answer.type != CommandType.RoundRobinResponse)
                return false;

            var response = JsonSerializer.Deserialize<string>(answer.data);

            if (!Enum.TryParse(response, out RoundRobinResponse result))
                return false;

            switch (result)
            {
                case RoundRobinResponse.Available:
                {
                    ErrorThrowIf(roundRobinSessionHandler == null, "SessionHandler for roundrobin multiboxing was not set!");
                    await roundRobinSessionHandler!.Invoke(client, token);

                    var keepRunning = false;

                    await foreach (var message in client.MessageChannel.Reader.ReadAllAsync(token))
                    {
                        if (message.type == CommandType.RoundRobinResponse)
                        {
                            var r = message.data.FromJsonEnum<RoundRobinResponse>();

                            if (r == RoundRobinResponse.TurnDone)
                            {
                                keepRunning = true;
                                break;
                            }

                            if (r == RoundRobinResponse.Finished)
                                break;
                        }
                    }

                    return keepRunning;
                }

                case RoundRobinResponse.Finished:
                    return false;
            }

            return true;
        }
        catch
        {
            client.HandlerTokenSource.Cancel();
            client.HandlerTokenSource.Dispose();
            return false;
        }
    }

    internal void RemoveClient(ClientSession c)
    {
        semaphore.Wait();
        try
        {
            var index = clients.IndexOf(c);
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


    public void Disconnect() => clients.ToList()
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
        List<ClientSession> snapshot;
        lock (clients)
            snapshot = clients.ToList();

        var tasks = snapshot.Select(c =>
                                            MessageHandler.WriteMessageAsync(c.Pipe, type, data, token));

        return Task.WhenAll(tasks);
    }


    public record ClientSession(
            IConnection                              Connection,
            Channel<(CommandType type, string data)> MessageChannel,
            Task                                     MessageHandler,
            CancellationTokenSource                  HandlerTokenSource,
            string                                   Id,
            PartyMemberType                          MemberType = PartyMemberType.None)
    {
        public Stream Pipe => Connection.Stream;
    }
}
