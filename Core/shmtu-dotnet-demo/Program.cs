using shmtu;
using shmtu.cas.captcha;
using shmtu.cas.demo.bill;
using shmtu.cas.demo.cas.captcha;

Console.WriteLine("ShangHai Maritime University CAS .NET Library Demo");
Console.WriteLine($"Library Version: {ShmtuDotnetLib.Version}");

// Get Env Var
var userId = Environment.GetEnvironmentVariable("SHMTU_USER_ID")
    ?? Environment.GetEnvironmentVariable("SHMTU_USERNAME")
    ?? "";
var password = Environment.GetEnvironmentVariable("SHMTU_PASSWORD") ?? "";
var ocrHost = Environment.GetEnvironmentVariable("SHMTU_OCR_HOST") ?? "127.0.0.1";
var ocrPortStr = Environment.GetEnvironmentVariable("SHMTU_OCR_PORT") ?? "21601";
var ocrPort = int.TryParse(ocrPortStr, out var port) ? port : 21601;
var captchaMode = (Environment.GetEnvironmentVariable("SHMTU_CAPTCHA_MODE") ?? "ocr").ToLowerInvariant();

Console.WriteLine($"User ID: {userId} Password: {new string('*', password.Length)}");
Console.WriteLine($"Captcha Mode: {captchaMode}");

ICaptchaResolver resolver = captchaMode switch
{
    "manual" or "cli" => CliManualCaptchaResolverFactory.Create(),
    _ => new RemoteOcrCaptchaResolver(ocrHost, ocrPort)
};

if (resolver is RemoteOcrCaptchaResolver remote)
    Console.WriteLine($"OCR Host: {remote.Host} Port: {remote.Port}");

await BillDemo.TestBill(userId, password, resolver);

BillItemDemo.TestBillItem();
