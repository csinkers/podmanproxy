using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace PodmanProxy;

internal class UdpConnection
{
    readonly TaskCompletionSource<bool> _forwardConnectionBindCompleted = new();
    readonly UdpClient _localServer;
    readonly UdpClient _forwardClient;
    readonly IPEndPoint _sourceEndpoint;
    readonly IPEndPoint _remoteEndpoint;
    readonly string _description;
    readonly ILogger _log;

    EndPoint? _forwardLocalEndpoint;
    bool _isRunning = true;
    long _totalBytesForwarded;
    long _totalBytesResponded;

    public long LastActivityTickCount { get; private set; } = Environment.TickCount64;

    public UdpConnection(ILogger log, UdpClient localServer, IPEndPoint sourceEndpoint, IPEndPoint remoteEndpoint)
    {
        _log = log;
        _localServer = localServer;
        var serverLocalEndpoint = _localServer.Client.LocalEndPoint;
        _sourceEndpoint = sourceEndpoint;
        _remoteEndpoint = remoteEndpoint;

        _forwardClient = new UdpClient(AddressFamily.InterNetwork);
        _description = $"{_sourceEndpoint} => {serverLocalEndpoint} => {_forwardLocalEndpoint} => {_remoteEndpoint}";
    }

    public override string ToString() => _description;

    public async Task SendToServerAsync(byte[] message)
    {
        LastActivityTickCount = Environment.TickCount64;

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
                _log.LogDebug($"Established UDP {_description}");

                while (_isRunning)
                {
                    try
                    {
                        var result = await _forwardClient.ReceiveAsync().ConfigureAwait(false);
                        LastActivityTickCount = Environment.TickCount64;
                        var sent = await _localServer.SendAsync(result.Buffer, result.Buffer.Length, _sourceEndpoint).ConfigureAwait(false);
                        Interlocked.Add(ref _totalBytesResponded, sent);
                    }
                    catch (OperationCanceledException) { /* Expected during shutdown */ }
                    catch (Exception ex)
                    {
                        if (_isRunning)
                            _log.LogWarning($"An exception occurred while receiving a server datagram : {ex}");
                    }
                }
            }
        });
    }

    public void Stop()
    {
        try
        {
            _log.LogDebug($"Closed UDP {_description}. {_totalBytesForwarded} bytes forwarded, {_totalBytesResponded} bytes responded.");
            _isRunning = false;
            _forwardClient.Close();
        }
        catch (Exception ex)
        {
            _log.LogWarning($"An exception occurred while closing UdpConnection : {ex}");
        }
    }
}