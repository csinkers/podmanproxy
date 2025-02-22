using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.Text.Json;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace NetProxy;

internal static class Program
{
    static void Main(string[] args)
    {
        var log = new ConsoleLogger();
        try
        {
            var cts = new CancellationTokenSource();

            var configJson = System.IO.File.ReadAllText("config.json");
            Dictionary<string, ProxyConfig>? configs = JsonSerializer.Deserialize<Dictionary<string, ProxyConfig>>(configJson);
            if (configs == null)
                throw new Exception("configs is null");

            var tasks = configs.SelectMany(c => ProxyFromConfig(log, c.Key, c.Value, cts.Token));
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

    static IEnumerable<Task> ProxyFromConfig(
        ConsoleLogger log,
        string proxyName,
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
            if (forwardIp == null)
                throw new ArgumentException("forwardIp is null", nameof(proxyConfig));

            if (!forwardPort.HasValue)
                throw new ArgumentException("forwardPort is null", nameof(proxyConfig));

            if (!localPort.HasValue)
                throw new ArgumentException("localPort is null", nameof(proxyConfig));

            if (protocol != "udp" && protocol != "tcp" && protocol != "any")
                throw new ArgumentException($"protocol is not supported {protocol}", nameof(proxyConfig));
        }
        catch (Exception ex)
        {
            log.LogError($"Failed to start {proxyName} : {ex.Message}");
            throw;
        }

        bool protocolHandled = false;
        if (protocol is "udp" or "any")
        {
            protocolHandled = true;
            Task task;
            try
            {
                var proxy = new UdpProxy();
                task = proxy.Start(log, forwardIp, forwardPort.Value, localPort.Value, localIp, ct);
            }
            catch (Exception ex)
            {
                log.LogError($"Failed to start {proxyName} : {ex.Message}");
                throw;
            }

            yield return task;
        }

        if (protocol is "tcp" or "any")
        {
            protocolHandled = true;
            Task task;
            try
            {
                var proxy = new TcpProxy();
                task = proxy.Start(log, forwardIp, forwardPort.Value, localPort.Value, localIp, ct);
            }
            catch (Exception ex)
            {
                log.LogError($"Failed to start {proxyName} : {ex.Message}");
                throw;
            }

            yield return task;
        }

        if (!protocolHandled)
            throw new InvalidOperationException($"Protocol not supported {protocol}");
    }
}