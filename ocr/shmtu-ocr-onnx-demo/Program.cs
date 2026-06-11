using System.Diagnostics;
using shmtu.captcha.onnx;
using shmtu.captcha.onnx.Backend;

Console.OutputEncoding = System.Text.Encoding.UTF8;

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
        "list-tags" => await HandleListTagsCommand(args[1..]),
        "list-models" => await HandleListModelsCommand(args[1..]),
        "download" => await HandleDownloadCommand(args[1..]),
        "recognize" or "image" or "predict" => await HandleRecognizeCommand(args[1..]),
        "help" or "--help" or "-h" => PrintUsageAndReturn(),
        _ => HandleUnknownCommand(command)
    };
}
catch (Exception ex)
{
    Console.Error.WriteLine($"错误: {ex.Message}");
    return 1;
}

// ==================== 命令处理 ====================

async Task<int> HandleListTagsCommand(string[] args)
{
    // 列出 v2 兼容范围内 (默认 v2.0.x) 解析到的最新 release tag。
    var maxMinorStr = GetOption(args, "--max-minor", ConstValue.V2.MaxSupportedMinor.ToString());
    if (!uint.TryParse(maxMinorStr, out var maxMinor))
    {
        Console.Error.WriteLine($"无效的 --max-minor: {maxMinorStr}");
        return 1;
    }

    Console.WriteLine($"[v2] 解析最新 release tag (maxMinor={maxMinor})...");
    var tag = await V2Downloader.ResolveLatestTagAsync(
        ConstValue.V2.MaxSupportedMajor,
        maxMinor,
        ConstValue.V2.DefaultTag,
        log: msg => Console.WriteLine(msg));

    Console.WriteLine();
    Console.WriteLine($"当前解析到的 v2 最新 tag: {tag}");
    Console.WriteLine($"默认 fallback tag:          {ConstValue.V2.DefaultTag}");
    return 0;
}

async Task<int> HandleListModelsCommand(string[] args)
{
    var version = ParseVersion(GetOption(args, "--version", "v2"));
    if (version != ConstValue.ModelVersion.V2)
    {
        Console.Error.WriteLine("list-models 仅支持 --version v2（v1 为硬编码 3 模型列表）。");
        return 1;
    }

    var tag = GetOption(args, "--tag", null);
    if (string.IsNullOrWhiteSpace(tag))
    {
        Console.WriteLine("[v2] 未指定 --tag,自动解析最新 tag...");
        tag = await V2Downloader.ResolveLatestTagAsync(
            ConstValue.V2.MaxSupportedMajor,
            ConstValue.V2.MaxSupportedMinor,
            ConstValue.V2.DefaultTag,
            log: msg => Console.WriteLine(msg));
    }

    Console.WriteLine($"[v2] 列出 tag={tag} manifest 中所有可用模型...");

    var models = await CasOnnxBackendV2.ListAvailableModelsAsync(tag, log: msg => Console.WriteLine(msg));

    Console.WriteLine();
    if (models.Count == 0)
    {
        Console.WriteLine("未获取到任何模型条目。");
        return 1;
    }

    Console.WriteLine($"共 {models.Count} 个模型条目:");
    Console.WriteLine();
    foreach (var m in models)
    {
        var backbones = m.SupportedBackbones != null && m.SupportedBackbones.Count > 0
            ? string.Join(",", m.SupportedBackbones)
            : (string.IsNullOrEmpty(m.Backbone) ? "-" : m.Backbone);
        var precisionList = m.Artifacts != null
            ? string.Join(",", m.Artifacts.Values
                .SelectMany(d => d.Keys)
                .Distinct())
            : "-";
        Console.WriteLine($"- {m.AssetStem}");
        Console.WriteLine($"    display:    {m.DisplayName}");
        Console.WriteLine($"    backbone:   {backbones}");
        Console.WriteLine($"    family:     {m.Family}");
        Console.WriteLine($"    version:    {m.Version}");
        Console.WriteLine($"    size(M):    {(m.ModelSizeM.HasValue ? m.ModelSizeM.Value.ToString("F2") : "-")}");
        Console.WriteLine($"    precision:  {precisionList}");
    }

    return 0;
}

