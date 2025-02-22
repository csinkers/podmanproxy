using System.Threading;
using System.Threading.Tasks;

namespace NetProxy;

public interface IProxy
{
    Task Start(
        string remoteServerHostNameOrAddress,
        ushort remoteServerPort,
        ushort localPort,
        string? localIp,
        CancellationToken ct);
}