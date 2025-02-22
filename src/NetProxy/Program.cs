#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.Threading;

namespace NetProxy;

internal static class Program
{
    static void Main(string[] args)
    {
        try
        {
            var cts = new CancellationTokenSource();
            var configJson = System.IO.File.ReadAllText("config.json");
            Dictionary<string, ProxyConfig>? configs = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, ProxyConfig>>(configJson);
            if (configs == null)
                throw new Exception("configs is null");

            var tasks = configs.SelectMany(c => ProxyFromConfig(c.Key, c.Value, cts.Token));
            while (Console.ReadKey().KeyChar != 'q')
            {
            }

            cts.Cancel();
            Task.WhenAll(tasks).Wait(CancellationToken.None);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred : {ex}");
        }
    }

    static IEnumerable<Task> ProxyFromConfig(string proxyName, ProxyConfig proxyConfig, CancellationToken ct)
    {
        var forwardPort = proxyConfig.ForwardPort;
        var localPort = proxyConfig.LocalPort;
        var forwardIp = proxyConfig.ForwardIp;
        var localIp = proxyConfig.LocalIp;
        var protocol = proxyConfig.Protocol;

        try
        {
            if (forwardIp == null)
                throw new Exception("forwardIp is null");
            if (!forwardPort.HasValue)
                throw new Exception("forwardPort is null");
            if (!localPort.HasValue)
                throw new Exception("localPort is null");
            if (protocol != "udp" && protocol != "tcp" && protocol != "any")
                throw new Exception($"protocol is not supported {protocol}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to start {proxyName} : {ex.Message}");
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
                task = proxy.Start(forwardIp, forwardPort.Value, localPort.Value, localIp, ct);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to start {proxyName} : {ex.Message}");
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
                task = proxy.Start(forwardIp, forwardPort.Value, localPort.Value, localIp, ct);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to start {proxyName} : {ex.Message}");
                throw;
            }

            yield return task;
        }

        if (!protocolHandled)
            throw new InvalidOperationException($"protocol not supported {protocol}");
    }
}