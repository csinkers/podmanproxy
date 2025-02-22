using System.Threading.Tasks;

namespace NetProxy;

internal interface IProxy
{
    Task Start(string remoteServerHostNameOrAddress, ushort remoteServerPort, ushort localPort, string? localIp = null);
}