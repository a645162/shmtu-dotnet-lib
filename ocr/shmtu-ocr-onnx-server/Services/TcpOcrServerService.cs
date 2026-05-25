using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Options;

namespace shmtu.captcha.onnx.server.Services;

public class TcpOcrServerService : BackgroundService
{
    private readonly OcrService _ocrService;
    private readonly ILogger<TcpOcrServerService> _logger;
    private readonly int _port;
    private readonly IPAddress _listenAddress;

    private const string EndMarker = "<END>";
    private static readonly byte[] EndMarkerBytes = Encoding.UTF8.GetBytes(EndMarker);

    public TcpOcrServerService(
        OcrService ocrService,
        IOptions<OcrServerConfig> config,
        ILogger<TcpOcrServerService> logger)
    {
        _ocrService = ocrService;
        _logger = logger;
        _port = config.Value.TcpPort;
        _listenAddress = IPAddress.Parse(config.Value.TcpListenAddress);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

        try
        {
            listener.Bind(new IPEndPoint(_listenAddress, _port));
            listener.Listen(128);

            _logger.LogInformation("TCP OCR server listening on {Address}:{Port}", _listenAddress, _port);

            while (!stoppingToken.IsCancellationRequested)
            {
                Socket client;
                try
                {
                    client = await listener.AcceptAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                _ = HandleClientAsync(client, stoppingToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TCP OCR server fatal error");
        }
        finally
        {
            try { listener.Close(); } catch { /* ignore */ }
        }
    }

    private async Task HandleClientAsync(Socket client, CancellationToken ct)
    {
        var remote = client.RemoteEndPoint?.ToString() ?? "unknown";
        _logger.LogDebug("[{Remote}] Connected", remote);

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(30));

            var imageData = await ReadUntilEndMarkerAsync(client, timeoutCts.Token);

            if (imageData == null || imageData.Length == 0)
            {
                _logger.LogWarning("[{Remote}] Empty or invalid data received", remote);
                return;
            }

            _logger.LogDebug("[{Remote}] Received {Bytes} bytes", remote, imageData.Length);

            var response = _ocrService.Predict(imageData);
            var resultText = response.Success ? response.Expression : "";

            var resultBytes = Encoding.UTF8.GetBytes(resultText);
            await client.SendAsync(resultBytes, SocketFlags.None, ct);
            _logger.LogDebug("[{Remote}] Result: {Result}", remote, resultText);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("[{Remote}] Connection timed out", remote);
        }
        catch (SocketException ex)
        {
            _logger.LogDebug("[{Remote}] Socket error: {Message}", remote, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{Remote}] Error handling client", remote);
        }
        finally
        {
            try { client.Close(); } catch { /* ignore */ }
            _logger.LogDebug("[{Remote}] Disconnected", remote);
        }
    }

    private static async Task<byte[]?> ReadUntilEndMarkerAsync(Socket socket, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        var buffer = new byte[4096];

        while (!ct.IsCancellationRequested)
        {
            var received = await socket.ReceiveAsync(buffer, SocketFlags.None, ct);
            if (received == 0)
                break;

            ms.Write(buffer, 0, received);

            var length = (int)ms.Length;
            if (length >= EndMarkerBytes.Length)
            {
                if (EndsWithEndMarker(ms.GetBuffer(), length))
                {
                    var result = new byte[length - EndMarkerBytes.Length];
                    Array.Copy(ms.GetBuffer(), 0, result, 0, result.Length);
                    return result;
                }
            }
        }

        return null;
    }

    private static bool EndsWithEndMarker(byte[] data, int length)
    {
        if (length < EndMarkerBytes.Length)
            return false;

        for (var i = 0; i < EndMarkerBytes.Length; i++)
        {
            if (data[length - EndMarkerBytes.Length + i] != EndMarkerBytes[i])
                return false;
        }

        return true;
    }
}
