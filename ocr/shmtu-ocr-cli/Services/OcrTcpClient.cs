using System.Net.Sockets;
using System.Text;
using shmtu.captcha.ocr.cli.Models;

namespace shmtu.captcha.ocr.cli.Services;

/// <summary>
/// TCP 协议 OCR 客户端，连接远端 TCP 服务器进行验证码识别。
/// 协议：发送 image_bytes + "&lt;END&gt;"，服务器返回表达式字符串。
/// </summary>
public class OcrTcpClient
{
    private readonly string _host;
    private readonly int _port;

    private const string EndMarker = "<END>";
    private static readonly byte[] EndMarkerBytes = Encoding.UTF8.GetBytes(EndMarker);

    public OcrTcpClient(string host, int port)
    {
        _host = host;
        _port = port;
    }

    /// <summary>
    /// 通过 TCP 协议识别验证码图片
    /// </summary>
    public async Task<string> RecognizeAsync(byte[] imageData, int timeoutMs = 10000)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(_host, _port);

        using var stream = client.GetStream();
        stream.ReadTimeout = timeoutMs;
        stream.WriteTimeout = timeoutMs;

        // 发送图片数据 + 结束标记
        await stream.WriteAsync(imageData);
        await stream.WriteAsync(EndMarkerBytes);
        await stream.FlushAsync();

        // 读取响应
        using var ms = new MemoryStream();
        var buffer = new byte[4096];
        int bytesRead;

        while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            ms.Write(buffer, 0, bytesRead);
        }

        var result = Encoding.UTF8.GetString(ms.ToArray()).Trim();
        return result;
    }
}
