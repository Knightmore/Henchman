using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using Henchman.Multiboxing.Server;

namespace Henchman.Multiboxing.Transport.NamedPipe;

public sealed class NamedPipeTransport(string pipeName, int maxClients) : IConnection
{
    private readonly NamedPipeServerStream pipe = new(
                                                      pipeName,
                                                      PipeDirection.InOut,
                                                      maxClients,
                                                      PipeTransmissionMode.Message,
                                                      PipeOptions.Asynchronous);

    public string Id { get; } = Guid.NewGuid()
                                    .ToString();

    public Stream Stream => pipe;

    public Task WaitForConnectionAsync(CancellationToken token) => pipe.WaitForConnectionAsync(token);

    public void Dispose() => pipe.Dispose();
}
