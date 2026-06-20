using System.Text.RegularExpressions;

namespace shmtu.captcha.onnx.Backend;

/// <summary>
/// 本地模型扫描结果条目。
/// </summary>
public sealed class LocalModelEntry
{
    /// <summary>模型版本（V1 / V2）。</summary>
    public ConstValue.ModelVersion Version { get; init; }

    /// <summary>文件名。</summary>
    public string FileName { get; init; } = "";

    /// <summary>文件完整路径。</summary>
    public string FullPath { get; init; } = "";

    /// <summary>文件大小（字节）。</summary>
    public long FileSizeBytes { get; init; }

    /// <summary>V2 backbone 名称（仅 V2 有效）。</summary>
    public string? Backbone { get; init; }

    /// <summary>V2 precision（仅 V2 有效，如 fp16 / fp32）。</summary>
    public string? Precision { get; init; }

    /// <summary>V2 model family（仅 V2 有效，如 trislot_decoder）。</summary>
    public string? Family { get; init; }

    /// <summary>V2 version string（仅 V2 有效，如 v2_0）。</summary>
    public string? ModelVersion { get; init; }

    /// <summary>可读的显示文本。</summary>
    public string DisplayName => Version == ConstValue.ModelVersion.V1
        ? $"V1 - {FileName}"
        : $"V2 / {Backbone} / {Precision} ({FileName})";

    /// <summary>文件大小的可读字符串。</summary>
    public string FileSizeDisplay
    {
        get
        {
            if (FileSizeBytes < 1024) return $"{FileSizeBytes} B";
            if (FileSizeBytes < 1024 * 1024) return $"{FileSizeBytes / 1024.0:F1} KB";
            return $"{FileSizeBytes / (1024.0 * 1024.0):F1} MB";
        }
    }
}

/// <summary>
/// 扫描本地模型目录，检测所有已下载的 ONNX 模型文件。
/// V1: 3 个 ResNet 模型（resnet18_equal_symbol_latest.onnx 等）。
/// V2: 按命名规则 {backbone}.{family}.v{major}_{minor}.{precision}.onnx 解析。
/// </summary>
public static class LocalModelScanner
{
    /// <summary>
    /// V2 文件名正则：{backbone}.{family}.v{major}_{minor}.{precision}.onnx
    /// 例: mobilenet_v3_small.trislot_decoder.v2_0.fp16.onnx
    /// </summary>
    private static readonly Regex V2Pattern = new(
        @"^(.+)\.(.+)\.v(\d+)_(\d+)\.(.+)\.onnx$",
        RegexOptions.Compiled);

    /// <summary>
    /// 扫描指定目录下所有 ONNX 模型文件，返回按版本分组的条目列表。
    /// </summary>
    /// <param name="modelDirectory">模型目录路径。</param>
    /// <returns>所有找到的本地模型条目。</returns>
    public static List<LocalModelEntry> Scan(string modelDirectory)
    {
        var results = new List<LocalModelEntry>();
        if (string.IsNullOrWhiteSpace(modelDirectory) || !Directory.Exists(modelDirectory))
            return results;

        var fullDir = Path.GetFullPath(modelDirectory);

        // 1) Scan V1 models
        foreach (var v1File in ConstValue.V1.AllFiles)
        {
            var path = Path.Combine(fullDir, v1File);
            if (!File.Exists(path)) continue;

            var fi = new FileInfo(path);
            results.Add(new LocalModelEntry
            {
                Version = ConstValue.ModelVersion.V1,
                FileName = v1File,
                FullPath = path,
                FileSizeBytes = fi.Length,
            });
        }

        // 2) Scan V2 models: all *.onnx files matching the V2 naming pattern
        try
        {
            foreach (var file in Directory.GetFiles(fullDir, "*.onnx"))
            {
                var fileName = Path.GetFileName(file);

                // Skip V1 files (already handled above)
                if (ConstValue.V1.AllFiles.Contains(fileName, StringComparer.OrdinalIgnoreCase))
                    continue;

                var match = V2Pattern.Match(fileName);
                if (!match.Success) continue;

                var backbone = match.Groups[1].Value;
                var family = match.Groups[2].Value;
                var precision = match.Groups[5].Value;

                // Validate precision: only known values
                if (precision != "fp16" && precision != "fp32" && precision != "int8")
                    continue;

                var fi = new FileInfo(file);
                results.Add(new LocalModelEntry
                {
                    Version = ConstValue.ModelVersion.V2,
                    FileName = fileName,
                    FullPath = file,
                    FileSizeBytes = fi.Length,
                    Backbone = backbone,
                    Precision = precision,
                    Family = family,
                    ModelVersion = $"v{match.Groups[3].Value}_{match.Groups[4].Value}",
                });
            }
        }
        catch
        {
            // best-effort: if directory scan fails, return what we have
        }

        return results;
    }
}
