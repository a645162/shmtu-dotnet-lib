using shmtu.cas.auth.common;
using shmtu.cas.captcha;

namespace shmtu.cas.demo.cas;

public static class CaptchaDemo
{
    // Test local TCP server OCR
    public static async Task TestLocalTcpServerOcr(string ip, int port)
    {
        Console.WriteLine("识别验证码 Test");
        var imageData = await Captcha.GetImageDataFromUrlAsync();

        if (imageData.Length == 0)
        {
            Console.WriteLine("获取验证码失败");
            return;
        }

        var startTime = DateTime.Now;
        var validateCode = Captcha.OcrByRemoteTcpServer(ip, port, imageData);
        var executionTime = DateTime.Now - startTime;
        Console.WriteLine($"OCR执行时间: {executionTime.TotalMilliseconds} 毫秒");

        var exprResult = Captcha.GetExprResult(validateCode);
        Console.WriteLine(validateCode);
        Console.WriteLine(exprResult);

        Captcha.SaveImageToFile(imageData);
    }

    public static async Task TestGetImageAndCookie()
    {
        using var client = new CasHttpClient();
        var imageData = await Captcha.FetchCaptcha(client);
        if (imageData.Length == 0)
        {
            Console.WriteLine("获取验证码图片失败");
            return;
        }

        Captcha.SaveImageToFile(imageData);
        Console.WriteLine($"CookieContainer 中已记录 {client.CookieContainer.Count} 条 cookie");
    }
}
