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
            var tasks = proxyConfigs.Select(c => ProxyFromConfig(log, c, cts.Token));

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

    static Task ProxyFromConfig(
        ConsoleLogger log,
        ProxyConfig proxyConfig,
        CancellationToken ct)
    {
        var forwardPort = proxyConfig.ForwardPort;
        var localPort = proxyConfig.LocalPort;
        var forwardIp = proxyConfig.ForwardIp;
        var localIp = proxyConfig.LocalIp;
        var protocol = proxyConfig.Protocol;

        try
        {
            switch (protocol)
            {
                case Protocol.Udp:
                    {
                        var proxy = new UdpProxy();
                        return proxy.Start(log, forwardIp, forwardPort, localPort, localIp, ct);
                    }
                case Protocol.Tcp:
                    {
                        var proxy = new TcpProxy();
                        return proxy.Start(log, forwardIp, forwardPort, localPort, localIp, ct);
                    }
                default:
                    throw new InvalidOperationException($"Protocol not supported {protocol}");
            }
        }
        catch (Exception ex)
        {
            log.LogError($"Failed to start proxy : {ex.Message}");
            throw;
        }
    }
}