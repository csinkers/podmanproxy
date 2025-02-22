using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace NetProxy;

internal class UdpProxy : IProxy
{
    const int ConnectionTimeoutMilliseconds = 4 * 60 * 1000;

    public async Task Start(
        ILogger log,
        string remoteServerHostNameOrAddress,
        ushort remoteServerPort,
        ushort localPort,
        string? localIp,
        CancellationToken ct)
    {
        var connections = new ConcurrentDictionary<IPEndPoint, UdpConnection>();

        // TCP will look up every time while this is only once.
        var ips = await Dns.GetHostAddressesAsync(remoteServerHostNameOrAddress, ct).ConfigureAwait(false);
        var remoteServerEndPoint = new IPEndPoint(ips[0], remoteServerPort);

        var localServer = new UdpClient(AddressFamily.InterNetworkV6);
        localServer.Client.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, false);
        IPAddress localIpAddress = string.IsNullOrEmpty(localIp) ? IPAddress.IPv6Any : IPAddress.Parse(localIp);
        localServer.Client.Bind(new IPEndPoint(localIpAddress, localPort));

        log.LogInformation($"UDP proxy started [{localIpAddress}]:{localPort} -> [{remoteServerHostNameOrAddress}]:{remoteServerPort}");

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