namespace NetProxy;

public record ProxyConfig(string Config, Protocol Protocol, string LocalIp, ushort LocalPort, string ForwardIp, ushort ForwardPort)
{
    public override string ToString() => Config;
}