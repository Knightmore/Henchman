using System.Threading;
using System.Threading.Tasks;
using Henchman.Multiboxing.Server;

namespace Henchman.Multiboxing.Transport.NamedPipe;

public sealed class NamedPipeListener(string pipeName, int maxClients) : IConnectionListener
{
    public async Task<IConnection> AcceptAsync(CancellationToken token)
    {
        var conn = new NamedPipeTransport(pipeName, maxClients);
        await conn.WaitForConnectionAsync(token);
        return conn;
    }

    public void Dispose() { }
}
