using Microsoft.Extensions.ObjectPool;
using Microsoft.Extensions.Options;
using shmtu.captcha.onnx;
using shmtu.captcha.onnx.Backend;
using shmtu.captcha.onnx.server.Models;
using System.Diagnostics;

namespace shmtu.captcha.onnx.server.Services;

public class OcrServerConfig
{
    public string ModelDirectory { get; set; } = "";
    public int PoolSize { get; set; } = 0;
    public int TcpPort { get; set; } = 21601;
    public string TcpListenAddress { get; set; } = "0.0.0.0";
    public string ExecutionProvider { get; set; } = "CPU";
    public int GpuDeviceId { get; set; } = 0;
    public string? ServerName { get; set; }
}

public class OcrService : IDisposable
{
    private readonly ILogger<OcrService> _logger;
    private readonly ObjectPool<CasOcr> _pool;
    private readonly int _poolSize;
    private readonly string? _modelDirectory;
    private readonly bool _useGpu;
    private readonly int _gpuDeviceId;
    private bool _modelsLoaded;
    private readonly Stopwatch _uptimeStopwatch = Stopwatch.StartNew();

    private long _totalRequests;
    private long _successCount;
    private long _failureCount;

    public int PoolSize => _poolSize;
    public bool ModelsLoaded => _modelsLoaded;
    public long TotalRequests => Interlocked.Read(ref _totalRequests);
    public long SuccessCount => Interlocked.Read(ref _successCount);
    public long FailureCount => Interlocked.Read(ref _failureCount);
    public long UptimeSeconds => _uptimeStopwatch.ElapsedMilliseconds / 1000;

    public OcrService(IOptions<OcrServerConfig> config, ILogger<OcrService> logger)
    {
        _logger = logger;
        var cfg = config.Value;
        _modelDirectory = ResolveModelDirectory(cfg.ModelDirectory);
        _poolSize = cfg.PoolSize > 0 ? cfg.PoolSize : Math.Max(Environment.ProcessorCount, 4);
        _useGpu = cfg.ExecutionProvider.Equals("CUDA", StringComparison.OrdinalIgnoreCase);
        _gpuDeviceId = cfg.GpuDeviceId;

        _logger.LogInformation(
            "OCR service configuration resolved | ModelDirectory={ModelDirectory} | PoolSize={PoolSize} | ExecutionProvider={ExecutionProvider} | GpuDeviceId={GpuDeviceId}",
            _modelDirectory ?? "<default>",
            _poolSize,
            _useGpu ? "CUDA" : cfg.ExecutionProvider,
            _gpuDeviceId);

        var policy = new CasOcrPooledObjectPolicy(_modelDirectory, _useGpu, cfg.GpuDeviceId);
        _pool = new DefaultObjectPool<CasOcr>(policy, _poolSize);
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        var initializeStopwatch = Stopwatch.StartNew();
        var ocr = _pool.Get();
        try
        {
            var missingFiles = ocr.GetMissingModelFiles();
            if (missingFiles.Length == 0)
            {
                _logger.LogInformation("All OCR model files already present | ModelDirectory={ModelDirectory}", ocr.ModelDirectoryPath);
            }
            else
            {
                _logger.LogWarning(
                    "OCR model files missing; download required | ModelDirectory={ModelDirectory} | MissingFiles={MissingFiles} | Source={ModelSource}",
                    ocr.ModelDirectoryPath,
                    string.Join(", ", missingFiles),
                    ConstValue.ModelOnnxBaseUrl);
            }

            var lastLoggedProgress = -10;
            var downloadStopwatch = Stopwatch.StartNew();
            var ensured = await ocr.EnsureModelsAsync(
                new Progress<float>(p =>
                {
                    var progressValue = (int)Math.Floor(p);
                    if (progressValue >= 100 || progressValue >= lastLoggedProgress + 10)
                    {
                        lastLoggedProgress = progressValue;
                        _logger.LogInformation(
                            "OCR model download progress | Progress={ProgressPercent}% | ModelDirectory={ModelDirectory}",
                            progressValue,
                            ocr.ModelDirectoryPath);
                    }
                }),
                null,
                message => _logger.LogInformation("OCR model preparation | {Message}", message));

            downloadStopwatch.Stop();
            _logger.LogInformation(
                "OCR model ensure finished | Success={Success} | ElapsedMs={ElapsedMs} | ModelDirectory={ModelDirectory}",
                ensured,
                downloadStopwatch.ElapsedMilliseconds,
                ocr.ModelDirectoryPath);

            if (!ensured)
            {
                _logger.LogError("OCR model ensure failed; service initialization aborted | ModelDirectory={ModelDirectory}", ocr.ModelDirectoryPath);
                return;
            }

            if (!ocr.IsLoaded)
            {
                _logger.LogInformation(
                    "Loading OCR models into ONNX runtime | ModelDirectory={ModelDirectory} | ExecutionProvider={ExecutionProvider} | GpuDeviceId={GpuDeviceId}",
                    ocr.ModelDirectoryPath,
                    _useGpu ? "CUDA" : "CPU",
                    _gpuDeviceId);
                var loadStopwatch = Stopwatch.StartNew();
                var loaded = ocr.LoadModel();
                loadStopwatch.Stop();
                _logger.LogInformation(
                    "OCR model load finished | Success={Success} | ElapsedMs={ElapsedMs} | ModelDirectory={ModelDirectory}",
                    loaded,
                    loadStopwatch.ElapsedMilliseconds,
                    ocr.ModelDirectoryPath);
            }

            _modelsLoaded = ocr.IsLoaded;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OCR service initialization failed");
            throw;
        }
        finally
        {
            _pool.Return(ocr);
        }

        if (!_modelsLoaded)
        {
            _logger.LogError("OCR service initialization finished without loaded models");
            return;
        }

        _logger.LogInformation("Starting OCR object pool warmup | PoolSize={PoolSize}", _poolSize);
        var warmupStopwatch = Stopwatch.StartNew();
        var warmup = new List<CasOcr>();
        for (var i = 0; i < _poolSize - 1; i++)
        {
            var instance = _pool.Get();
            if (!instance.IsLoaded)
            {
                var loaded = instance.LoadModel();
                _logger.LogDebug("Warmup instance loaded | Index={Index} | Success={Success}", i + 2, loaded);
            }

            warmup.Add(instance);
        }
        foreach (var instance in warmup)
            _pool.Return(instance);

        warmupStopwatch.Stop();
        initializeStopwatch.Stop();
        _logger.LogInformation(
            "OCR object pool warmup finished | WarmedInstances={WarmedInstances} | WarmupElapsedMs={WarmupElapsedMs} | InitElapsedMs={InitElapsedMs}",
            warmup.Count + 1,
            warmupStopwatch.ElapsedMilliseconds,
            initializeStopwatch.ElapsedMilliseconds);
    }

