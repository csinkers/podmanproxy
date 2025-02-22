using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace PodmanProxy;

public static class ProxyServer
{
    class Container { public IPAddress? CurrentRemote { get; set; } }

    public static async Task Run(string configPath, ILogger log, CancellationToken ct)
    {
        Container container = new();
        var config = ParseConfig(configPath);

        var manager = new ProxyManager();
        var managerTask = manager.Run(log, ct);

        var configDir = Path.GetDirectoryName(configPath) ?? throw new InvalidOperationException();
        var watcher = new FileSystemWatcher(configDir, Path.GetFileName(configPath));
        watcher.IncludeSubdirectories = false;
        watcher.EnableRaisingEvents = true;
        watcher.Changed += (_, _) =>
        {
            try
            {
                config = ParseConfig(configPath);
                if (container.CurrentRemote != null)
                    manager.UpdateConfig(config.WithRemote(container.CurrentRemote));
            }
            catch (Exception ex)
            {
                log.LogError($"An exception occurred on config file change: {ex}");
            }
        };

        var ip = "";
        var port = config.ControlPort;
        IPAddress localIpAddress = string.IsNullOrEmpty(ip) ? IPAddress.Any : IPAddress.Parse(ip);

        var server = new UdpClient(AddressFamily.InterNetwork);
        server.Client.Bind(new IPEndPoint(localIpAddress, port));
        log.LogInformation($"Listening on {localIpAddress}:{port}");

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var message = await server.ReceiveAsync(ct).ConfigureAwait(false);
                if (Equals(container.CurrentRemote, message.RemoteEndPoint.Address))
                    continue;

                container.CurrentRemote = message.RemoteEndPoint.Address;
                var updatedConfig = config.WithRemote(container.CurrentRemote);
                manager.UpdateConfig(updatedConfig);
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex)
            {
                log.LogError($"An exception occurred on receiving a control datagram: {ex}");
            }
        }

        await managerTask;
    }

    static Config ParseConfig(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("Invalid config path", path);

        var configJson = File.ReadAllText(path);
        return Config.Parse(configJson);
    }
}
