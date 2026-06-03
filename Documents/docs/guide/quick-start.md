# 快速开始

从 NuGet 引用 `shmtu-dotnet-lib` 到第一次成功同步账单的完整流程。

## 前置条件

| 工具 | 版本 | 用途 |
|---|---|---|
| .NET SDK | 8.0+ | 编译/运行示例 |
| 网络 | 校园网或 VPN | 访问 CAS 服务器 |
| ONNX 模型 | ~170MB | 验证码识别（可选） |

## 1. 创建项目

```bash
dotnet new console -n MyBillApp
cd MyBillApp
dotnet add package shmtu-dotnet-lib
```

如果需要 OCR 识别验证码：

```bash
dotnet add package shmtu-ocr-onnx-lib
```

## 2. 下载 ONNX 模型（可选）

首次使用需要下载三个 ONNX 模型：

```bash
mkdir -p ocr/models
# 从 GitHub Release 下载（详见 scripts/download_github_release.py）
# 或通过 OCR HTTP 服务自动下载
```

或跳过这步，使用**远程 OCR HTTP 服务**：

```bash
# 另起一个终端
cd path/to/shmtu-dotnet-lib
docker compose -f docker-compose.yml up -d   # 启动 HTTP OCR 服务在 5000 端口
```

## 3. 第一次登录与同步

```csharp
using shmtu.cas.auth;
using shmtu.cas.captcha;
using shmtu.datatype.bill;
using shmtu.sync;
using shmtu.parser.bill;
using shmtu.datatype.bill;

// 1. 准备存储 (此处用内存示例)
var store = new ListBillStore();

// 2. 解析验证码
ICaptchaResolver resolver = new RemoteOcrHttpCaptchaResolver(
    "http://127.0.0.1:5000");

var captchaImg = await EpayAuth.FetchCaptchaAsync();
var answer = await resolver.ResolveAsync(captchaImg);

// 3. 登录 CAS
var auth = new EpayAuth();
await auth.LoginAsync("2024001", "your_password", answer);

// 4. 同步账单
var options = new SyncOptions
{
    BillType = BillType.All,
    MaxPages = 50,
    EarlyStopThreshold = 5
};

var account = new AccountInfo { Username = "2024001" };
var result = await BillSync.RunAsync(auth, account, store, options);

Console.WriteLine($"新增 {result.NewCount} 条,翻了 {result.PagesFetched} 页");
```

## 4. 处理结果

`IBillStore.Merge()` 把新条目合并进来。`store` 是调用方控制的，库不关心是 Sqlite / JSON / 内存。

```csharp
// 内存实现示例
class ListBillStore : IBillStore
{
    public List<BillItemInfo> Bills { get; } = new();
    private readonly HashSet<string> _numbers = new();

    public bool Contains(string number) => _numbers.Contains(number);

    public void Merge(List<BillItemInfo> newBills)
    {
        foreach (var b in newBills)
        {
            if (_numbers.Add(b.Number))
                Bills.Add(b);
        }
    }
}
```

## 5. 验证

打印前 5 条账单：

```csharp
foreach (var b in store.Bills.Take(5))
{
    Console.WriteLine($"{b.ServerTime:yyyy-MM-dd HH:mm}  " +
                      $"{b.Position,-20}  {b.Amount,8:C}  {b.Category}");
}
```

## 下一步

- [CAS 认证](/guide/cas-auth) — 详细登录流程
- [账单同步 (BillSync)](/guide/bill-sync) — 同步选项与存储对接
- [ONNX 推理](/guide/onnx-inference) — 加载模型与识别
