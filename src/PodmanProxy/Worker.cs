using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PodmanProxy;

public class Worker(IOptionsMonitor<PodmanProxyOptions> options, ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        try
        {
            await ProxyServer.Run(options, logger, ct);
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
