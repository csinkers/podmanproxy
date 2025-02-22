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
        using var localServer = new TcpListener(new IPEndPoint(localIpAddress, config.LocalPort));
        localServer.Start();

        log.LogInformation($"TCP proxy started [{localIpAddress}]:{config.LocalPort} -> [{config.ForwardIp}]:{config.ForwardPort}");

        _ = Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(10), ct).ConfigureAwait(false);

                    var tempConnections = new List<TcpConnection>(connections.Count);
                    while (connections.TryTake(out var connection))
                        tempConnections.Add(connection);

                    foreach (var tcpConnection in tempConnections)
                    {
                        if (tcpConnection.LastActivityTickCount + ConnectionTimeoutMilliseconds < Environment.TickCount64)
                        {
                            log.LogDebug($"Cleaning up idle TCP connection {tcpConnection}");
                            tcpConnection.Stop();
                        }
                        else
                        {
                            connections.Add(tcpConnection);
                        }
                    }
                }
                catch (OperationCanceledException) { /* Expected during shutdown */ }
            }
        }, ct);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var ip = IPAddress.Parse(config.ForwardIp);
                var endpoint = new IPEndPoint(ip, config.ForwardPort);

                var tcpConnection =
                    await TcpConnection.AcceptTcpClientAsync(log, localServer, endpoint, ct)
                    .ConfigureAwait(false);

                tcpConnection.Run();
                connections.Add(tcpConnection);
            }
            catch (OperationCanceledException) { /* Expected during shutdown */ }
            catch (Exception ex)
            {
                log.LogError(ex.ToString());
            }
        }

        log.LogInformation($"TCP proxy stopped [{localIpAddress}]:{config.LocalPort} -> [{config.ForwardIp}]:{config.ForwardPort}");
    }
}