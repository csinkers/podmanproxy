using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace NetProxy;

internal class UdpConnection
{
    readonly UdpClient _localServer;
    readonly UdpClient _forwardClient;
    public long LastActivity { get; private set; } = Environment.TickCount64;
    readonly IPEndPoint _sourceEndpoint;
    readonly IPEndPoint _remoteEndpoint;
    readonly EndPoint? _serverLocalEndpoint;
    EndPoint? _forwardLocalEndpoint;
    bool _isRunning;
    long _totalBytesForwarded;
    long _totalBytesResponded;
    readonly TaskCompletionSource<bool> _forwardConnectionBindCompleted = new TaskCompletionSource<bool>();

    public UdpConnection(UdpClient localServer, IPEndPoint sourceEndpoint, IPEndPoint remoteEndpoint)
    {
        _localServer = localServer;
        _serverLocalEndpoint = _localServer.Client.LocalEndPoint;

        _isRunning = true;
        _remoteEndpoint = remoteEndpoint;
        _sourceEndpoint = sourceEndpoint;

        _forwardClient = new UdpClient(AddressFamily.InterNetworkV6);
        _forwardClient.Client.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, false);
    }

    public async Task SendToServerAsync(byte[] message)
    {
        LastActivity = Environment.TickCount64;

        await _forwardConnectionBindCompleted.Task.ConfigureAwait(false);
        var sent = await _forwardClient.SendAsync(message, message.Length, _remoteEndpoint).ConfigureAwait(false);
        Interlocked.Add(ref _totalBytesForwarded, sent);
    }

    public void Run()
    {
        Task.Run(async () =>
        {
            using (_forwardClient)
            {
                _forwardClient.Client.Bind(new IPEndPoint(IPAddress.Any, 0));
                _forwardLocalEndpoint = _forwardClient.Client.LocalEndPoint;
                _forwardConnectionBindCompleted.SetResult(true);
                Console.WriteLine($"Established UDP {_sourceEndpoint} => {_serverLocalEndpoint} => {_forwardLocalEndpoint} => {_remoteEndpoint}");

                while (_isRunning)
                {
                    try
                    {
                        var result = await _forwardClient.ReceiveAsync().ConfigureAwait(false);
                        LastActivity = Environment.TickCount64;
                        var sent = await _localServer.SendAsync(result.Buffer, result.Buffer.Length, _sourceEndpoint).ConfigureAwait(false);
                        Interlocked.Add(ref _totalBytesResponded, sent);
                    }
                    catch (Exception ex)
                    {
                        if (_isRunning)
                        {
                            Console.WriteLine($"An exception occurred while receiving a server datagram : {ex}");
                        }
                    }
                }
            }
        });
    }

    public void Stop()
    {
        try
        {
            Console.WriteLine($"Closed UDP {_sourceEndpoint} => {_serverLocalEndpoint} => {_forwardLocalEndpoint} => {_remoteEndpoint}. {_totalBytesForwarded} bytes forwarded, {_totalBytesResponded} bytes responded.");
            _isRunning = false;
            _forwardClient.Close();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An exception occurred while closing UdpConnection : {ex}");
        }
    }
}