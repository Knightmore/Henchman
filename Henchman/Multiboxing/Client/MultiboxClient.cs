using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Henchman.Multiboxing.Command;
using Henchman.Multiboxing.Transport;
using Henchman.TaskManager;

namespace Henchman.Multiboxing.Client;

public class MultiboxClient(
        IClientConnection                                                     connection,
        Func<Stream, Channel<(CommandType, string)>, CancellationToken, Task> sessionHandler,
        List<MultiboxClient.CharacterData>?                                   characters = null,
        CancellationToken                                                     token      = default)
{
    private readonly List<CharacterData> characters = characters ?? new List<CharacterData>();

    public async Task StartAsync()
    {
        using var scope = new TaskDescriptionScope("Running MultiBox Client");

        MessageHandler.receivedMessages = 0;
        MessageHandler.sentMessages     = 0;

        var incomingChannel = Channel.CreateUnbounded<(CommandType type, string data)>();

        await connection.ConnectAsync(token);
        TaskLog("Connected to server.");

        var stream = connection.Stream;

        var messagingCts   = new CancellationTokenSource();
        var messagingToken = messagingCts.Token;
        var linkedCts      = CancellationTokenSource.CreateLinkedTokenSource(token, messagingToken);
        var linkedToken    = linkedCts.Token;

        try
        {
            _ = Task.Run(async () =>
                         {
                             try
                             {
                                 VerboseSpecific("MessageReader", "Client message queue started!");

                                 while (!linkedToken.IsCancellationRequested)
                                 {
                                     var incoming = await MessageHandler.ReadMessageAsync(stream, linkedToken);
                                     if (incoming != null)
                                     {
                                         await incomingChannel.Writer.WriteAsync(
                                                                                 (incoming.Value.header, incoming.Value.json),
                                                                                 linkedToken);
                                     }
                                     else
                                         return;
                                 }
                             }
                             catch (OperationCanceledException)
                             {
                                 VerboseSpecific("MessageReader", "Client message queue canceled.");
                             }
                             catch (Exception ex)
                             {
                                 VerboseSpecific("MessageReader", $"Client message queue failed: {ex}");
                             }
                             finally
                             {
                                 incomingChannel.Writer.TryComplete();
                             }
                         }, linkedToken);

            try
            {
                while (!token.IsCancellationRequested)
                {
                    var message = await incomingChannel.Reader.ReadAsync(token);

                    if (message.type == CommandType.ServerRequest)
                    {
                        var generalCommand = JsonSerializer.Deserialize<string>(message.data);

                        if (Enum.TryParse<ServerRequest>(generalCommand, out var result))
                        {
                            switch (result)
                            {
                                case ServerRequest.ServerFull:
                                    Debug("Server full. Disconnecting client.");
                                    return;
                                case ServerRequest.Disconnect:
                                    Debug("Server requested disconnect.");
                                    return;
                                case ServerRequest.Turn:
                                {
                                    ErrorThrowIf(characters.Count == 0,
                                                 "Multiboxing RoundRobin mode requires a character list!");

                                    if (characters.TryGetFirst(out var nextCharacter))
                                    {
                                        messagingCts   = new CancellationTokenSource();
                                        messagingToken = messagingCts.Token;
                                        linkedCts      = CancellationTokenSource.CreateLinkedTokenSource(token, messagingToken);
                                        linkedToken    = linkedCts.Token;

                                        await MessageHandler.WriteMessageAsync(
                                                                               stream,
                                                                               CommandType.RoundRobinResponse,
                                                                               RoundRobinResponse.Available.ToJson(),
                                                                               token);

                                        if (Lifestream.IsBusy())
                                        {
                                            await WaitUntilAsync(() => !Lifestream.IsBusy(),
                                                                 "Waiting until Lifestream is done",
                                                                 token);
                                        }

                                        await Lifestream.SwitchToChar(nextCharacter.Name, nextCharacter.World, token);

                                        await sessionHandler.Invoke(stream, incomingChannel, token);

                                        characters.RemoveAt(0);

                                        if (characters.Count > 0)
                                        {
                                            await MessageHandler.WriteMessageAsync(
                                                                                   stream,
                                                                                   CommandType.RoundRobinResponse,
                                                                                   RoundRobinResponse.TurnDone.ToJson(),
                                                                                   token);
                                        }
                                        else
                                        {
                                            await MessageHandler.WriteMessageAsync(
                                                                                   stream,
                                                                                   CommandType.RoundRobinResponse,
                                                                                   RoundRobinResponse.Finished.ToJson(),
                                                                                   token);

                                            return;
                                        }
                                    }
                                    else
                                    {
                                        await MessageHandler.WriteMessageAsync(
                                                                               stream,
                                                                               CommandType.RoundRobinResponse,
                                                                               RoundRobinResponse.Finished.ToJson(),
                                                                               token);

                                        return;
                                    }

                                    break;
                                }
                                case ServerRequest.StartParallel:
                                {
                                    await sessionHandler.Invoke(stream, incomingChannel, token);
                                    return;
                                }
                            }
                        }
                    }
                }
            }
            catch (ChannelClosedException)
            {
                VerboseSpecific("MessageReader", "Client message queue closed.");
            }
        } finally
        {
            messagingCts.Cancel();
            messagingCts.Dispose();
            linkedCts.Dispose();
            connection.Dispose();
        }
    }

    public class CharacterData(string name, string world)
    {
        public string Name  = name;
        public string World = world;
    }
}
