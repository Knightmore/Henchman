using System.Net;
using System.Net.Sockets;
using System.Text;
using Henchman.Features.General;
using Henchman.Multiboxing.Client;
using Henchman.Multiboxing.Server;
using Henchman.Multiboxing.Transport.Tcp;

namespace Henchman.Multiboxing.Transport;

public static class TransportFactory
{
    public static IConnectionListener CreateServerListener(string featureName, int maxClients)
    {
        uint port                                                   = 5410;
        if (TryGetFeature<MultiboxingUI>(out var multiboxing)) port = multiboxing.Configuration.Port;

        ErrorThrowIf(!IsPortAvailable(port.ToInt()), $"Port {port} is already in use! Chage it in System -> Multiboxing!");

        return new TcpListenerWrapper(port.ToInt());
        //return new NamedPipeListener($"Henchman_{featureName}", maxClients);
    }

    public static IClientConnection CreateClientConnection(string featureName)
    {
        uint port = 5410;
        var  ip   = "127.0.0.1";
        if (TryGetFeature<MultiboxingUI>(out var multiboxing))
        {
            port = multiboxing.Configuration.Port;
            if (!multiboxing.Configuration.LocalOnly)
            {
                ip = Encoding.UTF8.GetString(multiboxing.Configuration.IpBytes)
                             .TrimEnd('\0')
                             .Trim();
                if (string.IsNullOrWhiteSpace(ip))
                    ip = "127.0.0.1";
            }
        }

        return new TcpClientConnection(ip, port.ToInt());
        //return new NamedPipeClientConnection($"Henchman_{featureName}");
    }

    private static bool IsPortAvailable(int port)
    {
        try
        {
            using var test = new TcpListener(IPAddress.Loopback, port);
            test.Start();
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }
}
