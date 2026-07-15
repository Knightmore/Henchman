using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Henchman.Multiboxing.Server;

public interface IConnection
{
    Stream Stream { get; }
    string Id     { get; }
    Task   WaitForConnectionAsync(CancellationToken token);
    void   Dispose();
}
