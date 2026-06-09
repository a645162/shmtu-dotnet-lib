using shmtu.captcha.onnx.Backend;
using SkiaSharp;

namespace shmtu.captcha.onnx;

/// <summary>
/// 公开 API：SHMTU CAS 验证码 OCR。
/// 模型默认目录为 <see cref="AppContext.BaseDirectory"/>（可执行文件目录）；缺失时调用
/// <see cref="EnsureModelsAsync"/> 触发下载。
///
/// 默认使用 v2 (trislot decoder, mobilenet_v3_small + fp16)，可显式传入 v1 走老的 3 模型 ResNet 路径。
/// </summary>
public sealed class CasOcr : IDisposable
{
    private ICasOcrBackend _backend;
    private string _modelDirectoryPath;
    private readonly bool _useGpu;
    private readonly int _gpuDeviceId;
    private readonly ConstValue.ModelVersion _version;

    /// <summary>
    /// 构造 OCR 客户端。
    /// </summary>
    /// <param name="modelDirectoryPath">模型目录（默认 <see cref="AppContext.BaseDirectory"/>）。</param>
    /// <param name="useGpu">是否启用 GPU EP（仅 GPU 构建生效）。</param>
    /// <param name="gpuDeviceId">GPU 设备 id。</param>
    /// <param name="version">模型版本，默认 v2。</param>
    public CasOcr(
        string? modelDirectoryPath = null,
        bool useGpu = false,
        int gpuDeviceId = 0,
        ConstValue.ModelVersion version = ConstValue.DefaultVersion)
    {
        _modelDirectoryPath = ResolvePath(modelDirectoryPath);
        _useGpu = useGpu;
        _gpuDeviceId = gpuDeviceId;
        _version = version;
        _backend = CreateBackend(version);
    }

    /// <summary>当前 backend 使用的模型版本。</summary>
    public ConstValue.ModelVersion Version => _version;

    /// <summary>当前 backend 标识（用于日志 / 配置持久化）。</summary>
    public string BackendName => _backend.BackendName;

    public string ModelDirectoryPath
    {
        get => _modelDirectoryPath;
        set
        {
            if (!Directory.Exists(value)) Directory.CreateDirectory(value);
            _modelDirectoryPath = Path.GetFullPath(value);
        }
    }

    public bool IsLoaded => _backend.IsLoaded;

    public bool CheckModelIsExist()
        => _version == ConstValue.ModelVersion.V2
            ? CasOnnxBackendV2.CheckModelIsExist(ModelDirectoryPath)
            : CasOnnxBackendV1.CheckModelIsExist(ModelDirectoryPath);

    public string[] GetMissingModelFiles()
        => _version == ConstValue.ModelVersion.V2
            ? (CasOnnxBackendV2.CheckModelIsExist(ModelDirectoryPath)
                ? Array.Empty<string>()
                : new[] { ConstValue.V2.DefaultModelName })
            : CasOnnxBackendV1.GetMissingModelFiles(ModelDirectoryPath);

    /// <summary>
    /// 检查模型；缺失则下载。返回是否最终满足条件（已存在或下载成功）。
    /// </summary>
    public async Task<bool> EnsureModelsAsync(
        IProgress<float>? progress = null,
        HttpClient? httpClient = null,
        Action<string>? log = null)
    {
        if (CheckModelIsExist())
        {
            progress?.Report(100f);
            log?.Invoke($"模型文件已存在: {ModelDirectoryPath} (version={_version})");
            return true;
        }

        return _version == ConstValue.ModelVersion.V2
            ? await CasOnnxBackendV2.DownloadModelAsync(ModelDirectoryPath, progress, httpClient, log)
            : await CasOnnxBackendV1.DownloadModelAsync(ModelDirectoryPath, progress, httpClient, log);
    }

    public bool LoadModel()
    {
        if (_backend.IsLoaded) return true;
        try
        {
            return _backend.LoadModel(ModelDirectoryPath, _useGpu, _gpuDeviceId);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return false;
        }
    }

    public (int Result, string Expr, int EqualSymbol, int Operator, int Digit1, int Digit2)
        PredictValidateCode(SKBitmap image)
    {
        var defaultValue = (-1, "", -1, -1, -1, -1);
        if (!_backend.IsLoaded && !LoadModel()) return defaultValue;
        return _backend.PredictValidateCode(image);
    }

    public (int Result, string Expr, int EqualSymbol, int Operator, int Digit1, int Digit2)
        PredictValidateCode(string imagePath)
    {
        try
        {
            using var image = SKBitmap.Decode(imagePath);
            if (image == null) return (-1, "", -1, -1, -1, -1);
            return PredictValidateCode(image);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return (-1, "", -1, -1, -1, -1);
        }
    }

    public (int Result, string Expr, int EqualSymbol, int Operator, int Digit1, int Digit2)
        PredictValidateCode(Stream stream)
    {
        try
        {
            using var image = SKBitmap.Decode(stream);
            if (image == null) return (-1, "", -1, -1, -1, -1);
            return PredictValidateCode(image);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return (-1, "", -1, -1, -1, -1);
        }
    }

    public (int Result, string Expr, int EqualSymbol, int Operator, int Digit1, int Digit2)
        PredictValidateCode(byte[] imageBytes)
    {
        using var ms = new MemoryStream(imageBytes);
        return PredictValidateCode(ms);
    }

    public void Dispose() => _backend.Dispose();

    private static ICasOcrBackend CreateBackend(ConstValue.ModelVersion version) => version switch
    {
        ConstValue.ModelVersion.V2 => new CasOnnxBackendV2(),
        _ => new CasOnnxBackendV1()
    };

    private static string ResolvePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            var baseDir = AppContext.BaseDirectory;
            var defaultModelsDir = Path.GetFullPath(Path.Combine(baseDir, "..", "Models"));
            if (Directory.Exists(defaultModelsDir)) return defaultModelsDir;
            Directory.CreateDirectory(defaultModelsDir);
            return defaultModelsDir;
        }
        if (!Directory.Exists(path)) Directory.CreateDirectory(path);
        return Path.GetFullPath(path);
    }
}