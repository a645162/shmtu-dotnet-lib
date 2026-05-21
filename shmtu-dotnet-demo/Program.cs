using shmtu;
using shmtu.cas.demo.bill;

// See https://aka.ms/new-console-template for more information

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

Console.WriteLine($"User ID: {userId} Password: {new string('*', password.Length)}");
Console.WriteLine($"OCR Host: {ocrHost} Port: {ocrPort}");

// 测试本地TCP服务器OCR
// await CaptchaDemo.TestLocalTcpServerOcr(ocrHost, ocrPort);
// await CaptchaDemo.TestGetImageAndCookie();

await BillDemo.TestBill(userId, password, ocrHost, ocrPort);

BillItemDemo.TestBillItem();