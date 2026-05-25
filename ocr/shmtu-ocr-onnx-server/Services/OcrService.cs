using Microsoft.Extensions.ObjectPool;
using Microsoft.Extensions.Options;
using shmtu.captcha.onnx;
using shmtu.captcha.onnx.server.Models;

namespace shmtu.captcha.onnx.server.Services;

public class OcrServerConfig
{
    public string ModelDirectory { get; set; } = "";
    public int PoolSize { get; set; } = 0;
    public int TcpPort { get; set; } = 21601;
    public string TcpListenAddress { get; set; } = "0.0.0.0";
}

public class OcrService : IDisposable
{
    private readonly ObjectPool<CasOcr> _pool;
    private readonly int _poolSize;
    private bool _modelsLoaded;

    public int PoolSize => _poolSize;
    public bool ModelsLoaded => _modelsLoaded;

    public OcrService(IOptions<OcrServerConfig> config)
    {
        var cfg = config.Value;
        var modelDir = ResolveModelDirectory(cfg.ModelDirectory);
        _poolSize = cfg.PoolSize > 0 ? cfg.PoolSize : Math.Max(Environment.ProcessorCount, 4);

        var policy = new CasOcrPooledObjectPolicy(modelDir);
        _pool = new DefaultObjectPool<CasOcr>(policy, _poolSize);
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        var ocr = _pool.Get();
        try
        {
            await ocr.EnsureModelsAsync();
            if (!ocr.IsLoaded)
                ocr.LoadModel();
            _modelsLoaded = ocr.IsLoaded;
        }
        finally
        {
            _pool.Return(ocr);
        }

        if (!_modelsLoaded)
            return;

        var warmup = new List<CasOcr>();
        for (var i = 0; i < _poolSize - 1; i++)
        {
            var instance = _pool.Get();
            if (!instance.IsLoaded)
                instance.LoadModel();
            warmup.Add(instance);
        }
        foreach (var instance in warmup)
            _pool.Return(instance);
    }

    public OcrResponse Predict(byte[] imageBytes)
    {
        var ocr = _pool.Get();
        try
        {
            if (!ocr.IsLoaded && !ocr.LoadModel())
                return new OcrResponse { Success = false, Error = "Model not loaded" };

            var (result, expr, equalSymbol, op, digit1, digit2) = ocr.PredictValidateCode(imageBytes);
            return new OcrResponse
            {
                Success = result >= 0,
                Expression = expr,
                Result = result,
                EqualSymbol = equalSymbol,
                Operator = op,
                Digit1 = digit1,
                Digit2 = digit2,
                Error = result < 0 ? "OCR recognition failed" : null
            };
        }
        catch (Exception ex)
        {
            return new OcrResponse { Success = false, Error = ex.Message };
        }
        finally
        {
            _pool.Return(ocr);
        }
    }

    public void Dispose()
    {
    }

    private static string? ResolveModelDirectory(string configured)
    {
        if (string.IsNullOrWhiteSpace(configured))
            return null;

        if (Path.IsPathFullyQualified(configured))
            return configured;

        // Resolve relative paths against the assembly location, not CWD
        var baseDir = AppContext.BaseDirectory;
        return Path.GetFullPath(Path.Combine(baseDir, configured));
    }
}

internal class CasOcrPooledObjectPolicy : PooledObjectPolicy<CasOcr>
{
    private readonly string? _modelDir;

    public CasOcrPooledObjectPolicy(string? modelDir)
    {
        _modelDir = modelDir;
    }

    public override CasOcr Create()
    {
        return new CasOcr(_modelDir);
    }

    public override bool Return(CasOcr obj)
    {
        return obj.IsLoaded || obj.LoadModel();
    }
}
