using System;
using System.Buffers;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace NetProxy;

public class TcpConnection
{
    readonly TcpClient _localServerConnection;
    readonly IPEndPoint _remoteEndpoint;
    readonly TcpClient _forwardClient;
    readonly CancellationTokenSource _cancellationTokenSource = new();
    readonly string _description;
    readonly ILogger _log;

    EndPoint? _forwardLocalEndpoint;
    long _totalBytesForwarded;
    long _totalBytesResponded;

    public long LastActivity { get; private set; } = Environment.TickCount64;

    public static async Task<TcpConnection> AcceptTcpClientAsync(ILogger log, TcpListener tcpListener, IPEndPoint remoteEndpoint)
    {
        var localServerConnection = await tcpListener.AcceptTcpClientAsync().ConfigureAwait(false);
        localServerConnection.NoDelay = true;
        return new TcpConnection(log, localServerConnection, remoteEndpoint);
    }

    TcpConnection(ILogger log, TcpClient localServerConnection, IPEndPoint remoteEndpoint)
    {
        _log = log;
        _localServerConnection = localServerConnection;
        _remoteEndpoint = remoteEndpoint;
        var sourceEndpoint = _localServerConnection.Client.RemoteEndPoint;
        var serverLocalEndpoint = _localServerConnection.Client.LocalEndPoint;

        _forwardClient = new TcpClient { NoDelay = true };
        _description = $"{sourceEndpoint} => {serverLocalEndpoint} => {_forwardLocalEndpoint} => {_remoteEndpoint}";
    }

    public void Run() => RunInternal(_cancellationTokenSource.Token);

    public void Stop()
    {
        try
        {
            _cancellationTokenSource.Cancel();
        }
        catch (Exception ex)
        {
            _log.LogError($"An exception occurred while closing TcpConnection : {ex}");
        }
    }

    public override string ToString() => _description;

    void RunInternal(CancellationToken cancellationToken)
    {
        Task.Run(async () =>
        {
            try
            {
                using (_localServerConnection)
                using (_forwardClient)
                {
                    await _forwardClient.ConnectAsync(_remoteEndpoint.Address, _remoteEndpoint.Port, cancellationToken).ConfigureAwait(false);
                    _forwardLocalEndpoint = _forwardClient.Client.LocalEndPoint;

                    _log.LogDebug($"Established TCP {_description}");

                    await using (var serverStream = _forwardClient.GetStream())
                    await using (var clientStream = _localServerConnection.GetStream())
                    await using (cancellationToken.Register(() =>
                                 {
                                     serverStream.Close();
                                     clientStream.Close();
                                 }, true))
                    {
                        await Task.WhenAny(
                            CopyToAsync(clientStream, serverStream, 81920, Direction.Forward, cancellationToken),
                            CopyToAsync(serverStream, clientStream, 81920, Direction.Responding, cancellationToken)
                        ).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning($"An exception occurred during TCP stream : {ex}");
            }
            finally
            {
                _log.LogDebug($"Closed TCP {_description}. {_totalBytesForwarded} bytes forwarded, {_totalBytesResponded} bytes responded.");
            }
        }, cancellationToken);
    }

    async Task CopyToAsync(
        Stream source,
        Stream destination,
        int bufferSize = 81920,
        Direction direction = Direction.Unknown,
        CancellationToken ct = default)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
        try
        {
            while (!ct.IsCancellationRequested)
            {
                int bytesRead = await source.ReadAsync(new Memory<byte>(buffer), ct).ConfigureAwait(false);
                if (bytesRead == 0)
                    break;

                LastActivity = Environment.TickCount64;
                await destination.WriteAsync(new ReadOnlyMemory<byte>(buffer, 0, bytesRead), ct).ConfigureAwait(false);

                switch (direction)
                {
                    case Direction.Forward:
                        Interlocked.Add(ref _totalBytesForwarded, bytesRead);
                        break;
                    case Direction.Responding:
                        Interlocked.Add(ref _totalBytesResponded, bytesRead);
                        break;
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}