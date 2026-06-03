# CAS 认证

`EpayAuth` 封装了上海海事大学 CAS 统一认证的完整登录流程。本章说明登录步骤、Cookie 管理和续期策略。

## CAS 登录流程

```
1. GET /cas/login → 获取 execution 参数 (隐藏字段)
2. GET /cas/captcha → 获取验证码图片 (PNG)
3. 用户输入或 OCR 识别 → captcha 字符串
4. POST /cas/login (form: username, password, captcha, execution) → 302 重定向
5. 跟随重定向 → 拿到 TGC Cookie
6. 用 TGC 访问 ePay 系统的受保护页面
```

## 核心 API

```csharp
namespace shmtu.cas.auth;

public class EpayAuth
{
    public CookieContainer Cookies { get; }

    // 1. 获取验证码图片（无需登录）
    public static Task<byte[]> FetchCaptchaAsync(CancellationToken ct = default);

    // 2. 完整登录流程
    public Task LoginAsync(
        string username,
        string password,
        string captcha,
        CancellationToken ct = default);

    // 3. 续期
    public Task<bool> RefreshAsync(CancellationToken ct = default);

    // 4. 登出
    public Task LogoutAsync(CancellationToken ct = default);

    // 5. 检查是否已登录
    public bool IsLoggedIn { get; }
}
```

## 完整登录示例

```csharp
using shmtu.cas.auth;
using shmtu.cas.captcha;

// 1. 获取验证码
var captchaBytes = await EpayAuth.FetchCaptchaAsync();

// 2. 用 OCR 识别或手动输入
ICaptchaResolver ocr = new RemoteOcrHttpCaptchaResolver("http://127.0.0.1:5000");
var answer = await ocr.ResolveAsync(captchaBytes);

// 3. 登录
var auth = new EpayAuth();
try
{
    await auth.LoginAsync("2024001", "your_password", answer.Text);
    Console.WriteLine("登录成功");
}
catch (CaptchaFailedException)
{
    Console.WriteLine("验证码错误，请重试");
}
catch (CasAuthException ex)
{
    Console.WriteLine($"登录失败: {ex.Message}");
}
```

## Cookie 管理

`EpayAuth` 内部用 `CookieContainer` 跟踪 Cookie，**不要在每次请求时新建 `EpayAuth` 实例**：

```csharp
// ❌ 错误：每次新建实例会丢失 Cookie
foreach (var account in accounts)
{
    var auth = new EpayAuth();
    await auth.LoginAsync(account.User, account.Pwd, captcha);
    var bills = await BillSync.RunAsync(auth, ...);
}

// ✅ 正确：单例复用
var auth = new EpayAuth();
await auth.LoginAsync(username, password, captcha);
foreach (var account in accounts)
{
    var bills = await BillSync.RunAsync(auth, account, store, options);
}
```

## 续期策略

CAS TGC 默认有效期约 2 小时。续期流程：

```csharp
public class SessionRefreshService
{
    private readonly EpayAuth _auth;
    private Timer? _timer;

    public void StartAutoRefresh()
    {
        _timer = new Timer(async _ =>
        {
            if (!_auth.IsLoggedIn)
            {
                try { await _auth.RefreshAsync(); }
                catch { /* 通知 UI 重新登录 */ }
            }
        }, null, TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(30));
    }
}
```

> 续期失败时不要抛异常给用户，应该**静默重试**或**通知 UI 弹窗**重新输入密码。

## 多账号并发

`EpayAuth` 内部用 `HttpClient` + `CookieContainer`，**不是线程安全的**。多账号并发需要：

```csharp
// 每个账号一个 EpayAuth 实例
var tasks = accounts.Select(async acc =>
{
    var auth = new EpayAuth();
    await auth.LoginAsync(acc.User, acc.Pwd, captcha);
    return await BillSync.RunAsync(auth, acc, store, options);
});
var results = await Task.WhenAll(tasks);
```

> 验证码是**全局共享**的：一次登录拿到的验证码只能用于该次请求。但不同账号可以分别登录。

## 错误码

| 异常 | 含义 | 处理 |
|---|---|---|
| `CaptchaFailedException` | 验证码错误 | 重新获取 + 识别 + 重试 |
| `CasAuthException` | 通用登录失败 | 检查用户名/密码 |
| `HttpRequestException` | 网络错误 | 重试或检查网络 |
| `TaskCanceledException` | 超时 | 增大 `HttpClient.Timeout` |

## SSL / 证书

校园 CAS 服务器使用自签名证书，开发环境需要：

```csharp
// 仅开发环境！
var handler = new HttpClientHandler
{
    ServerCertificateCustomValidationCallback = (_, _, _, _) => true
};
```

**生产环境不要禁用证书验证**。

## 日志

启用 `Microsoft.Extensions.Logging`：

```csharp
using var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
var auth = new EpayAuth(loggerFactory);
```

可看到每次 HTTP 请求的详细日志。

## 下一步

- [验证码解析器](/guide/captcha-resolver) — 自动化验证码
- [账单同步 (BillSync)](/guide/bill-sync) — 登录后拉取账单
