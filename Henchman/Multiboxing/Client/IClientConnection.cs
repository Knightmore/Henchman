using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Henchman.Multiboxing.Client;

public interface IClientConnection
{
    Stream Stream { get; }
    Task   ConnectAsync(CancellationToken token);
    void   Dispose();
}
