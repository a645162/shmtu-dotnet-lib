using System.Diagnostics;
using shmtu.captcha.onnx.server.Models;
using shmtu.captcha.onnx.server.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<OcrServerConfig>(
    builder.Configuration.GetSection("OcrServer"));

builder.Services.AddSingleton<OcrService>();
builder.Services.AddHostedService<TcpOcrServerService>();

var app = builder.Build();

// Initialize OCR service (download models if needed, pre-warm pool)
var ocrService = app.Services.GetRequiredService<OcrService>();
await ocrService.InitializeAsync();

var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("OCR service initialized. Models loaded: {Loaded}, Pool size: {Size}",
    ocrService.ModelsLoaded, ocrService.PoolSize);

app.MapGet("/api/health", () =>
{
    return Results.Ok(new HealthResponse
    {
        Status = "healthy",
        ModelsLoaded = ocrService.ModelsLoaded,
        PoolSize = ocrService.PoolSize
    });
});

app.MapPost("/api/ocr", (OcrRequest? request) =>
{
    if (request?.ImageBase64 == null)
        return Results.BadRequest(new OcrResponse { Success = false, Error = "ImageBase64 is required" });

    byte[] imageBytes;
    try
    {
        imageBytes = Convert.FromBase64String(request.ImageBase64);
    }
    catch (FormatException)
    {
        return Results.BadRequest(new OcrResponse { Success = false, Error = "Invalid base64 string" });
    }

    var sw = Stopwatch.StartNew();
    var result = ocrService.Predict(imageBytes);
    sw.Stop();
    logger.LogInformation("OCR completed in {Elapsed}ms, Success={Success}", sw.ElapsedMilliseconds, result.Success);

    return Results.Ok(result);
});

app.MapPost("/api/ocr/upload", async (IFormFile? file) =>
{
    if (file == null || file.Length == 0)
        return Results.BadRequest(new OcrResponse { Success = false, Error = "Image file is required" });

    byte[] imageBytes;
    using (var ms = new MemoryStream())
    {
        await file.CopyToAsync(ms);
        imageBytes = ms.ToArray();
    }

    var sw = Stopwatch.StartNew();
    var result = ocrService.Predict(imageBytes);
    sw.Stop();
    logger.LogInformation("OCR upload completed in {Elapsed}ms, Success={Success}", sw.ElapsedMilliseconds, result.Success);

    return Results.Ok(result);
}).DisableAntiforgery();

app.Run();
