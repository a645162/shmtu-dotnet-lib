using shmtu.captcha.onnx.Backend;
using SkiaSharp;

namespace shmtu.captcha.onnx;

/// <summary>
/// 公开 API：SHMTU CAS 验证码 OCR。
/// 模型默认目录为 <see cref="AppContext.BaseDirectory"/>（可执行文件目录）；缺失时调用
/// <see cref="EnsureModelsAsync"/> 触发下载。
/// </summary>
public sealed class CasOcr : IDisposable
{
    private readonly CasOnnxBackend _backend = new();
    private string _modelDirectoryPath;

    public CasOcr(string? modelDirectoryPath = null)
    {
        _modelDirectoryPath = ResolvePath(modelDirectoryPath);
    }

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

    public bool CheckModelIsExist() => CasOnnxBackend.CheckModelIsExist(ModelDirectoryPath);

    /// <summary>
    /// 检查模型；缺失则下载。返回是否最终满足条件（已存在或下载成功）。
    /// </summary>
    public async Task<bool> EnsureModelsAsync(
        IProgress<float>? progress = null,
        HttpClient? httpClient = null)
    {
        if (CheckModelIsExist())
        {
            progress?.Report(100f);
            return true;
        }

        return await CasOnnxBackend.DownloadModelAsync(ModelDirectoryPath, progress, httpClient);
    }

    public bool LoadModel()
    {
        if (_backend.IsLoaded) return true;
        try
        {
            return _backend.LoadModel(ModelDirectoryPath);
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
