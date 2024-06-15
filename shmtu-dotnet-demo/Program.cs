using shmtu;
using shmtu.cas;
using shmtu.cas.auth.common;
using shmtu.cas.captcha;
using shmtu.cas.demo;

// See https://aka.ms/new-console-template for more information

Console.WriteLine("ShangHai Maritime University CAS .NET Library Demo");
Console.WriteLine($"Library Version: {ShmtuDotnetLib.Version}");

// Get Env Var
var userId = Environment.GetEnvironmentVariable("SHMTU_USER_ID") ?? "";
var password = Environment.GetEnvironmentVariable("SHMTU_PASSWORD") ?? "";
Console.WriteLine($"User ID: {userId} Password: {password}");

// 测试本地TCP服务器OCR
// await CaptchaDemo.TestLocalTcpServerOcr("127.0.0.1", 21601);
// await CaptchaDemo.TestGetImageAndCookie();

await BillDemo.TestBill(userId, password);