using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace NetProxy;

internal static class UdpProxy
{
    const int ConnectionTimeoutMilliseconds = 4 * 60 * 1000;

    public static async Task Start(
        ILogger log,
        ProxyConfig config,
        CancellationToken ct)
    {
        var connections = new ConcurrentDictionary<IPEndPoint, UdpConnection>();

        // TCP will look up every time while this is only once.
        var ips = await Dns.GetHostAddressesAsync(config.ForwardIp, ct).ConfigureAwait(false);
        var remoteServerEndPoint = new IPEndPoint(ips[0], config.ForwardPort);

        var localServer = new UdpClient(AddressFamily.InterNetworkV6);
        localServer.Client.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, false);
        IPAddress localIpAddress = string.IsNullOrEmpty(config.LocalIp) ? IPAddress.IPv6Any : IPAddress.Parse(config.LocalIp);
        localServer.Client.Bind(new IPEndPoint(localIpAddress, config.LocalPort));

        log.LogInformation($"UDP proxy started [{localIpAddress}]:{config.LocalPort} -> [{config.ForwardIp}]:{config.ForwardPort}");

        _ = Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(10), ct).ConfigureAwait(false);
                foreach (var connection in connections.ToArray())
                {
                    if (connection.Value.LastActivity + ConnectionTimeoutMilliseconds < Environment.TickCount64)
                    {
                        connections.TryRemove(connection.Key, out UdpConnection? c);
                        connection.Value.Stop();
                    }
                }
            }
        }, ct);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var message = await localServer.ReceiveAsync(ct).ConfigureAwait(false);
                var sourceEndPoint = message.RemoteEndPoint;
                var client = connections.GetOrAdd(sourceEndPoint,
                    ep =>
                    {
                        var udpConnection = new UdpConnection(log, localServer, sourceEndPoint, remoteServerEndPoint);
                        udpConnection.Run();
                        return udpConnection;
                    });

                await client.SendToServerAsync(message.Buffer).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                log.LogError($"An exception occurred on receiving a client datagram: {ex}");
            }
        }
    }
}