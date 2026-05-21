using System.Net.Sockets;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace shmtu.captcha.onnx.tests;

public sealed class OcrConsistencyTests : IAsyncLifetime, IDisposable
{
    private readonly ITestOutputHelper _out;
    private CasOcr? _ocr;
    private string? _host;
    private int _port;
    private bool _remoteConfigured;

    public OcrConsistencyTests(ITestOutputHelper output) => _out = output;

    public async Task InitializeAsync()
    {
        _host = Environment.GetEnvironmentVariable("SHMTU_OCR_HOST");
        var portStr = Environment.GetEnvironmentVariable("SHMTU_OCR_PORT");
        _port = int.TryParse(portStr, out var p) ? p : 21601;
        _remoteConfigured = !string.IsNullOrWhiteSpace(_host);

        _ocr = new CasOcr();
        if (!_ocr.CheckModelIsExist())
        {
            _out.WriteLine("Downloading ONNX models to {0}...", _ocr.ModelDirectoryPath);
            var ok = await _ocr.EnsureModelsAsync(new Progress<float>(v => _out.WriteLine("  {0}%", (int)v)));
            if (!ok) throw new InvalidOperationException("Failed to obtain ONNX models.");
        }
        if (!_ocr.LoadModel()) throw new InvalidOperationException("Failed to load ONNX models.");
        if (_remoteConfigured)
            _out.WriteLine("Models loaded; comparing against {0}:{1}", _host, _port);
        else
            _out.WriteLine("Models loaded; remote OCR not configured (SHMTU_OCR_HOST empty).");
    }

    public static IEnumerable<object[]> SampleImages()
    {
        var samples = Path.Combine(AppContext.BaseDirectory, "Samples");
        if (!Directory.Exists(samples)) yield break;
        foreach (var f in Directory.GetFiles(samples, "*.png").OrderBy(f => f))
            yield return new object[] { f };
    }

    [SkippableTheory]
    [MemberData(nameof(SampleImages))]
    public async Task LocalOnnx_MatchesRemoteTcpServer(string imagePath)
    {
        Skip.IfNot(_remoteConfigured, "SHMTU_OCR_HOST not set; skip remote comparison.");

        var bytes = await File.ReadAllBytesAsync(imagePath);
        var local = _ocr!.PredictValidateCode(bytes);

        string remote;
        try
        {
            remote = await RemoteOcrAsync(_host!, _port, bytes);
        }
        catch (Exception ex)
        {
            Skip.If(true, $"Remote OCR unavailable: {ex.Message}");
            return;
        }

        _out.WriteLine("{0,-38} | LOCAL: {1,-22} | REMOTE: {2,-22}",
            Path.GetFileName(imagePath), local.Expr, remote);

        var localResult = ExtractResult(local.Expr);
        var remoteResult = ExtractResult(remote);

        Assert.True(int.TryParse(localResult, out var l),  $"Local result not int: '{localResult}'");
        Assert.True(int.TryParse(remoteResult, out var r), $"Remote result not int: '{remoteResult}'");
        Assert.Equal(r, l);
    }

    [Theory]
    [MemberData(nameof(SampleImages))]
    public async Task LocalOnnx_ProducesNumericResult(string imagePath)
    {
        var bytes = await File.ReadAllBytesAsync(imagePath);
        var local = _ocr!.PredictValidateCode(bytes);
        _out.WriteLine("{0,-38} | {1}", Path.GetFileName(imagePath), local.Expr);
        Assert.NotEqual(-1, local.Result);
        Assert.False(string.IsNullOrWhiteSpace(local.Expr));
    }

    public void Dispose() => _ocr?.Dispose();
    Task IAsyncLifetime.DisposeAsync() => Task.CompletedTask;

    private static async Task<string> RemoteOcrAsync(string host, int port, byte[] imageData)
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        using var cts = new CancellationTokenSource(5000);
        await socket.ConnectAsync(host, port, cts.Token);
        socket.SendTimeout = 5000;
        socket.ReceiveTimeout = 8000;

        using var stream = new NetworkStream(socket);
        await stream.WriteAsync(imageData);
        var endMarker = "<END>"u8.ToArray();
        await stream.WriteAsync(endMarker);
        await stream.FlushAsync();

        var buffer = new byte[1024];
        var read = await stream.ReadAsync(buffer);
        return Encoding.UTF8.GetString(buffer, 0, read).Trim();
    }

    private static string ExtractResult(string expr)
    {
        var idx = expr.IndexOf('=');
        return idx >= 0 && idx + 1 < expr.Length ? expr[(idx + 1)..].Trim() : expr.Trim();
    }
}
