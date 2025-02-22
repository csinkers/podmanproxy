using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PodmanProxy.Client;

/// <summary>
/// Small client program to run on the podman WSL2 VM that
/// broadcasts control datagrams to the proxy server letting
/// it know which IP the WSL2 VM is using so it can forward
/// traffic appropriately.
/// </summary>
internal static class Program
{
    public static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Configuration.AddCommandLine(args);
        builder.Logging.AddConsole();
        builder.Services.AddSystemd();
        builder.Services.AddHostedService<ClientWorker>();

        var host = builder.Build();
        await host.RunAsync();
    }
}
