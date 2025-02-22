using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace PodmanProxy;

/*
Install service via Powershell:

New-Service `
  -Name "PodmanProxy" `
  -DisplayName "Podman Proxy" `
  -BinaryPathName "C:\path\to\PodmanProxy.exe --ConfigPath=C:\path\to\config.json" `
  -StartupType Automatic
*/

internal class Program
{
    public static int Main(string[] args)
    {
        try
        {
            var builder = Host.CreateApplicationBuilder(args);
            builder.Configuration.AddCommandLine(args);
            builder.Services.AddWindowsService(options => { options.ServiceName = "Podman Proxy"; });
            builder.Services.AddHostedService<Worker>();

            var host = builder.Build();
            host.Run();
            return 0;
        }
        catch (Exception)
        {
            return 1;
        }
    }

    /* Old CLI implementation
    public static int RunCli(string[] args)
    {
        var log = new ConsoleLogger();
        if (args.Length != 1)
            return PrintUsage();

        try
        {
            var cts = new CancellationTokenSource();
            Task task = ProxyServer.Run(args[0], log, cts.Token);

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
            task.Wait(CancellationToken.None);

            return 0;
        }
        catch (Exception ex)
        {
            log.LogError($"An error occurred : {ex}");
            return 1;
        }
    }

    static int PrintUsage()
    {
        var name = Assembly.GetExecutingAssembly().GetName().Name;
        Console.WriteLine($"Usage: {name} configPath");
        return 1;
    }
    */
}
