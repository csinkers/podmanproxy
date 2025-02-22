using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PodmanProxy;

public class Worker(IConfiguration configuration, ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        try
        {
            var configPath = configuration.GetValue<string>("ConfigPath");
            if (configPath == null)
            {
                logger.LogError("ConfigPath parameter must be supplied (e.g. --ConfigPath=C:\\...");
                Environment.Exit(2);
            }

            await ProxyServer.Run(configPath, logger, ct);
        }
        catch (AggregateException ex)
        {
            foreach (var inner in ex.InnerExceptions)
            {
                if (inner is OperationCanceledException)
                    continue;

                throw;
            }
        }
        catch (OperationCanceledException) { /* Expected during shutdown */ }
        catch (Exception ex)
        {
            logger.LogError(ex, "{Message}", ex.Message);
            Environment.Exit(1);
        }
    }
}
