namespace shmtu.cas.captcha;

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Flurl.Http;

public static class Captcha
{
    // Read image from file
    public static byte[] ReadImageFromFile(string fileName)
    {
        // Read image from file
        using var fs = new FileStream(fileName, FileMode.Open);
        var imageBytes = new byte[fs.Length];

        // Try to Read the entire file
        var read = fs.Read(imageBytes, 0, imageBytes.Length);
        if (read != imageBytes.Length)
        {
            throw new Exception("Error reading image file.");
        }

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
        return System.Text.RegularExpressions.Regex.IsMatch(
            ip,
            @"^([01]?\\d\\d?|2[0-4]\\d|25[0-5])\\." +
            @"([01]?\d\d?|2[0-4]\d|25[0-5])\." +
            @"([01]?\d\d?|2[0-4]\d|25[0-5])\." +
            @"([01]?\d\d?|2[0-4]\d|25[0-5])$"
        );
    }

    // Get image data from URL
    [Obsolete("Obsolete because 'WebClient' is obsoleted.")]
    public static byte[] GetImageDataFromUrl(
        string imageUrl = "https://cas.shmtu.edu.cn/cas/captcha"
    )
    {
        using var client = new WebClient();
        return client.DownloadData(imageUrl);
    }

    public static async Task<byte[]> GetImageDataFromUrlAsync(
        string imageUrl = "https://cas.shmtu.edu.cn/cas/captcha"
    )
    {
        using var httpClient = new HttpClient();
        using var response =
            await httpClient.GetAsync(imageUrl, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync();
    }

    public static async Task<(byte[]?, string)>
        GetImageDataFromUrlUsingGet(string cookie = "")
    {
        const string imageUrl = "https://cas.shmtu.edu.cn/cas/captcha";

        try
        {
            var request = imageUrl
                .WithHeader("Cookie", cookie);
            var response = await request.GetAsync();

            var responseCode = (HttpStatusCode)response.StatusCode;
            
            if (responseCode != HttpStatusCode.OK)
            {
                Console.WriteLine($"请求失败，状态码：{response.StatusCode}");
                return (null, "");
            }

            // JSESSIONID是在获取验证码的过程中设置到浏览器的Cookie中的
            // 如果不存在更新JSESSIONID操作则直接返回原本传入的Cookie
            // 如果没有传入Cookie，一般服务器会Set-Cookie返回一个新的JSESSIONID
            // 因此一般不会出现Cookie为空的情况
            var returnCookie =
                response.ResponseMessage
                    .Headers
                    .GetValues("Set-Cookie")
                    .FirstOrDefault() ?? cookie;

            return (response.GetBytesAsync().Result, returnCookie);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"请求失败：{ex.Message}");
            return (null, "");
        }
    }

    // OCR by remote TCP server
    public static string OcrByRemoteTcpServer(string host, int port, byte[] imageData)
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        socket.Connect(host, port);
        socket.SendTimeout = 5000;

        var stream = new NetworkStream(socket);
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

    // Get expression result from expression string
    public static string GetExprResultByExprString(string expr)
    {
        var index = expr.IndexOf('=');

        if (index == -1) return "";
        if (!(0 < index + 1 && index + 1 <= expr.Length)) return "";

        return expr[(index + 1)..].Trim();
    }
}