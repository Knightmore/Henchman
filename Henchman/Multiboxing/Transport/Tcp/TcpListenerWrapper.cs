using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Henchman.Features.General;
using Henchman.Multiboxing.Server;

namespace Henchman.Multiboxing.Transport.Tcp;

public sealed class TcpListenerWrapper : IConnectionListener, IDisposable
{
    private readonly TcpListener listener;

    public TcpListenerWrapper(int port)
    {
        bool useLocalOnly;
        if (TryGetFeature<MultiboxingUI>(out var multiboxing))
            useLocalOnly = multiboxing.Configuration.LocalOnly;
        else
            throw new Exception("Could not read Multiboxing configuration field useLocalOnly");

        listener = new TcpListener(useLocalOnly
                                           ? IPAddress.Loopback
                                           : IPAddress.IPv6Any, port);
        if (!useLocalOnly)
            listener.Server.DualMode = true;
        listener.Start();

        Verbose(((IPEndPoint)listener.LocalEndpoint).Port.ToString());
        Verbose($"""
                 TCP Listener bound to the following IPs:
                 {string.Join("\n", GetBoundIps(useLocalOnly))}
                 """);
    }

    public async Task<IConnection> AcceptAsync(CancellationToken token)
    {
        var client = await listener.AcceptTcpClientAsync(token);
        return new TcpConnection(client);
    }

    public void Dispose() => listener.Stop();

    private List<string> GetBoundIps(bool useLocalOnly)
    {
        if (useLocalOnly)
            return new List<string> { "127.0.0.1" };

        var usable = new List<string>();

        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up)
                continue;

            var props = ni.GetIPProperties();

            foreach (var addr in props.UnicastAddresses)
            {
                var ip = addr.Address;

                if (IPAddress.IsLoopback(ip))
                {
                    usable.Add(ip.ToString());
                    continue;
                }

                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    usable.Add(ip.ToString());
                    continue;
                }

                if (ip.AddressFamily == AddressFamily.InterNetworkV6)
                {
                    if (ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6UniqueLocal)
                        usable.Add(ip.ToString());
                }
            }
        }

        return usable;
    }
}
