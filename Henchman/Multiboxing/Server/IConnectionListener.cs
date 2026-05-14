using System.Threading;
using System.Threading.Tasks;

namespace Henchman.Multiboxing.Server;

public interface IConnectionListener
{
    Task<IConnection> AcceptAsync(CancellationToken token);
    void              Dispose();
}
