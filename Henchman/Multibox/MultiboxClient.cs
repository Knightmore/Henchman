using System.IO.Pipes;
using System.Security.Principal;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Henchman.Helpers;
using Henchman.Multibox.Command;
using Henchman.TaskManager;

namespace Henchman.Multibox;

public class MultiboxClient(string featureName, Func<PipeStream, Channel<(CommandType, string)>, CancellationToken, Task> sessionHandler, List<MultiboxClient.CharacterData>? characters = null, CancellationToken token = default)
{
    private readonly string pipeName = $"Henchman_{featureName}";

    public async Task StartAsync()
    {
        using var scope = new TaskDescriptionScope("Running MultiBox Client");
        MessageHandler.receivedMessages = 0;
        MessageHandler.sentMessages     = 0;
        var incomingChannel = Channel.CreateUnbounded<(CommandType type, string data)>();
        await using var pipe = new NamedPipeClientStream(".", pipeName,
                                                         PipeDirection.InOut,
                                                         PipeOptions.Asynchronous,
                                                         TokenImpersonationLevel.Impersonation);

        await pipe.ConnectAsync(token);
        Log("Connected to server.");
        pipe.ReadMode = PipeTransmissionMode.Message;

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
                                     var incomingMessage = await MessageHandler.ReadMessageAsync(pipe, linkedToken);
                                     if (incomingMessage != null)
                                     {
                                         VerboseSpecific("IncomingQueue", "Adding message to queue");
                                         await incomingChannel.Writer.WriteAsync((incomingMessage.Value.header, incomingMessage.Value.json), linkedToken);
                                     }
                                     else
                                         return;
                                 }
                             }
                             catch (Exception ex)
                             {
                                 VerboseSpecific("IncomingQueue", "Client message queue stopped!");
                                 VerboseSpecific("MessageReader", $"Cancellation status — token: {token.IsCancellationRequested}, messagingToken: {messagingToken.IsCancellationRequested}");
                             }
                         }, linkedToken);
            while (!token.IsCancellationRequested)
            {
                var message = await incomingChannel.Reader.ReadAsync(token);
                Verbose("Processing message from queue.");

                if (message.type == CommandType.ServerRequest)
                {
                    var generalCommand = JsonSerializer.Deserialize<string>(message.data);
                    Verbose($"GeneralCommand: {generalCommand}");
                    if (Enum.TryParse<ServerRequest>(generalCommand, out var result))
                    {
                        if (result == ServerRequest.Turn)
                        {
                            ErrorThrowIf(characters == null, "Multiboxing RoundRobin mode can not be used without passing a character list!");
                            Log("Received Turn command. Progressing with sessionHandler!");
                            if (characters!.TryGetFirst(out var nextCharacter))
                            {
                                messagingCts   = new CancellationTokenSource();
                                messagingToken = messagingCts.Token;
                                linkedCts      = CancellationTokenSource.CreateLinkedTokenSource(token, messagingToken);
                                linkedToken    = linkedCts.Token;
                                await MessageHandler.WriteMessageAsync(pipe, CommandType.RoundRobinResponse, RoundRobinResponse.Available.ToJson(), token);

                                if (Lifestream.IsBusy())
                                    await WaitUntilAsync(() => !Lifestream.IsBusy(), "Waiting until Lifestream is done", token);
                                await Lifestream.SwitchToChar(nextCharacter.Name, nextCharacter.World, token);
                                await sessionHandler.Invoke(pipe, incomingChannel, token);
                                characters!.RemoveAt(0);
                                Verbose($"Left characters to trade on this account {characters.Count}");
                                if (characters.Count > 0)
                                {
                                    Verbose(RoundRobinResponse.TurnDone.ToJson());
                                    await MessageHandler.WriteMessageAsync(pipe, CommandType.RoundRobinResponse, RoundRobinResponse.TurnDone.ToJson(), token);
                                }
                                else
                                {
                                    Verbose(RoundRobinResponse.Finished.ToJson());
                                    await MessageHandler.WriteMessageAsync(pipe, CommandType.RoundRobinResponse, RoundRobinResponse.Finished.ToJson(), token);
                                    messagingCts.Cancel();
                                    Verbose("CANCEL CALLED ON messagingCts");
                                    messagingCts.Dispose();
                                    linkedCts.Dispose();
                                    return;
                                }
                            }
                            else
                            {
                                Verbose(RoundRobinResponse.Finished.ToJson());
                                await MessageHandler.WriteMessageAsync(pipe, CommandType.RoundRobinResponse, RoundRobinResponse.Finished.ToJson(), token);
                                messagingCts.Cancel();
                                Verbose("CANCEL CALLED ON messagingCts");
                                messagingCts.Dispose();
                                linkedCts.Dispose();
                                return;
                            }
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            Verbose($"Cancellation status — token: {token.IsCancellationRequested}, messagingToken: {messagingToken.IsCancellationRequested}");
            Verbose("Tradeloop stopped!");
            messagingCts.Cancel();
            messagingCts.Dispose();
            linkedCts.Dispose();
        }
        catch (Exception ex)
        {
            Verbose($"Cancellation status — token: {token.IsCancellationRequested}, messagingToken: {messagingToken.IsCancellationRequested}");
            InternalError($"Error with server {pipeName}: {ex.Message}");
            messagingCts.Cancel();
            messagingCts.Dispose();
            linkedCts.Dispose();
        }
    }

    public class CharacterData(string name, string world)
    {
        public string Name  = name;
        public string World = world;
    }
}
