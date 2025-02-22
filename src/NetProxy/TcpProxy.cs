using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace NetProxy;

public class TcpProxy : IProxy
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
        var connections = new ConcurrentBag<TcpConnection>();

        IPAddress localIpAddress = string.IsNullOrEmpty(localIp) ? IPAddress.IPv6Any : IPAddress.Parse(localIp);
        var localServer = new TcpListener(new IPEndPoint(localIpAddress, localPort));
        localServer.Server.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, false);
        localServer.Start();

        log.LogInformation($"TCP proxy started [{localIpAddress}]:{localPort} -> [{remoteServerHostNameOrAddress}]:{remoteServerPort}");

        _ = Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(10), ct).ConfigureAwait(false);

                var tempConnections = new List<TcpConnection>(connections.Count);
                while (connections.TryTake(out var connection))
                {
                    tempConnections.Add(connection);
                }

                foreach (var tcpConnection in tempConnections)
                {
                    if (tcpConnection.LastActivity + ConnectionTimeoutMilliseconds < Environment.TickCount64)
                    {
                        tcpConnection.Stop();
                    }
                    else
                    {
                        connections.Add(tcpConnection);
                    }
                }
            }
        }, ct);

        while (true)
        {
            try
            {
                var ips = await Dns.GetHostAddressesAsync(remoteServerHostNameOrAddress, ct).ConfigureAwait(false);

                var tcpConnection =
                    await TcpConnection.AcceptTcpClientAsync(
                        log,
                        localServer,
                        new IPEndPoint(ips[0], remoteServerPort)
                    )
                    .ConfigureAwait(false);

                tcpConnection.Run();
                connections.Add(tcpConnection);
            }
            catch (Exception ex)
            {
                log.LogError(ex.ToString());
            }
        }
    }
}