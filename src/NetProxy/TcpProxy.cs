using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace NetProxy;

public static class TcpProxy
{
    const int ConnectionTimeoutMilliseconds = 4 * 60 * 1000;

    public static async Task Start(
        ILogger log,
        ProxyConfig config,
        CancellationToken ct)
    {
        var connections = new ConcurrentBag<TcpConnection>();

        IPAddress localIpAddress = string.IsNullOrEmpty(config.LocalIp) ? IPAddress.Any : IPAddress.Parse(config.LocalIp);
        var localServer = new TcpListener(new IPEndPoint(localIpAddress, config.LocalPort));
        localServer.Start();

        log.LogInformation($"TCP proxy started [{localIpAddress}]:{config.LocalPort} -> [{config.ForwardIp}]:{config.ForwardPort}");

        _ = Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(10), ct).ConfigureAwait(false);

                var tempConnections = new List<TcpConnection>(connections.Count);
                while (connections.TryTake(out var connection))
                    tempConnections.Add(connection);

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

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var ip = IPAddress.Parse(config.ForwardIp);
                var endpoint = new IPEndPoint(ip, config.ForwardPort);

                var tcpConnection =
                    await TcpConnection.AcceptTcpClientAsync(log, localServer, endpoint)
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