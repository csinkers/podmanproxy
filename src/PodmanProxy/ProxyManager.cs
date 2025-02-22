using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace PodmanProxy;

public class ProxyManager
{
    readonly Channel<Config> _configChannel = Channel.CreateUnbounded<Config>();

    public void UpdateConfig(Config config) => _configChannel.Writer.TryWrite(config);

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
            var config = await _configChannel.Reader.ReadAsync(ct);

            // Add any new proxies
            foreach (var proxyConfig in config.ParseProxyConfigs(log))
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

    static Task ProxyFromConfig(ProxyConfig config, ILogger log, CancellationToken ct)
    {
        try
        {
            return config.Protocol switch
            {
                Protocol.Udp => UdpProxy.Start(log, config, ct),
                Protocol.Tcp => TcpProxy.Start(log, config, ct),
                _ => throw new InvalidOperationException($"protocol not supported {config.Protocol}")
            };
        }
        catch (Exception ex)
        {
            log.LogError($"Failed to start \"{config}\" : {ex.Message}");
            throw;
        }
    }
}