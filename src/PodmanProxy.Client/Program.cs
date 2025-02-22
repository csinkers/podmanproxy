using System.Net;
using System.Net.Sockets;
using System.Reflection;

namespace PodProxy.Client;

/// <summary>
/// Small client program to run on the podman WSL2 VM that
/// broadcasts control datagrams to the proxy server letting
/// it know which IP the WSL2 VM is using so it can forward
/// traffic appropriately.
/// </summary>
internal static class Program
{
    const ushort DefaultPort = 9188;

    public static int Main(string[] args)
    {
        try
        {
            var arg = args.Length == 0 ? DefaultPort.ToString() : args[0];
            if (args.Length > 1)
                return PrintUsage();

            if (!ushort.TryParse(arg, out var port))
                throw new FormatException("Expected control port, e.g. 9188");

            var cts = new CancellationTokenSource();
            Task task = RunClient(port, cts.Token);

            Console.WriteLine("Press q to exit");
            while (Console.ReadKey().KeyChar != 'q')
            {
            }

            cts.Cancel();
            task.Wait(CancellationToken.None);
            return 0;
        }
        catch (AggregateException ex)
        {
            foreach (var inner in ex.InnerExceptions)
            {
                if (inner is OperationCanceledException)
                    continue;

                throw;
            }

            return 0;
        }
        catch (OperationCanceledException)
        {
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred : {ex}");
            return 1;
        }
    }

    static int PrintUsage()
    {
        var name = Assembly.GetExecutingAssembly().GetName().Name;
        Console.WriteLine($"Usage: {name} controlPort (e.g. 9188)");
        return 1;
    }

    static async Task RunClient(ushort port, CancellationToken ct)
    {
        TimeSpan requestInterval = TimeSpan.FromSeconds(5);
        var serverEndpoint = new IPEndPoint(IPAddress.Broadcast, port);

        while (!ct.IsCancellationRequested)
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
                Console.WriteLine($"An exception occurred sending a control datagram: {ex}");
            }

            await Task.Delay(requestInterval, ct);
        }
    }
}