async Task<int> HandleDownloadCommand(string[] args)
{
    var version = ParseVersion(GetOption(args, "--version", "v2"));
    var modelDir = ResolveModelDir(GetOption(args, "--model-dir", null));
    var tag = GetOption(args, "--tag", null);
    var backbone = GetOption(args, "--backbone", ConstValue.V2.DefaultBackbone);
    var precision = GetOption(args, "--precision", ConstValue.V2.DefaultPrecision);

    Console.WriteLine($"[下载] version={version} modelDir={modelDir}");
    if (version == ConstValue.ModelVersion.V2)
    {
        Console.WriteLine($"[下载] tag={tag ?? "(自动解析最新 v2 tag)"} backbone={backbone} precision={precision}");
    }

    using var ocr = new CasOcr(modelDir, version: version);

    if (ocr.CheckModelIsExist())
    {
        Console.WriteLine("模型已存在，无需下载。");
        return 0;
    }

    var lastReported = -1;
    var progress = new Progress<float>(p =>
    {
        var rounded = (int)p;
        if (rounded == lastReported) return;
        lastReported = rounded;
        Console.Write($"\r下载进度：{rounded,3}%");
    });

    // v1 由 CasOcr 内部走 CasOnnxBackendV1.DownloadModelAsync;
    // v2 默认走 CasOcr.EnsureModelsAsync (内部用 ConstValue.V2.DefaultBackbone/Precision).
    // 当用户显式指定了 backbone/precision 但 CheckModelIsExist 已 true,不需要再下;
    // 若不存在,我们提供单独的精确入口:V2Downloader.DownloadAsync.
    var ok = false;
    if (version == ConstValue.ModelVersion.V2 &&
        (backbone != ConstValue.V2.DefaultBackbone || precision != ConstValue.V2.DefaultPrecision))
    {
        ok = await V2Downloader.DownloadAsync(
            modelDir,
            tag,
            backbone,
            precision,
            progress,
            log: msg => Console.WriteLine($"\n[log] {msg}"));
    }
    else
    {
        ok = await ocr.EnsureModelsAsync(progress,
            log: msg => Console.WriteLine($"\n[log] {msg}"));
    }
    Console.WriteLine();

    if (ok)
    {
        Console.WriteLine("模型下载成功！");
        return 0;
    }

    Console.Error.WriteLine("模型下载失败。");
    return 1;
}

async Task<int> HandleRecognizeCommand(string[] args)
{
    var imagePath = GetPositionalArg(args, 0);
    if (string.IsNullOrEmpty(imagePath))
    {
        imagePath = GetOption(args, "--image", "");
    }
    if (string.IsNullOrEmpty(imagePath))
    {
        Console.Error.WriteLine("请提供图片路径（位置参数或 --image）。");
        return 1;
    }

    var version = ParseVersion(GetOption(args, "--version", "v2"));
    var modelDir = ResolveModelDir(GetOption(args, "--model-dir", null));

    if (!File.Exists(imagePath))
    {
        Console.Error.WriteLine($"找不到图片文件：{imagePath}");
        return 1;
    }

    Console.WriteLine($"上海海事大学验证码 OCR - 命令行演示 (库版本 v{CaptchaOcrLib.Version})");
    Console.WriteLine($"使用版本：{version}, 模型目录：{modelDir}");

    using var ocr = new CasOcr(modelDir, version: version);

    Console.WriteLine($"当前 backend：{ocr.BackendName}");

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
        var ok = await ocr.EnsureModelsAsync(progress,
            log: msg => Console.WriteLine($"\n[log] {msg}"));
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

    var sw = Stopwatch.StartNew();
    var r = ocr.PredictValidateCode(imagePath);
    sw.Stop();
    Console.WriteLine($"[{Path.GetFileName(imagePath)}] {r.Expr}  （耗时 {sw.ElapsedMilliseconds} 毫秒）");
    return 0;
}

int HandleUnknownCommand(string command)
{
    Console.Error.WriteLine($"未知命令：{command}");
    PrintUsage();
    return 1;
}

