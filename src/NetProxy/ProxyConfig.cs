namespace NetProxy;

public record ProxyConfig(Protocol Protocol, string LocalIp, ushort LocalPort, string ForwardIp, ushort ForwardPort);