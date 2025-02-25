using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PodmanProxy;

public static class ProxyServer
{
    class Container { public IPAddress? CurrentRemote { get; set; } }

    public static async Task Run(IOptionsMonitor<PodmanProxyOptions> options, ILogger log, CancellationToken ct)
    {
        Container container = new();
        var manager = new ProxyManager();
        var managerTask = manager.Run(log, ct);

        options.OnChange(newOptions =>
        {
            try
            {
                if (container.CurrentRemote != null)
                    manager.UpdateConfig(newOptions, container.CurrentRemote);
            }
            catch (Exception ex)
            {
                log.LogError("An exception occurred on config file change: {ex}", ex);
            }
        });

        var ip = "";
        var port = options.CurrentValue.ControlPort;
        IPAddress localIpAddress = string.IsNullOrEmpty(ip) ? IPAddress.Any : IPAddress.Parse(ip);

        var server = new UdpClient(AddressFamily.InterNetwork);
        server.Client.Bind(new IPEndPoint(localIpAddress, port));
        log.LogInformation("Listening on {ip}:{port}", localIpAddress, port);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var message = await server.ReceiveAsync(ct).ConfigureAwait(false);
                if (Equals(container.CurrentRemote, message.RemoteEndPoint.Address))
                    continue;

                container.CurrentRemote = message.RemoteEndPoint.Address;
                manager.UpdateConfig(options.CurrentValue, container.CurrentRemote);
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex)
            {
                log.LogError("An exception occurred on receiving a control datagram: {ex}", ex);
            }
        }

        await managerTask;
    }
}
