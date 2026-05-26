using System.Diagnostics;
using shmtu.captcha.onnx;
using shmtu.captcha.ocr.cli.Services;

Console.OutputEncoding = System.Text.Encoding.UTF8;

// 简单命令行解析
if (args.Length == 0)
{
    PrintUsage();
    return 0;
}

var command = args[0].ToLowerInvariant();

try
{
    return command switch
    {
        "image" => await HandleImageCommand(args[1..]),
        "test" => await HandleTestCommand(args[1..]),
        "health" => await HandleHealthCommand(args[1..]),
        "download-model" => await HandleDownloadModelCommand(args[1..]),
        _ => HandleUnknownCommand(command)
    };
}
catch (Exception ex)
{
    Console.Error.WriteLine($"错误: {ex.Message}");
    return 1;
}

// === 命令处理 ===

async Task<int> HandleImageCommand(string[] args)
{
    var imagePath = GetPositionalArg(args, 0);
    if (string.IsNullOrEmpty(imagePath))
        imagePath = GetOption(args, "--image", "");
    var mode = GetOption(args, "--mode", "local");
    var host = GetOption(args, "--host", "127.0.0.1");
    var port = int.Parse(GetOption(args, "--port", "21601"));
    var url = GetOption(args, "--url", "http://127.0.0.1:21600");
    var modelDir = GetOption(args, "--model-dir", AppContext.BaseDirectory);

    if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
    {
        Console.Error.WriteLine($"找不到图片文件: {imagePath}");
        return 1;
    }

    var imageData = await File.ReadAllBytesAsync(imagePath);
    var sw = Stopwatch.StartNew();
    string expression;

    switch (mode.ToLowerInvariant())
    {
        case "local":
            expression = await RecognizeLocalAsync(imageData, modelDir);
            break;
        case "tcp":
            expression = await RecognizeTcpAsync(imageData, host, port);
            break;
        case "restful":
            expression = await RecognizeRestfulAsync(imageData, url);
            break;
        default:
            Console.Error.WriteLine($"不支持的识别模式: {mode} (可选: local, tcp, restful)");
            return 1;
    }

    sw.Stop();
    Console.WriteLine($"识别结果: {expression}  （耗时 {sw.ElapsedMilliseconds} 毫秒）");

    // 尝试计算验证码答案
    var answer = GetExprResult(expression);
    if (answer != expression)
    {
        Console.WriteLine($"验证码答案: {answer}");
    }

    return 0;
}

async Task<int> HandleTestCommand(string[] args)
{
    var mode = GetOption(args, "--mode", "tcp");
    var host = GetOption(args, "--host", "127.0.0.1");
    var port = int.Parse(GetOption(args, "--port", "21601"));
    var url = GetOption(args, "--url", "http://127.0.0.1:21600");
    var rounds = int.Parse(GetOption(args, "--rounds", "10"));

    Console.WriteLine($"验证码识别测试 - 模式: {mode}, 轮次: {rounds}");

    int success = 0;
    long totalMs = 0;

    for (int i = 1; i <= rounds; i++)
    {
        try
        {
            var sw = Stopwatch.StartNew();

            // 从 CAS 获取验证码图片
            var imageData = await FetchCaptchaFromCasAsync();
            Console.WriteLine($"第 {i}/{rounds} 轮: 获取验证码 {imageData.Length} bytes");

            string expression;
            switch (mode.ToLowerInvariant())
            {
                case "tcp":
                    expression = await RecognizeTcpAsync(imageData, host, port);
                    break;
                case "restful":
                    expression = await RecognizeRestfulAsync(imageData, url);
                    break;
                default:
                    Console.Error.WriteLine($"测试模式不支持 local，请使用 tcp 或 restful");
                    return 1;
            }

            sw.Stop();
            totalMs += sw.ElapsedMilliseconds;
            success++;

            var answer = GetExprResult(expression);
            Console.WriteLine($"  结果: {expression} -> {answer}  （{sw.ElapsedMilliseconds} 毫秒）");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  第 {i} 轮失败: {ex.Message}");
        }
    }

    Console.WriteLine();
    Console.WriteLine($"测试完成: 成功 {success}/{rounds}");
    if (success > 0)
    {
        Console.WriteLine($"平均耗时: {(double)totalMs / success:F1} 毫秒");
    }

    return 0;
}

async Task<int> HandleHealthCommand(string[] args)
{
    var url = GetOption(args, "--url", "http://127.0.0.1:21600");

    using var client = new OcrHttpClient(url);
    var health = await client.HealthCheckAsync();

    Console.WriteLine($"状态: {health.Status}");
    Console.WriteLine($"模型已加载: {health.ModelsLoaded}");
    Console.WriteLine($"连接池大小: {health.PoolSize}");

    return health.Status.Equals("Healthy", StringComparison.OrdinalIgnoreCase) ? 0 : 1;
}