int PrintUsageAndReturn()
{
    PrintUsage();
    return 0;
}

// ==================== 工具方法 ====================

/// <summary>
/// 解析 --version,支持 v1 / v2 (大小写不敏感);默认 v2。
/// </summary>
static ConstValue.ModelVersion ParseVersion(string? raw)
{
    if (string.IsNullOrWhiteSpace(raw)) return ConstValue.DefaultVersion;
    var v = raw.Trim().ToLowerInvariant();
    return v switch
    {
        "v1" or "1" => ConstValue.ModelVersion.V1,
        "v2" or "2" => ConstValue.ModelVersion.V2,
        _ => throw new ArgumentException(
            $"无效的 --version: {raw}（可选: v1 / v2）")
    };
}

/// <summary>解析 --model-dir,空值时回退到可执行目录 + Models。</summary>
static string ResolveModelDir(string? raw)
{
    if (string.IsNullOrWhiteSpace(raw)) return AppContext.BaseDirectory;
    return Path.GetFullPath(raw);
}

static string GetOption(string[] args, string option, string? defaultValue)
{
    for (int i = 0; i < args.Length - 1; i++)
    {
        if (args[i] == option)
        {
            return args[i + 1];
        }
    }
    return defaultValue ?? "";
}

static string GetPositionalArg(string[] args, int position)
{
    var positionalArgs = args.Where(a => !a.StartsWith("-")).ToArray();
    return positionalArgs.Length > position ? positionalArgs[position] : "";
}

void PrintUsage()
{
    Console.WriteLine("上海海事大学验证码 OCR - 命令行演示 (v1/v2 共存,默认 v2)");
    Console.WriteLine();
    Console.WriteLine("用法:");
    Console.WriteLine("  shmtu-ocr-onnx-demo recognize <图片路径> [选项]");
    Console.WriteLine("  shmtu-ocr-onnx-demo list-tags [--max-minor N]");
    Console.WriteLine("  shmtu-ocr-onnx-demo list-models [--version v2] [--tag v2.0.5]");
    Console.WriteLine("  shmtu-ocr-onnx-demo download [选项]");
    Console.WriteLine();
    Console.WriteLine("命令:");
    Console.WriteLine("  recognize <path>  识别单张验证码图片（兼容 image / predict 别名）");
    Console.WriteLine("  list-tags         自动解析 v2 最新 release tag");
    Console.WriteLine("  list-models       列出 v2 manifest 中所有可用模型（v2 only）");
    Console.WriteLine("  download          仅下载模型,不识别");
    Console.WriteLine();
    Console.WriteLine("选项:");
    Console.WriteLine("  --version v1|v2           模型版本,默认 v2");
    Console.WriteLine("  --tag TAG                 v2 release tag（如 v2.0.5）,留空则自动解析最新 v2 tag");
    Console.WriteLine("  --backbone NAME           v2 backbone,默认 mobilenet_v3_small");
    Console.WriteLine("  --precision fp16|fp32     v2 精度,默认 fp16");
    Console.WriteLine("  --model-dir DIR           模型目录（覆盖默认: <exe>/Models）");
    Console.WriteLine("  --max-minor N             list-tags 时允许的最大 minor（uint,默认 V2.MaxSupportedMinor）");
    Console.WriteLine();
    Console.WriteLine("示例:");
    Console.WriteLine("  shmtu-ocr-onnx-demo recognize ./samples/001.png");
    Console.WriteLine("  shmtu-ocr-onnx-demo recognize ./samples/001.png --version v1");
    Console.WriteLine("  shmtu-ocr-onnx-demo list-tags");
    Console.WriteLine("  shmtu-ocr-onnx-demo list-models --tag v2.0.5");
    Console.WriteLine("  shmtu-ocr-onnx-demo download --version v2 --tag v2.0.5 --backbone mobilenet_v3_small --precision fp16");
    Console.WriteLine("  shmtu-ocr-onnx-demo download --version v1 --model-dir ./models");
    Console.WriteLine("  shmtu-ocr-onnx-demo download --version v2 --backbone resnet18 --precision fp32");
}
