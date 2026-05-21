using System.Diagnostics;
using shmtu.captcha.onnx;

Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.WriteLine($"SHMTU CAS Captcha ONNX OCR - CLI Demo (lib v{CaptchaOcrLib.Version})");

var modelDir = args.Length >= 2 ? args[1] : AppContext.BaseDirectory;
using var ocr = new CasOcr(modelDir);

Console.WriteLine($"Model directory: {ocr.ModelDirectoryPath}");

if (!ocr.CheckModelIsExist())
{
    Console.WriteLine("Models missing, downloading...");
    var lastReported = -1;
    var progress = new Progress<float>(p =>
    {
        var rounded = (int)p;
        if (rounded == lastReported) return;
        lastReported = rounded;
        Console.Write($"\rDownloading: {rounded,3}%");
    });
    var ok = await ocr.EnsureModelsAsync(progress);
    Console.WriteLine();
    if (!ok)
    {
        Console.Error.WriteLine("Failed to download models.");
        return 1;
    }
}

if (!ocr.LoadModel())
{
    Console.Error.WriteLine("Failed to load models.");
    return 1;
}

if (args.Length >= 1)
{
    var path = args[0];
    if (!File.Exists(path))
    {
        Console.Error.WriteLine($"Image not found: {path}");
        return 1;
    }

    var sw = Stopwatch.StartNew();
    var r = ocr.PredictValidateCode(path);
    sw.Stop();
    Console.WriteLine($"[{Path.GetFileName(path)}] {r.Expr}  (elapsed {sw.ElapsedMilliseconds} ms)");
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
        Console.WriteLine($"No images in {samplesDir}.");
    }
    else
    {
        Console.WriteLine($"Batch testing {files.Length} image(s) in {samplesDir}:");
        var totalMs = 0L;
        foreach (var f in files)
        {
            var sw = Stopwatch.StartNew();
            var r = ocr.PredictValidateCode(f);
            sw.Stop();
            totalMs += sw.ElapsedMilliseconds;
            Console.WriteLine($"  [{Path.GetFileName(f),-32}] {r.Expr}  ({sw.ElapsedMilliseconds} ms)");
        }
        Console.WriteLine($"Average: {(double)totalMs / files.Length:F1} ms / image");
    }
}
else
{
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine("  shmtu-captcha-onnx-cli <imagePath> [modelDir]");
    Console.WriteLine("Or put sample images under ./samples/ for batch testing.");
}

return 0;
