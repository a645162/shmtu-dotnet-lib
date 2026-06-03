# CAS 登录链路

本章详细讲解 `EpayAuth.LoginAsync` 的内部时序、重试策略、错误分类与扩展点。

## 完整时序图

```
Client                         CAS Server                    ePay Server
  │                                  │                              │
  │ 1. GET /cas/login                │                              │
  │─────────────────────────────────→│                              │
  │ ← 200 + execution token          │                              │
  │                                  │                              │
  │ 2. GET /cas/captcha              │                              │
  │─────────────────────────────────→│                              │
  │ ← 200 + PNG bytes                │                              │
  │                                  │                              │
  │ 3. [OCR 识别 / 手动输入]          │                              │
  │ 4. POST /cas/login               │                              │
  │   (form: username, password,     │                              │
  │    captcha, execution, _eventId) │                              │
  │─────────────────────────────────→│                              │
  │ ← 302 (重定向)                   │                              │
  │                                  │                              │
  │ 5. 跟随重定向 (3-5 次)           │                              │
  │   GET /cas/login?ticket=xxx      │                              │
  │─────────────────────────────────→│                              │
  │ ← 302 (到 ePay 应用)             │                              │
  │                                  │                              │
  │ 6. GET ePay 受保护页面           │                              │
  │──────────────────────────────────┼─────────────────────────────→│
  │                                  │ ← 200 (登录态确认)            │
  │ ← 200 (HTML)                     │                              │
  │                                  │                              │
  ▼                                  ▼                              ▼
```

## 关键步骤的代码实现

### 1. 获取 execution token

```csharp
// Flurl 链式调用
var response = await "https://cas.shmtu.edu.cn/cas/login"
    .GetAsync();
var html = await response.GetStringAsync();

var doc = new HtmlDocument();
doc.LoadHtml(html);
var execution = doc.DocumentNode
    .SelectSingleNode("//input[@name='execution']")
    .Attributes["value"].Value;
```

### 2. 获取验证码

```csharp
public static async Task<byte[]> FetchCaptchaAsync(CancellationToken ct = default)
{
    var response = await "https://cas.shmtu.edu.cn/cas/captcha"
        .GetAsync(ct);
    return await response.GetBytesAsync();
}
```

### 3-4. 提交登录

```csharp
var response = await "https://cas.shmtu.edu.cn/cas/login"
    .PostUrlEncodedAsync(new
    {
        username,
        password,
        captcha,
        execution,
        _eventId = "submit",
        geolocation = ""
    }, ct);

// 检查是否登录失败（CAS 返回登录页 HTML 而不是重定向）
if (response.ResponseMessage.Headers.Location?.AbsolutePath != "/cas/login")
    throw new CaptchaFailedException();
```

### 5. 跟随重定向

```csharp
// Flurl 默认自动跟随 3 次重定向
// 如果需要更多，设置：
var flurlClient = new FlurlClient("https://cas.shmtu.edu.cn")
{
    Settings = {
        Redirects = new Flurl.Redis.Redirect[]
        {
            new Flurl.Redis.Fluent.Redirect().WithMaxCount(10)
        }
    }
};
```

## Cookie 持久化

`EpayAuth` 内部用 `CookieContainer`：

```csharp
public class EpayAuth
{
    private readonly HttpClient _http;
    public CookieContainer Cookies { get; }

    public EpayAuth()
    {
        Cookies = new CookieContainer();
        var handler = new HttpClientHandler
        {
            CookieContainer = Cookies,
            UseCookies = true,
            AllowAutoRedirect = true
        };
        _http = new HttpClient(handler);
    }
}
```

## 重试策略

```csharp
public async Task LoginAsync(string username, string password, string captcha, CancellationToken ct = default)
{
    const int maxRetries = 3;
    for (int i = 0; i < maxRetries; i++)
    {
        try
        {
            await LoginInternalAsync(username, password, captcha, ct);
            return;
        }
        catch (CaptchaFailedException) when (i < maxRetries - 1)
        {
            // 验证码错误：重新获取 + OCR + 重试
            captcha = await GetNewCaptchaAsync(ct);
        }
        catch (HttpRequestException) when (i < maxRetries - 1)
        {
            // 网络错误：等待后重试
            await Task.Delay(TimeSpan.FromSeconds(2 * (i + 1)), ct);
        }
    }
}
```

## 错误分类

```csharp
// 基类
public class CasAuthException : Exception { }

// 验证码错误（可重试）
public class CaptchaFailedException : CasAuthException { }

// 账号密码错误（不可重试，需用户介入）
public class InvalidCredentialsException : CasAuthException { }

// 账号被锁定（不可重试）
public class AccountLockedException : CasAuthException { }

// 网络错误（可重试）
public class CasNetworkException : CasAuthException { }

// 服务器维护（可重试）
public class CasMaintenanceException : CasAuthException { }
```

## 续期机制

```csharp
public async Task<bool> RefreshAsync(CancellationToken ct = default)
{
    try
    {
        // 访问任意受保护页面触发 Cookie 续期
        var response = await "https://epay.shmtu.edu.cn/user/info"
            .WithCookies(Cookies)
            .GetAsync(ct);

        return response.ResponseMessage.IsSuccessStatusCode;
    }
    catch
    {
        return false;
    }
}
```

## 扩展点

### 自定义 HTTP 处理器

```csharp
public class MyEpayAuth : EpayAuth
{
    public MyEpayAuth() : base()
    {
        // 注入自定义 handler
    }
}
```

### 自定义 User-Agent

```csharp
var auth = new EpayAuth();
auth.SetHeader("User-Agent", "MyApp/1.0");
```

### 代理支持

```csharp
var handler = new HttpClientHandler
{
    Proxy = new WebProxy("http://proxy:8080"),
    UseProxy = true
};
var auth = new EpayAuth(handler);
```

## 性能考虑

- 一次完整登录需要 3-5 次 HTTP 请求，耗时 1-3s
- 登录后访问账单页直接复用 Cookie，无额外开销
- 重试策略建议：3 次重试，指数退避

## 安全考虑

- ⚠️ **不要在 URL 中传递密码**（可能被日志记录）
- ✅ 用 POST + form-encoded 提交
- ✅ CookieContainer 自动处理 HttpOnly
- ⚠️ TGC Cookie 应仅在 HTTPS 下传输

## 下一步

- [验证码解析器](/advanced/captcha-resolver) — 自动化的关键
- [同步抽象与存储](/advanced/sync-store) — 登录后的拉取
