using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PodProxy.Client;

public class ClientWorker(IConfiguration configuration, ILogger<ClientWorker> logger) : BackgroundService
{
    const ushort DefaultPort = 9188;
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        TimeSpan requestInterval = TimeSpan.FromSeconds(configuration.GetValue("RequestIntervalSeconds", 5));
        ushort port = configuration.GetValue("ControlPort", DefaultPort);

        var serverEndpoint = new IPEndPoint(IPAddress.Broadcast, port);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var client = new UdpClient(AddressFamily.InterNetwork);
                client.Client.Bind(new IPEndPoint(IPAddress.Any, 0));

                var message = new byte[] { 1 };
                _ = await client.SendAsync(message, message.Length, serverEndpoint).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError("An exception occurred sending a control datagram: {ex}", ex);
            }

            await Task.Delay(requestInterval, stoppingToken);
        }
    }
}