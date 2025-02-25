using System.Net;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace PodmanProxy;

public class ProxyManager
{
    readonly Channel<(PodmanProxyOptions, IPAddress)> _configChannel = Channel.CreateUnbounded<(PodmanProxyOptions, IPAddress)>();

    public void UpdateConfig(PodmanProxyOptions options, IPAddress remoteAddress) => _configChannel.Writer.TryWrite((options, remoteAddress));

    public async Task Run(ILogger log, CancellationToken ct)
    {
        Dictionary<string, (Task Task, CancellationTokenSource Cts)> proxies = new();
        ct.Register(() =>
        {
            foreach (var kvp in proxies)
                kvp.Value.Cts.Cancel();

            proxies.Clear();
        });

        while (!ct.IsCancellationRequested)
        {
            var (config, remote) = await _configChannel.Reader.ReadAsync(ct);

            // Add any new proxies
            foreach (var proxyConfig in config.ParseProxyConfigs(log, remote))
            {
                if (proxies.TryGetValue(proxyConfig.Config, out var existing))
                    if (IsStatusOk(existing.Task.Status)) // If it's not complete/faulted/canceled then keep using it
                        continue;

                var cts = new CancellationTokenSource();
                var proxyTask = ProxyFromConfig(proxyConfig, log, cts.Token);
                proxies.Add(proxyConfig.Config, (proxyTask, cts));
            }

            // Remove any stale proxies
            foreach (var kvp in proxies.ToArray())
            {
                if (!config.Proxies.Contains(kvp.Key))
                {
                    await kvp.Value.Cts.CancelAsync();
                    proxies.Remove(kvp.Key);
                }
            }
        }
    }

    static bool IsStatusOk(TaskStatus status)
        => status switch
        {
            TaskStatus.Faulted => false,
            TaskStatus.Canceled => false,
            TaskStatus.RanToCompletion => false,
            _ => true
        };

    static async Task ProxyFromConfig(ProxyConfig config, ILogger log, CancellationToken ct)
    {
        try
        {
            switch (config.Protocol)
            {
                case Protocol.Udp:
                    await UdpProxy.Start(log, config, ct);
                    break;

                case Protocol.Tcp:
                    await TcpProxy.Start(log, config, ct);
                    break;

                default:
                    throw new InvalidOperationException($"protocol not supported {config.Protocol}");
            }
        }
        catch (Exception ex)
        {
            log.LogError("Failed to start \"{config}\" : {ex}", config, ex.Message);
            throw;
        }
    }
}