using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace PodmanProxy;

internal static class UdpProxy
{
    const int ConnectionTimeoutMilliseconds = 4 * 60 * 1000;

    public static async Task Start(
        ILogger log,
        ProxyConfig config,
        CancellationToken ct)
    {
        var connections = new ConcurrentDictionary<IPEndPoint, UdpConnection>();

        var ip = IPAddress.Parse(config.ForwardIp);
        var remoteServerEndPoint = new IPEndPoint(ip, config.ForwardPort);

        using var localServer = new UdpClient(AddressFamily.InterNetwork);
        IPAddress localIpAddress = string.IsNullOrEmpty(config.LocalIp) ? IPAddress.Any : IPAddress.Parse(config.LocalIp);
        localServer.Client.Bind(new IPEndPoint(localIpAddress, config.LocalPort));

        log.LogInformation($"UDP proxy started [{localIpAddress}]:{config.LocalPort} -> [{config.ForwardIp}]:{config.ForwardPort}");

        _ = Task.Run(async () => // Cleanup task
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(10), ct).ConfigureAwait(false);
                    foreach (var connection in connections.ToArray())
                    {
                        if (connection.Value.LastActivityTickCount + ConnectionTimeoutMilliseconds < Environment.TickCount64)
                        {
                            log.LogDebug($"Cleaning up idle UDP connection {connection.Key}");
                            connections.TryRemove(connection.Key, out _);
                            connection.Value.Stop();
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
                var message = await localServer.ReceiveAsync(ct).ConfigureAwait(false);
                var sourceEndPoint = message.RemoteEndPoint;
                var client = connections.GetOrAdd(sourceEndPoint,
                    ep =>
                    {
                        var udpConnection = new UdpConnection(log, localServer, ep, remoteServerEndPoint);
                        udpConnection.Run();
                        return udpConnection;
                    });

                await client.SendToServerAsync(message.Buffer).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { /* Expected during shutdown */ }
            catch (Exception ex)
            {
                log.LogWarning($"An exception occurred on receiving a client datagram: {ex}");
            }
        }

        log.LogInformation($"UDP proxy stopped [{localIpAddress}]:{config.LocalPort} -> [{config.ForwardIp}]:{config.ForwardPort}");
    }
}