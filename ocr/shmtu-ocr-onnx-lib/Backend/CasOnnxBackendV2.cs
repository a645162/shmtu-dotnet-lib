using System.Text.Json;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using shmtu.captcha.onnx.ImageProcess;
using SkiaSharp;

namespace shmtu.captcha.onnx.Backend;

/// <summary>
/// v2 backend：单 ONNX（trislot decoder）。一次前向输出 3 个 tensor。
///
/// 输出 name 映射（基于 model-assets.json 与默认 mobilenet_v3_small.fp16.onnx 的预期命名）：
///   digit_left_logits  → 1×10  → left digit  (argmax 0..9)
///   operator_logits    → 1×3   → 0=+, 1=-, 2=× (无 Chs)
///   digit_right_logits → 1×10  → right digit (argmax 0..9)
/// 若实际 session.OutputMetadata 的名字与此不一致，会按"包含 digit_left/operator/digit_right"
/// 的子串做模糊匹配。
///
/// EqualSymbol 在 v2 语义中不存在，统一返回 -1（= NotApplicable）。
/// </summary>
public sealed class CasOnnxBackendV2 : ICasOcrBackend
{
    private InferenceSession? _session;
    private bool _isLoaded;

    public string BackendName => "v2";

    public bool IsLoaded => _isLoaded && _session != null;

    /// <summary>检查默认 backbone+precision 的模型文件是否存在。</summary>
    public static bool CheckModelIsExist(string destDir)
        => V2Downloader.CheckModelIsExist(destDir, ConstValue.V2.DefaultBackbone, ConstValue.V2.DefaultPrecision);

    /// <summary>检查指定 backbone+precision 的模型文件是否存在。</summary>
    public static bool CheckModelIsExist(string destDir, string backbone, string precision)
        => V2Downloader.CheckModelIsExist(destDir, backbone, precision);

    /// <summary>获取默认模型的本地完整路径（不存在则返回 null）。</summary>
    public static string? GetModelPath(string destDir)
    {
        var name = ConstValue.V2.BuildModelName(ConstValue.V2.DefaultBackbone, ConstValue.V2.DefaultPrecision);
        var full = Path.Combine(Path.GetFullPath(destDir), name);
        return File.Exists(full) ? full : null;
    }

    /// <summary>获取指定 backbone+precision 的本地完整路径（不存在则返回 null）。</summary>
    public static string? GetModelPath(string destDir, string backbone, string precision)
    {
        var name = ConstValue.V2.BuildModelName(backbone, precision);
        var full = Path.Combine(Path.GetFullPath(destDir), name);
        return File.Exists(full) ? full : null;
    }

    /// <summary>
    /// 下载 v2 默认模型到 destDir（tag=null 时自动解析最新 release）。
    /// 可选 <paramref name="assetStem"/> 用于在 v2 多模型 manifest 中选择特定模型；
    /// 留空则使用默认 backbone 匹配。
    /// </summary>
    public static Task<bool> DownloadModelAsync(
        string destDir,
        string? tag,
        IProgress<float>? progress = null,
        HttpClient? httpClient = null,
        Action<string>? log = null,
        string? assetStem = null)
    {
        return V2Downloader.DownloadAsync(
            destDir,
            tag,
            ConstValue.V2.DefaultBackbone,
            ConstValue.V2.DefaultPrecision,
            progress,
            httpClient,
            log,
            "gitee",
            "github",
            assetStem);
    }

    /// <summary>列出 v2 manifest 中所有可用模型（仅 v2 多模型 schema 有意义）。</summary>
    public static Task<List<ModelInfo>> ListAvailableModelsAsync(
        string? tag = null,
        HttpClient? httpClient = null,
        Action<string>? log = null)
    {
        return ListAvailableModelsInternalAsync(tag, httpClient, log);
    }

