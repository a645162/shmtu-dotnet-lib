using shmtu.cas;
using shmtu.cas.captcha;

// See https://aka.ms/new-console-template for more information

Console.WriteLine("ShangHai Maritime University CAS .NET Library Demo");
Console.WriteLine($"Library Version: {ShmtuCasDotnet.Version}");

// 测试本地TCP服务器OCR
await Captcha.TestLocalTcpServerOcr("127.0.0.1", 21601);