async Task<int> HandleDownloadModelCommand(string[] args)
{
    var modelDir = GetOption(args, "--model-dir", AppContext.BaseDirectory);

    using var ocr = new CasOcr(modelDir);

    if (ocr.CheckModelIsExist())
    {
        Console.WriteLine("模型已存在，无需下载。");
        return 0;
    }

    Console.WriteLine("开始下载模型...");
    var lastReported = -1;
    var progress = new Progress<float>(p =>
    {
        var rounded = (int)p;
        if (rounded == lastReported) return;
        lastReported = rounded;
        Console.Write($"\r下载进度: {rounded,3}%");
    });

    var ok = await ocr.EnsureModelsAsync(progress);
    Console.WriteLine();

    if (ok)
    {
        Console.WriteLine("模型下载成功！");
        return 0;
    }
    else
    {
        Console.Error.WriteLine("模型下载失败。");
        return 1;
    }
}

int HandleUnknownCommand(string command)
{
    Console.Error.WriteLine($"未知命令: {command}");
    PrintUsage();
    return 1;
}

// === 识别方法 ===

async Task<string> RecognizeLocalAsync(byte[] imageData, string modelDir)
{
    using var ocr = new CasOcr(modelDir);

    if (!ocr.CheckModelIsExist())
    {
        throw new InvalidOperationException("本地模型不存在，请先运行 download-model 命令下载模型");
    }

    if (!ocr.LoadModel())
    {
        throw new InvalidOperationException("本地模型加载失败");
    }

    var result = ocr.PredictValidateCode(imageData);
    return result.Expr;
}

async Task<string> RecognizeTcpAsync(byte[] imageData, string host, int port)
{
    var client = new OcrTcpClient(host, port);
    return await client.RecognizeAsync(imageData);
}

async Task<string> RecognizeRestfulAsync(byte[] imageData, string url)
{
    using var client = new OcrHttpClient(url);
    var response = await client.RecognizeAsync(imageData);

    if (!response.Success)
    {
        throw new InvalidOperationException($"OCR 识别失败: {response.Error ?? "未知错误"}");
    }

    return response.Expression;
}

// === 工具方法 ===

/// <summary>
/// 从 CAS 服务器获取验证码图片
/// </summary>
async Task<byte[]> FetchCaptchaFromCasAsync()
{
    using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
    var response = await httpClient.GetAsync("https://cas.shmtu.edu.cn/cas/captcha");
    response.EnsureSuccessStatusCode();
    return await response.Content.ReadAsByteArrayAsync();
}

/// <summary>
/// 从算式字符串中提取答案（如 "3+5=8" → "8"）
/// </summary>
static string GetExprResult(string expr)
{
    var trimmed = expr.Trim();
    var eqPos = trimmed.LastIndexOf('=');
    if (eqPos >= 0 && eqPos < trimmed.Length - 1)
    {
        return trimmed[(eqPos + 1)..].Trim();
    }
    return trimmed;
}

/// <summary>
/// 从命令行参数中获取命名选项值（如 --mode tcp → "tcp"）
/// </summary>
static string GetOption(string[] args, string option, string defaultValue)
{
    for (int i = 0; i < args.Length - 1; i++)
    {
        if (args[i] == option)
        {
            return args[i + 1];
        }
    }
    return defaultValue;
}

/// <summary>
/// 从命令行参数中获取位置参数
/// </summary>
static string GetPositionalArg(string[] args, int position)
{
    var positionalArgs = args.Where(a => !a.StartsWith("-")).ToArray();
    return positionalArgs.Length > position ? positionalArgs[position] : "";
}

void PrintUsage()
{
    Console.WriteLine("上海海事大学验证码 OCR 命令行工具");
    Console.WriteLine();
    Console.WriteLine("用法:");
    Console.WriteLine("  shmtu-ocr-cli image <path> --mode local|tcp|restful [--host 127.0.0.1] [--port 21601] [--url http://127.0.0.1:21600]");
    Console.WriteLine("  shmtu-ocr-cli test --mode tcp|restful [--host 127.0.0.1] [--port 21601] [--url http://127.0.0.1:21600] [--rounds 10]");
    Console.WriteLine("  shmtu-ocr-cli health --url http://127.0.0.1:21600");
    Console.WriteLine("  shmtu-ocr-cli download-model [--model-dir ./models]");
    Console.WriteLine();
    Console.WriteLine("命令:");
    Console.WriteLine("  image <path>       识别验证码图片");
    Console.WriteLine("  test               从 CAS 获取验证码并测试识别");
    Console.WriteLine("  health             检查 RESTful OCR 服务器健康状态");
    Console.WriteLine("  download-model     下载本地 ONNX 模型");
    Console.WriteLine();
    Console.WriteLine("选项:");
    Console.WriteLine("  --mode             识别模式: local(本地ONNX), tcp(TCP远端), restful(RESTful远端)");
    Console.WriteLine("  --host             TCP 服务器地址 (默认: 127.0.0.1)");
    Console.WriteLine("  --port             TCP 服务器端口 (默认: 21601)");
    Console.WriteLine("  --url              RESTful 服务器地址 (默认: http://127.0.0.1:21600)");
    Console.WriteLine("  --rounds           测试轮次 (默认: 10)");
    Console.WriteLine("  --model-dir        本地模型目录");
}