    public OcrResponse Predict(byte[] imageBytes)
    {
        Interlocked.Increment(ref _totalRequests);
        var ocr = _pool.Get();
        try
        {
            if (!ocr.IsLoaded && !ocr.LoadModel())
            {
                _logger.LogError("OCR prediction aborted because model load failed | ImageBytes={ImageBytes}", imageBytes.Length);
                Interlocked.Increment(ref _failureCount);
                return new OcrResponse { Success = false, Error = "Model not loaded" };
            }

            var (result, expr, equalSymbol, op, digit1, digit2) = ocr.PredictValidateCode(imageBytes);
            var success = !string.IsNullOrEmpty(expr);
            _logger.LogDebug(
                "OCR prediction finished | Success={Success} | ImageBytes={ImageBytes} | Expression={Expression} | Result={Result}",
                success,
                imageBytes.Length,
                expr,
                result);

            if (success)
                Interlocked.Increment(ref _successCount);
            else
                Interlocked.Increment(ref _failureCount);

            return new OcrResponse
            {
                Success = success,
                Expression = expr,
                Result = result,
                EqualSymbol = equalSymbol,
                Operator = op,
                Digit1 = digit1,
                Digit2 = digit2,
                Error = success ? null : "OCR recognition failed"
            };
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _failureCount);
            _logger.LogError(ex, "OCR prediction failed | ImageBytes={ImageBytes}", imageBytes.Length);
            return new OcrResponse { Success = false, Error = ex.Message };
        }
        finally
        {
            _pool.Return(ocr);
        }
    }

    public void Dispose()
    {
        _logger.LogInformation("Disposing OCR service");
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
    private readonly bool _useGpu;
    private readonly int _gpuDeviceId;

    public CasOcrPooledObjectPolicy(string? modelDir, bool useGpu = false, int gpuDeviceId = 0)
    {
        _modelDir = modelDir;
        _useGpu = useGpu;
        _gpuDeviceId = gpuDeviceId;
    }

    public override CasOcr Create()
    {
        return new CasOcr(_modelDir, _useGpu, _gpuDeviceId);
    }

    public override bool Return(CasOcr obj)
    {
        return obj.IsLoaded || obj.LoadModel();
    }
}