    private static async Task<List<ModelInfo>> ListAvailableModelsInternalAsync(
        string? tag,
        HttpClient? httpClient,
        Action<string>? log)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            tag = await V2Downloader.ResolveLatestTagAsync(
                ConstValue.V2.MaxSupportedMajor,
                ConstValue.V2.MaxSupportedMinor,
                ConstValue.V2.DefaultTag,
                httpClient,
                log);
        }

        var ownClient = httpClient is null;
        var client = httpClient ?? new HttpClient();
        try
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd("shmtu-cas-ocr-dotnet/1.0");
            var manifestUrl = $"{ConstValue.V2.BaseUrlGithub}/{tag}/{ConstValue.V2.ManifestName}";
            var json = await client.GetStringAsync(manifestUrl);
            var manifest = JsonSerializer.Deserialize<ReleaseManifest>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (manifest == null) return new List<ModelInfo>();
            return V2Downloader.ListModelsFromManifest(manifest);
        }
        catch (Exception ex)
        {
            log?.Invoke($"[CasOnnxBackendV2] 列出模型失败: {ex.Message}");
            return new List<ModelInfo>();
        }
        finally
        {
            if (ownClient) client.Dispose();
        }
    }

    public bool LoadModel(string directoryPath, bool useGpu = false, int gpuDeviceId = 0)
    {
        var modelPath = GetModelPath(directoryPath);
        if (modelPath == null) return false;
        return LoadModelFromPath(modelPath, useGpu, gpuDeviceId);
    }

    /// <summary>从指定 backbone+precision 加载模型。</summary>
    public bool LoadModel(string directoryPath, string backbone, string precision, bool useGpu = false, int gpuDeviceId = 0)
    {
        var modelPath = GetModelPath(directoryPath, backbone, precision);
        if (modelPath == null) return false;
        return LoadModelFromPath(modelPath, useGpu, gpuDeviceId);
    }

    /// <summary>从指定文件路径加载模型。</summary>
    public bool LoadModelFromPath(string modelPath, bool useGpu = false, int gpuDeviceId = 0)
    {
        if (!File.Exists(modelPath)) return false;

        var options = new SessionOptions();

#if GPU_BUILD
        if (useGpu)
        {
            options.AppendExecutionProvider_CUDA(gpuDeviceId);
        }
#endif

        _session = new InferenceSession(modelPath, options);
        _isLoaded = true;

        LogOutputNames(_session);

        return true;
    }

    private static void LogOutputNames(InferenceSession session)
    {
        try
        {
            var names = string.Join(", ", session.OutputMetadata.Keys);
            Console.WriteLine($"[CasOnnxBackendV2] session outputs: {names}");
        }
        catch
        {
            // best-effort logging
        }
    }

    private static int ArgMax(ReadOnlySpan<float> data)
    {
        if (data.Length == 0) return -1;
        var bestIdx = 0;
        var bestVal = data[0];
        for (var i = 1; i < data.Length; i++)
        {
            if (data[i] > bestVal)
            {
                bestVal = data[i];
                bestIdx = i;
            }
        }
        return bestIdx;
    }

    private static string ResolveOutputKey(IReadOnlyDictionary<string, NodeMetadata> outputs, string preferred,
        params string[] hints)
    {
        if (outputs.ContainsKey(preferred)) return preferred;
        foreach (var hint in hints)
        {
            foreach (var kv in outputs)
            {
                if (kv.Key.Contains(hint, StringComparison.OrdinalIgnoreCase))
                    return kv.Key;
            }
        }
        // 最后退到首键
        return outputs.Keys.First();
    }

    private static int GetOperatorCharIndex(int opIdx) => opIdx switch
    {
        0 => '+',
        1 => '-',
        2 => '×',
        _ => '?'
    };

    private static int Calc(int left, int opIdx, int right) => opIdx switch
    {
        0 => left + right,
        1 => left - right,
        2 => left * right,
        _ => -1
    };

    public (int Result, string Expr, int EqualSymbol, int Operator, int Digit1, int Digit2)
        PredictValidateCode(SKBitmap originalImage)
    {
        const int equalSymbol = -1; // v2: not applicable
        var defaultValue = (-1, "", equalSymbol, -1, -1, -1);
        if (!IsLoaded) return defaultValue;
        if (originalImage.Width == 0 || originalImage.Height == 0) return defaultValue;

        var tensor = V2Preprocess.ConvertToV2Tensor(originalImage);
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input", tensor)
        };

        using var results = _session!.Run(inputs);
        var outputs = _session.OutputMetadata;

        var leftKey = ResolveOutputKey(outputs, "digit_left_logits", "digit_left", "left");
        var opKey = ResolveOutputKey(outputs, "operator_logits", "operator");
        var rightKey = ResolveOutputKey(outputs, "digit_right_logits", "digit_right", "right");

        var leftArr = results.First(r => r.Name == leftKey).AsTensor<float>().ToArray();
        var opArr = results.First(r => r.Name == opKey).AsTensor<float>().ToArray();
        var rightArr = results.First(r => r.Name == rightKey).AsTensor<float>().ToArray();

        var d1 = ArgMax(leftArr);
        var op = ArgMax(opArr);
        var d2 = ArgMax(rightArr);

        if (d1 < 0 || d1 > 9 || d2 < 0 || d2 > 9 || op < 0 || op > 2)
            return defaultValue;

        var result = Calc(d1, op, d2);
        var opChar = GetOperatorCharIndex(op);
        var expr = $"{d1} {opChar} {d2} = {result}";

        return (result, expr, equalSymbol, op, d1, d2);
    }

    public void Dispose()
    {
        _session?.Dispose();
        _session = null;
        _isLoaded = false;
    }
}