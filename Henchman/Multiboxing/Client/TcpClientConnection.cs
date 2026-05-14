using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Henchman.Multiboxing.Client;

public sealed class TcpClientConnection(string host, int port) : IClientConnection
{
    private TcpClient client;

    public Stream Stream => client.GetStream();

    public async Task ConnectAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                client = new TcpClient();
                await using var reg = token.Register(() =>
                                                     {
                                                         try
                                                         {
                                                             Stream.Dispose();
                                                             client.Dispose();
                                                         }
                                                         catch (Exception ex)
                                                         {
                                                             InternalTaskError($"Could not close TCP socket: {ex}");
                                                         }
                                                     });

                await client.ConnectAsync(host, port, token);
                client.NoDelay = true;
                return;
            }
            catch
            {
                await Task.Delay(GeneralDelayMs, token);
            }
        }

        token.ThrowIfCancellationRequested();
    }

    public void Dispose() => client?.Dispose();
}
