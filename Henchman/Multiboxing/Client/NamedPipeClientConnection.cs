using System.IO;
using System.IO.Pipes;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;

namespace Henchman.Multiboxing.Client;

public sealed class NamedPipeClientConnection(string pipeName) : IClientConnection
{
    private readonly NamedPipeClientStream pipe = new(
                                                      ".",
                                                      pipeName,
                                                      PipeDirection.InOut,
                                                      PipeOptions.Asynchronous,
                                                      TokenImpersonationLevel.Impersonation);

    private readonly string pipeName = pipeName;

    public Stream Stream => pipe;

    public async Task ConnectAsync(CancellationToken token)
    {
        await pipe.ConnectAsync(token);
        pipe.ReadMode = PipeTransmissionMode.Message;
    }

    public void Dispose() => pipe.Dispose();
}
