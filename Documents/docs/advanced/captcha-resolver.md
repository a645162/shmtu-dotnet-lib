# 验证码解析器

`ICaptchaResolver` 是 `shmtu-dotnet-lib` 中负责"给定验证码图片，返回答案字符串"的抽象。本章讲解抽象定义、已有实现与如何扩展。

## 接口定义

```csharp
namespace shmtu.cas.captcha;

public interface ICaptchaResolver
{
    Task<CaptchaAnswer> ResolveAsync(
        byte[] imageBytes,
        CancellationToken ct = default);
}

public sealed record CaptchaAnswer(string Text, CaptchaAnswerKind Kind)
{
    public static CaptchaAnswer Manual(string text) =>
        new(text, CaptchaAnswerKind.Manual);
    public static CaptchaAnswer Auto(string text) =>
        new(text, CaptchaAnswerKind.Auto);
}

public enum CaptchaAnswerKind
{
    Manual,   // 用户手动输入
    Auto      // OCR 自动识别
}
```

## 异常

```csharp
// 验证码图片无效（解析失败、二进制损坏）
public class InvalidCaptchaImageException : Exception { }

// 解析器自身无法继续（网络断开、模型文件丢失等）
public class CaptchaResolverUnavailableException : Exception { }

// 需要用户手动输入（由 UI 层捕获后弹窗）
public class CaptchaRequiresManualInputException : CaptchaResolverUnavailableException { }
```

## 三种内置实现

### 1. ManualCaptchaResolver

抛出异常让 UI 层弹窗：

```csharp
public class ManualCaptchaResolver : ICaptchaResolver
{
    public Task<CaptchaAnswer> ResolveAsync(byte[] imageBytes, CancellationToken ct = default)
    {
        throw new CaptchaRequiresManualInputException();
    }
}
```

UI 层（如 `shmtu-terminal-desktop`）捕获异常后弹出输入框，用户输入后调用 `CaptchaAnswer.Manual(text)`。

### 2. RemoteOcrHttpCaptchaResolver

调用本地 HTTP OCR 服务：

```csharp
public class RemoteOcrHttpCaptchaResolver : ICaptchaResolver
{
    private readonly string _baseUrl;
    private readonly HttpClient _http;
    private readonly int _retries;

    public RemoteOcrHttpCaptchaResolver(string baseUrl, int retries = 3)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _http = new HttpClient { BaseAddress = new Uri(_baseUrl) };
        _retries = retries;
    }

    public async Task<CaptchaAnswer> ResolveAsync(byte[] imageBytes, CancellationToken ct = default)
    {
        for (int i = 0; i < _retries; i++)
        {
            try
            {
                var response = await _http.PostAsJsonAsync(
                    "/captcha/recognize",
                    new { image = Convert.ToBase64String(imageBytes) },
                    ct);

                response.EnsureSuccessStatusCode();

                var result = await response.Content
                    .ReadFromJsonAsync<OcrResult>(cancellationToken: ct);

                if (string.IsNullOrEmpty(result?.Text))
                    throw new InvalidCaptchaImageException();

                return CaptchaAnswer.Auto(result.Text);
            }
            catch (HttpRequestException) when (i < _retries - 1)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(200 * (i + 1)), ct);
            }
        }

        throw new CaptchaResolverUnavailableException("OCR HTTP 服务不可用");
    }
}
```

### 3. RemoteOcrHttpCaptchaResolver（Flurl 版）

链式调用，更易读：

```csharp
public class RemoteOcrHttpCaptchaResolver : ICaptchaResolver
{
    private readonly string _baseUrl;

    public async Task<CaptchaAnswer> ResolveAsync(byte[] imageBytes, CancellationToken ct = default)
    {
        try
        {
            var result = await _baseUrl
                .AppendPathSegment("captcha/recognize")
                .PostJsonAsync(new { image = Convert.ToBase64String(imageBytes) }, ct)
                .ReceiveJson<OcrResult>();

            return CaptchaAnswer.Auto(result.Text);
        }
        catch (FlurlHttpException)
        {
            throw new CaptchaResolverUnavailableException();
        }
    }
}
```

## 编写自定义解析器

### 接入第三方打码平台

```csharp
public class CloudCaptchaResolver : ICaptchaResolver
{
    private readonly HttpClient _http;
    private readonly string _apiKey;

    public async Task<CaptchaAnswer> ResolveAsync(byte[] imageBytes, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync(
            "https://api.cloud-captcha.com/v1/recognize",
            new
            {
                apikey = _apiKey,
                image = Convert.ToBase64String(imageBytes),
                type = "shmtu"
            }, ct);

        var result = await response.Content.ReadFromJsonAsync<CloudCaptchaResult>(cancellationToken: ct);

        if (result.Code != 0)
            throw new CaptchaResolverUnavailableException(result.Message);

        return CaptchaAnswer.Auto(result.Text);
    }
}
```

### 集成多个解析器（fallback）

```csharp
public class FallbackCaptchaResolver : ICaptchaResolver
{
    private readonly ICaptchaResolver[] _resolvers;

    public FallbackCaptchaResolver(params ICaptchaResolver[] resolvers)
    {
        _resolvers = resolvers;
    }

    public async Task<CaptchaAnswer> ResolveAsync(byte[] imageBytes, CancellationToken ct = default)
    {
        var errors = new List<Exception>();
        foreach (var resolver in _resolvers)
        {
            try
            {
                return await resolver.ResolveAsync(imageBytes, ct);
            }
            catch (CaptchaRequiresManualInputException)
            {
                throw;  // 手动模式不 fallback
            }
            catch (Exception ex)
            {
                errors.Add(ex);
            }
        }

        throw new AggregateException("所有解析器都失败", errors);
    }
}
```

使用：

```csharp
var resolver = new FallbackCaptchaResolver(
    new RemoteOcrHttpCaptchaResolver("http://gpu-server:5000"),  // GPU OCR（快）
    new RemoteOcrHttpCaptchaResolver("http://cpu-server:5000"),  // CPU OCR（备份）
    new ManualCaptchaResolver()                                    // 手动（最后兜底）
);
```

## 与 UI 层的集成

在 `shmtu-terminal-desktop` 中，ViewModel 监听异常后弹窗：

```csharp
public async Task<CaptchaAnswer> ResolveWithUiFallbackAsync(byte[] image)
{
    try
    {
        return await _resolver.ResolveAsync(image);
    }
    catch (CaptchaRequiresManualInputException)
    {
        // 弹窗让用户输入
        var text = await ShowCaptchaDialogAsync(image);
        if (string.IsNullOrEmpty(text))
            throw new OperationCanceledException("用户取消");

        return CaptchaAnswer.Manual(text);
    }
}
```

## 性能对比

| 解析器 | 平均延迟 | 准确率 |
|---|---|---|
| ManualCaptchaResolver | 5-15s | 100% |
| RemoteOcrHttpCaptchaResolver (GPU) | 5-10ms | ~95% |
| RemoteOcrHttpCaptchaResolver (CPU) | 30-80ms | ~92% |
| 第三方打码平台 | 1-3s | ~99% |

## 下一步

- [ONNX 模型格式](/advanced/onnx-models) — 自训练或微调
- [同步抽象与存储](/advanced/sync-store) — 拿到答案后的同步
