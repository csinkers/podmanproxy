#nullable enable
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace NetProxy;

internal class UdpProxy : IProxy
{
    /// <summary>
    /// Milliseconds
    /// </summary>
    public int ConnectionTimeout { get; set; } = (4 * 60 * 1000);

    public async Task Start(string remoteServerHostNameOrAddress, ushort remoteServerPort, ushort localPort, string? localIp = null)
    {
        var connections = new ConcurrentDictionary<IPEndPoint, UdpConnection>();

        // TCP will lookup every time while this is only once.
        var ips = await Dns.GetHostAddressesAsync(remoteServerHostNameOrAddress).ConfigureAwait(false);
        var remoteServerEndPoint = new IPEndPoint(ips[0], remoteServerPort);

        var localServer = new UdpClient(AddressFamily.InterNetworkV6);
        localServer.Client.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, false);
        IPAddress localIpAddress = string.IsNullOrEmpty(localIp) ? IPAddress.IPv6Any : IPAddress.Parse(localIp);
        localServer.Client.Bind(new IPEndPoint(localIpAddress, localPort));

        Console.WriteLine($"UDP proxy started [{localIpAddress}]:{localPort} -> [{remoteServerHostNameOrAddress}]:{remoteServerPort}");

        var _ = Task.Run(async () =>
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
                foreach (var connection in connections.ToArray())
                {
                    if (connection.Value.LastActivity + ConnectionTimeout < Environment.TickCount64)
                    {
                        connections.TryRemove(connection.Key, out UdpConnection? c);
                        connection.Value.Stop();
                    }
                }
            }
        });

        while (true)
        {
            try
            {
                var message = await localServer.ReceiveAsync().ConfigureAwait(false);
                var sourceEndPoint = message.RemoteEndPoint;
                var client = connections.GetOrAdd(sourceEndPoint,
                    ep =>
                    {
                        var udpConnection = new UdpConnection(localServer, sourceEndPoint, remoteServerEndPoint);
                        udpConnection.Run();
                        return udpConnection;
                    });
                await client.SendToServerAsync(message.Buffer).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"an exception occurred on receiving a client datagram: {ex}");
            }
        }
    }
}