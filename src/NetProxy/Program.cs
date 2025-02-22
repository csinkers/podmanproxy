using System;
using System.Threading.Tasks;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace NetProxy;

internal static class Program
{
    const string Remote = "172.28.27.207"; // TODO: Detect via listening for broadcast packets

    static void Main(string[] args)
    {
        var log = new ConsoleLogger();
        log.LogLevel = LogLevel.Debug;
        try
        {
            var cts = new CancellationTokenSource();

            var configJson = System.IO.File.ReadAllText("config.json");
            Config? config = Config.Parse(configJson);
            if (config == null)
                throw new FormatException("Config could not be parsed");

            for (var i = 0; i < config.Proxies.Count; i++)
                config.Proxies[i] = config.Proxies[i].Replace("$remote", Remote);

            var proxyConfigs = config.ParseProxyConfigs(log);
            var tasks = proxyConfigs.Select(c => ProxyFromConfig(log, c, cts.Token)).ToList();

            while (Console.ReadKey().KeyChar != 'q')
            {
            }

            cts.Cancel();
            Task.WhenAll(tasks).Wait(CancellationToken.None);
        }
        catch (Exception ex)
        {
            log.LogError($"An error occurred : {ex}");
        }
    }

    static Task ProxyFromConfig(ConsoleLogger log, ProxyConfig proxyConfig, CancellationToken ct)
    {
        try
        {
            return proxyConfig.Protocol switch
            {
                Protocol.Udp => UdpProxy.Start(log, proxyConfig, ct),
                Protocol.Tcp => TcpProxy.Start(log, proxyConfig, ct),
                _ => throw new InvalidOperationException($"Protocol not supported {proxyConfig.Protocol}")
            };
        }
        catch (Exception ex)
        {
            log.LogError($"Failed to start proxy : {ex.Message}");
            throw;
        }
    }
}