using System.Diagnostics;
using shmtu.captcha.onnx;

Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.WriteLine($"上海海事大学验证码 OCR - 命令行演示 (库版本 v{CaptchaOcrLib.Version})");

var modelDir = args.Length >= 2 ? args[1] : AppContext.BaseDirectory;
using var ocr = new CasOcr(modelDir);

Console.WriteLine($"模型目录：{ocr.ModelDirectoryPath}");

if (!ocr.CheckModelIsExist())
{
    Console.WriteLine("模型缺失，开始下载...");
    var lastReported = -1;
    var progress = new Progress<float>(p =>
    {
        var rounded = (int)p;
        if (rounded == lastReported) return;
        lastReported = rounded;
        Console.Write($"\r下载进度：{rounded,3}%");
    });
    var ok = await ocr.EnsureModelsAsync(progress);
    Console.WriteLine();
    if (!ok)
    {
        Console.Error.WriteLine("模型下载失败。");
        return 1;
    }
}

if (!ocr.LoadModel())
{
    Console.Error.WriteLine("模型加载失败。");
    return 1;
}

if (args.Length >= 1)
{
    var path = args[0];
    if (!File.Exists(path))
    {
        Console.Error.WriteLine($"找不到图片文件：{path}");
        return 1;
    }

    var sw = Stopwatch.StartNew();
    var r = ocr.PredictValidateCode(path);
    sw.Stop();
    Console.WriteLine($"[{Path.GetFileName(path)}] {r.Expr}  （耗时 {sw.ElapsedMilliseconds} 毫秒）");
    return 0;
}

// 无参时：批量识别 ./samples 下所有 png/jpg 作为冒烟测试
var samplesDir = Path.Combine(AppContext.BaseDirectory, "samples");
if (Directory.Exists(samplesDir))
{
    var files = Directory.GetFiles(samplesDir, "*.*")
        .Where(f => f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                    f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                    f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
        .OrderBy(f => f)
        .ToArray();

    if (files.Length == 0)
    {
        Console.WriteLine($"{samplesDir} 目录下未找到任何图片。");
    }
    else
    {
        Console.WriteLine($"批量识别 {files.Length} 张图片（{samplesDir}）：");
        var totalMs = 0L;
        foreach (var f in files)
        {
            var sw = Stopwatch.StartNew();
            var r = ocr.PredictValidateCode(f);
            sw.Stop();
            totalMs += sw.ElapsedMilliseconds;
            Console.WriteLine($"  [{Path.GetFileName(f),-32}] {r.Expr}  （{sw.ElapsedMilliseconds} 毫秒）");
        }
        Console.WriteLine($"平均：{(double)totalMs / files.Length:F1} 毫秒 / 张");
    }
}
else
{
    Console.WriteLine();
    Console.WriteLine("用法：");
    Console.WriteLine("  shmtu-ocr-onnx-demo <图片路径> [模型目录]");
    Console.WriteLine("或者将示例图片放置在 ./samples/ 目录下，直接运行即可批量识别。");
}

return 0;
