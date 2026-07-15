using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Henchman.Multiboxing.Server;

namespace Henchman.Multiboxing.Transport.Tcp;

public sealed class TcpConnection(TcpClient client) : IConnection, IDisposable
{
    private readonly NetworkStream _stream = client.GetStream();

    public string Id { get; } = Guid.NewGuid()
                                    .ToString();

    public Stream Stream => _stream;

    public Task WaitForConnectionAsync(CancellationToken token) => Task.CompletedTask;

    public void Dispose()
    {
        _stream.Dispose();
        client.Close();
        client.Dispose();
    }
}
