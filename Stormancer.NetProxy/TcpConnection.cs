using System;
using System.Buffers;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace NetProxy;

internal class TcpConnection
{
    readonly TcpClient _localServerConnection;
    readonly EndPoint? _sourceEndpoint;
    readonly IPEndPoint _remoteEndpoint;
    readonly TcpClient _forwardClient;
    readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
    readonly EndPoint? _serverLocalEndpoint;
    EndPoint? _forwardLocalEndpoint;
    long _totalBytesForwarded;
    long _totalBytesResponded;
    public long LastActivity { get; private set; } = Environment.TickCount64;

    public static async Task<TcpConnection> AcceptTcpClientAsync(TcpListener tcpListener, IPEndPoint remoteEndpoint)
    {
        var localServerConnection = await tcpListener.AcceptTcpClientAsync().ConfigureAwait(false);
        localServerConnection.NoDelay = true;
        return new TcpConnection(localServerConnection, remoteEndpoint);
    }

    TcpConnection(TcpClient localServerConnection, IPEndPoint remoteEndpoint)
    {
        _localServerConnection = localServerConnection;
        _remoteEndpoint = remoteEndpoint;

        _forwardClient = new TcpClient {NoDelay = true};

        _sourceEndpoint = _localServerConnection.Client.RemoteEndPoint;
        _serverLocalEndpoint = _localServerConnection.Client.LocalEndPoint;
    }

    public void Run()
    {
        RunInternal(_cancellationTokenSource.Token);
    }

    public void Stop()
    {
        try
        {
            _cancellationTokenSource.Cancel();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An exception occurred while closing TcpConnection : {ex}");
        }
    }

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

                    Console.WriteLine($"Established TCP {_sourceEndpoint} => {_serverLocalEndpoint} => {_forwardLocalEndpoint} => {_remoteEndpoint}");

                    using (var serverStream = _forwardClient.GetStream())
                    using (var clientStream = _localServerConnection.GetStream())
                    using (cancellationToken.Register(() =>
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
                Console.WriteLine($"An exception occurred during TCP stream : {ex}");
            }
            finally
            {
                Console.WriteLine($"Closed TCP {_sourceEndpoint} => {_serverLocalEndpoint} => {_forwardLocalEndpoint} => {_remoteEndpoint}. {_totalBytesForwarded} bytes forwarded, {_totalBytesResponded} bytes responded.");
            }
        });
    }

    async Task CopyToAsync(Stream source, Stream destination, int bufferSize = 81920, Direction direction = Direction.Unknown, CancellationToken cancellationToken = default)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
        try
        {
            while (true)
            {
                int bytesRead = await source.ReadAsync(new Memory<byte>(buffer), cancellationToken).ConfigureAwait(false);
                if (bytesRead == 0) break;
                LastActivity = Environment.TickCount64;
                await destination.WriteAsync(new ReadOnlyMemory<byte>(buffer, 0, bytesRead), cancellationToken).ConfigureAwait(false);

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