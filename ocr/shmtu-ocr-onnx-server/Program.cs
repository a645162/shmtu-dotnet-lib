using System.Diagnostics;
using shmtu.captcha.onnx.server.Models;
using shmtu.captcha.onnx.server.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<OcrServerConfig>(
    builder.Configuration.GetSection("OcrServer"));

builder.Services.AddSingleton<OcrService>();
builder.Services.AddHostedService<TcpOcrServerService>();

var app = builder.Build();
var logger = app.Services.GetRequiredService<ILogger<Program>>();

app.Use(async (context, next) =>
{
    var requestStopwatch = Stopwatch.StartNew();
    logger.LogInformation(
        "HTTP request started | Method={Method} | Path={Path} | RemoteIp={RemoteIp} | ContentLength={ContentLength}",
        context.Request.Method,
        context.Request.Path,
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        context.Request.ContentLength);

    try
    {
        await next();
        requestStopwatch.Stop();
        logger.LogInformation(
            "HTTP request finished | Method={Method} | Path={Path} | StatusCode={StatusCode} | ElapsedMs={ElapsedMs}",
            context.Request.Method,
            context.Request.Path,
            context.Response.StatusCode,
            requestStopwatch.ElapsedMilliseconds);
    }
    catch (Exception ex)
    {
        requestStopwatch.Stop();
        logger.LogError(
            ex,
            "HTTP request failed | Method={Method} | Path={Path} | ElapsedMs={ElapsedMs}",
            context.Request.Method,
            context.Request.Path,
            requestStopwatch.ElapsedMilliseconds);
        throw;
    }
});

// Initialize OCR service (download models if needed, pre-warm pool)
var ocrService = app.Services.GetRequiredService<OcrService>();
logger.LogInformation("Initializing OCR service");
await ocrService.InitializeAsync();

logger.LogInformation("OCR service initialized. Models loaded: {Loaded}, Pool size: {Size}",
    ocrService.ModelsLoaded, ocrService.PoolSize);

app.MapGet("/api/health", () =>
{
    var serverName = app.Configuration.GetSection("OcrServer:ServerName").Get<string?>();
    return Results.Ok(new HealthResponse
    {
        Status = "healthy",
        ModelsLoaded = ocrService.ModelsLoaded,
        PoolSize = ocrService.PoolSize,
        ServerName = serverName
    });
});

app.MapGet("/api/status", () =>
{
    var loaded = ocrService.ModelsLoaded;
    var poolSize = ocrService.PoolSize;
    var total = ocrService.TotalRequests;
    var success = ocrService.SuccessCount;
    var failure = ocrService.FailureCount;
    var uptime = ocrService.UptimeSeconds;

    string status = loaded ? "healthy" : "unavailable";
    string availabilityLevel = !loaded ? "unavailable" : "available";
    string reason = loaded ? "" : "Models not loaded";

    logger.LogDebug("Status requested | Status={Status} | ModelsLoaded={ModelsLoaded} | Total={Total}",
        status, loaded, total);

    return Results.Ok(new StatusResponse
    {
        Status = status,
        AvailabilityLevel = availabilityLevel,
        Reason = reason,
        ModelsLoaded = loaded,
        PoolSize = poolSize,
        QueueCapacity = poolSize,
        PendingRequests = 0,
        ActiveWorkers = poolSize,
        TotalRequests = total,
        SuccessCount = success,
        FailureCount = failure,
        UptimeSeconds = uptime,
        ServerName = app.Configuration.GetSection("OcrServer:ServerName").Get<string?>()
    });
});

app.MapPost("/api/ocr", (OcrRequest? request) =>
{
    if (request?.ImageBase64 == null)
    {
        logger.LogWarning("OCR request rejected: missing ImageBase64 payload");
        return Results.BadRequest(new OcrResponse { Success = false, Error = "ImageBase64 is required" });
    }

    byte[] imageBytes;
    try
    {
        imageBytes = Convert.FromBase64String(request.ImageBase64);
    }
    catch (FormatException)
    {
        logger.LogWarning("OCR request rejected: invalid base64 payload");
        return Results.BadRequest(new OcrResponse { Success = false, Error = "Invalid base64 string" });
    }

    logger.LogInformation("OCR request accepted | ImageBytes={ImageBytes}", imageBytes.Length);
    var sw = Stopwatch.StartNew();
    var result = ocrService.Predict(imageBytes);
    sw.Stop();
    logger.LogInformation(
        "OCR completed | ElapsedMs={ElapsedMs} | Success={Success} | Expression={Expression} | Result={Result}",
        sw.ElapsedMilliseconds,
        result.Success,
        result.Expression,
        result.Result);

    return Results.Ok(result);
});

app.MapPost("/api/ocr/upload", async (IFormFile? file) =>
{
    if (file == null || file.Length == 0)
    {
        logger.LogWarning("OCR upload request rejected: missing file");
        return Results.BadRequest(new OcrResponse { Success = false, Error = "Image file is required" });
    }

    byte[] imageBytes;
    using (var ms = new MemoryStream())
    {
        await file.CopyToAsync(ms);
        imageBytes = ms.ToArray();
    }

    logger.LogInformation(
        "OCR upload request accepted | FileName={FileName} | ContentType={ContentType} | FileLength={FileLength} | ImageBytes={ImageBytes}",
        file.FileName,
        file.ContentType,
        file.Length,
        imageBytes.Length);
    var sw = Stopwatch.StartNew();
    var result = ocrService.Predict(imageBytes);
    sw.Stop();
    logger.LogInformation(
        "OCR upload completed | ElapsedMs={ElapsedMs} | Success={Success} | Expression={Expression} | Result={Result}",
        sw.ElapsedMilliseconds,
        result.Success,
        result.Expression,
        result.Result);

    return Results.Ok(result);
}).DisableAntiforgery();

app.Run();
