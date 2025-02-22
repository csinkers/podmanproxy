using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace PodmanProxy;

internal static class Program
{
    const string Remote = "172.28.27.207"; // TODO: Detect via listening for broadcast packets

    public static int Main(string[] args)
    {
        var log = new ConsoleLogger();
        if (args.Length != 1)
            return PrintUsage();

        try
        {
            var cts = new CancellationTokenSource();
            if (!File.Exists(args[0]))
                throw new FileNotFoundException("Could not find config file", args[0]);

            var configJson = System.IO.File.ReadAllText(args[0]);
            Config? config = Config.Parse(configJson);
            if (config == null)
                throw new FormatException("Config could not be parsed");

            config = config.WithRemote(IPAddress.Parse(Remote));

            var proxyConfigs = config.ParseProxyConfigs(log);
            var tasks = proxyConfigs.Select(c => ProxyFromConfig(log, c, cts.Token)).ToList();

            Console.WriteLine("Press q to exit, > to increase log verbosity, < to decrease");
            bool done = false;
            while (!done)
            {
                switch (Console.ReadKey().KeyChar)
                {
                    case 'q':
                        done = true;
                        break;

                    case '>':
                        if (log.LogLevel > LogLevel.Debug)
                        {
                            log.LogLevel--;
                            Console.WriteLine($"Level: {log.LogLevel}");
                        }

                        break;

                    case '<':
                        if (log.LogLevel < LogLevel.Critical)
                        {
                            log.LogLevel++;
                            Console.WriteLine($"Level: {log.LogLevel}");
                        }

                        break;
                }
            }

            cts.Cancel();
            Task.WhenAll(tasks).Wait(CancellationToken.None);

            return 0;
        }
        catch (Exception ex)
        {
            log.LogError($"An error occurred : {ex}");
            return 1;
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

    static int PrintUsage()
    {
        var name = Assembly.GetExecutingAssembly().GetName().Name;
        Console.WriteLine($"Usage: {name} configPath");
        return 1;
    }
}