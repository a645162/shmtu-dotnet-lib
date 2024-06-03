namespace shmtu.cas.captcha;

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

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
        using var response = await httpClient.GetAsync(imageUrl, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync();
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

    // Test local TCP server OCR
    public static async Task TestLocalTcpServerOcr(string ip, int port)
    {
        Console.WriteLine("识别验证码 Test");
        var imageData = await GetImageDataFromUrlAsync();

        if (imageData.Length == 0)
        {
            Console.WriteLine("获取验证码失败");
            return;
        }

        var startTime = DateTime.Now;
        var validateCode = OcrByRemoteTcpServer(ip, port, imageData);
        var executionTime = DateTime.Now - startTime;
        Console.WriteLine($"OCR执行时间: {executionTime.TotalMilliseconds} 毫秒");

        var exprResult = GetExprResultByExprString(validateCode);
        Console.WriteLine(validateCode);
        Console.WriteLine(exprResult);

        SaveImageToFile(imageData);
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