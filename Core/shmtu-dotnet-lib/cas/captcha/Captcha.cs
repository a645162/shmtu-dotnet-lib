using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using shmtu.cas.auth.common;

namespace shmtu.cas.captcha;

/// <summary>
/// 验证码相关工具。HTTP 部分对齐 Rust 的 <c>captcha::fetch_captcha</c>，
/// 算式部分对齐 <c>captcha::get_expr_result</c>。
/// </summary>
public static class Captcha
{
    public const string CaptchaUrl = "https://cas.shmtu.edu.cn/cas/captcha";

    // Read image from file
    public static byte[] ReadImageFromFile(string fileName)
    {
        using var fs = new FileStream(fileName, FileMode.Open);
        var imageBytes = new byte[fs.Length];

        var read = fs.Read(imageBytes, 0, imageBytes.Length);
        if (read != imageBytes.Length) throw new Exception("Error reading image file.");

        return imageBytes;
    }

    // Save image to file
    public static void SaveImageToFile(byte[] imageData, string directoryPath = ".")
    {
        var currentDateTime = DateTime.Now.ToString("yyyyMMddHHmmss");
        var fileName = $"captcha_{currentDateTime}.png";
        var filePath = Path.Combine(directoryPath, fileName);
        File.WriteAllBytes(filePath, imageData);
        Console.WriteLine($"Image saved to file: {fileName}");
    }

    // Check IP address
    public static bool ValidateIpAddress(string ip)
    {
        return Regex.IsMatch(
            ip,
            @"^([01]?\d\d?|2[0-4]\d|25[0-5])\." +
            @"([01]?\d\d?|2[0-4]\d|25[0-5])\." +
            @"([01]?\d\d?|2[0-4]\d|25[0-5])\." +
            @"([01]?\d\d?|2[0-4]\d|25[0-5])$"
        );
    }

    /// <summary>
    /// 无 cookie 的简单拉取，主要给独立的工具脚本使用。
    /// </summary>
    public static async Task<byte[]> GetImageDataFromUrlAsync(
        string imageUrl = CaptchaUrl,
        CancellationToken cancellationToken = default)
    {
        using var httpClient = new HttpClient();
        using var response = await httpClient.GetAsync(
            imageUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    /// <summary>
    /// 拉取验证码图片字节。对齐 Rust 的 <c>captcha::fetch_captcha</c>。
    /// Cookie 自动通过 <see cref="CasHttpClient"/> 的 CookieContainer 管理，调用方不再需要传 cookie。
    /// </summary>
    public static async Task<byte[]> FetchCaptcha(
        CasHttpClient client,
        CancellationToken cancellationToken = default)
    {
        using var response = await client.HttpClient.GetAsync(CaptchaUrl, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"获取验证码失败，状态码: {(int)response.StatusCode}");
        }
        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    /// <summary>
    /// 通过远端 TCP OCR 服务识别验证码（同步版本）。
    /// </summary>
    public static string OcrByRemoteTcpServer(string host, int port, byte[] imageData)
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        socket.Connect(host, port);
        socket.SendTimeout = 5000;

        using var stream = new NetworkStream(socket);
        stream.Write(imageData, 0, imageData.Length);
        stream.Flush();

        var endMarker = "<END>"u8.ToArray();
        stream.Write(endMarker, 0, endMarker.Length);
        stream.Flush();

        var buffer = new byte[1024];
        var bytesRead = stream.Read(buffer, 0, buffer.Length);
        var response = Encoding.UTF8.GetString(buffer, 0, bytesRead);

        return response.Trim();
    }

    /// <summary>
    /// 通过远端 TCP OCR 服务识别验证码（异步版本）。
    /// </summary>
    public static async Task<string> OcrByRemoteTcpServerAsync(string host, int port, byte[] imageData)
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        await socket.ConnectAsync(host, port);
        socket.SendTimeout = 5000;

        await using var stream = new NetworkStream(socket);
        await stream.WriteAsync(imageData);

        var endMarker = "<END>"u8.ToArray();
        await stream.WriteAsync(endMarker);

        var buffer = new byte[1024];
        var bytesRead = await stream.ReadAsync(buffer);
        var response = Encoding.UTF8.GetString(buffer, 0, bytesRead);

        return response.Trim();
    }

    /// <summary>
    /// 把 "12+34=46" 这样的算式取右侧答案 "46"；找不到 "=" 则返回空串。
    /// 对齐 Rust 的 <c>captcha::get_expr_result</c>（注：Rust 找不到时返回 trim 后的整串）。
    /// </summary>
    public static string GetExprResult(string expr)
    {
        var index = expr.IndexOf('=');
        if (index == -1) return expr.Trim();
        if (index + 1 > expr.Length) return "";
        return expr[(index + 1)..].Trim();
    }

    /// <summary>历史名称，保留作为别名。</summary>
    public static string GetExprResultByExprString(string expr) => GetExprResult(expr);
}
