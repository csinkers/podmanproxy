#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace NetProxy;

internal class TcpProxy : IProxy
{
    /// <summary>
    /// Milliseconds
    /// </summary>
    public int ConnectionTimeout { get; set; } = (4 * 60 * 1000);

    public async Task Start(string remoteServerHostNameOrAddress, ushort remoteServerPort, ushort localPort, string? localIp)
    {
        var connections = new ConcurrentBag<TcpConnection>();

        IPAddress localIpAddress = string.IsNullOrEmpty(localIp) ? IPAddress.IPv6Any : IPAddress.Parse(localIp);
        var localServer = new TcpListener(new IPEndPoint(localIpAddress, localPort));
        localServer.Server.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, false);
        localServer.Start();

        Console.WriteLine($"TCP proxy started [{localIpAddress}]:{localPort} -> [{remoteServerHostNameOrAddress}]:{remoteServerPort}");

        var _ = Task.Run(async () =>
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromSeconds(10)).ConfigureAwait(false);

                var tempConnections = new List<TcpConnection>(connections.Count);
                while (connections.TryTake(out var connection))
                {
                    tempConnections.Add(connection);
                }

                foreach (var tcpConnection in tempConnections)
                {
                    if (tcpConnection.LastActivity + ConnectionTimeout < Environment.TickCount64)
                    {
                        tcpConnection.Stop();
                    }
                    else
                    {
                        connections.Add(tcpConnection);
                    }
                }
            }
        });

        while (true)
        {
            try
            {
                var ips = await Dns.GetHostAddressesAsync(remoteServerHostNameOrAddress).ConfigureAwait(false);

                var tcpConnection = await TcpConnection.AcceptTcpClientAsync(localServer,
                        new IPEndPoint(ips[0], remoteServerPort))
                    .ConfigureAwait(false);
                tcpConnection.Run();
                connections.Add(tcpConnection);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(ex);
                Console.ResetColor();
            }
        }
    }
}