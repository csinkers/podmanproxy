using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PodmanProxy;

// Note: Install service via Setup.ps1
internal static class Program
{
    const string ConfigFilename = "podmanproxy.json";

    public static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Logging.AddSimpleConsole(x => x.SingleLine = true);
        builder.Configuration.AddJsonFile(ConfigFilename, false, true);
        builder.Services.Configure<PodmanProxyOptions>(builder.Configuration.GetSection("PodmanProxy"));
        builder.Services.AddWindowsService(options => { options.ServiceName = "PodmanProxy"; });
        builder.Services.AddHostedService<Worker>();

        var host = builder.Build();
        var mainTask = host.RunAsync();
        await mainTask;
    }
}
