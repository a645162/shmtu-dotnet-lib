# 账单同步 (BillSync)

`BillSync` 是 `shmtu-dotnet-lib` 提供的**通用账单同步抽象**，把登录态、翻页、解析、去重、早停、进度推送统一封装。调用方只需要实现 `IBillStore` 接口。

## 核心 API

```csharp
namespace shmtu.sync;

public static class BillSync
{
    public static Task<SyncResult> RunAsync(
        EpayAuth auth,
        AccountInfo account,
        IBillStore store,
        SyncOptions options = null,
        IProgress<PageSyncProgress> progress = null,
        CancellationToken ct = default);
}

public interface IBillStore
{
    bool Contains(string number);
    void Merge(List<BillItemInfo> newBills);
}
```

## 同步流程

```
┌──────────────────────────────────────────────────────────┐
│  1. 拉取第 1 页 HTML (Flurl.Http + CookieContainer)       │
│  2. HtmlAgilityPack 解析为 BillItemInfo[]                │
│  3. 逐条判断 IBillStore.Contains(number)                 │
│     - 已存在 → 计数 +1                                   │
│     - 新条目 → 加入 newBills                             │
│  4. 连续 EarlyStopThreshold 条已存在 → 早停              │
│  5. IBillStore.Merge(newBills)                           │
│  6. IProgress.Report(进度)                                │
│  7. 翻下一页 / 退出                                       │
└──────────────────────────────────────────────────────────┘
```

## 完整示例

```csharp
using shmtu.sync;
using shmtu.datatype.bill;
using shmtu.cas.auth;

// 1. 准备存储
var store = new SqliteBillStore(connectionString);

// 2. 配置同步选项
var options = new SyncOptions
{
    StartPage = 1,
    MaxPages = 100,
    BillType = BillType.All,
    EarlyStopThreshold = 5
};

// 3. 进度回调
var progress = new Progress<PageSyncProgress>(p =>
{
    Console.WriteLine($"[{p.CurrentPage}/{p.TotalPages}] " +
                      $"+{p.NewCount} (累计翻 {p.PagesFetched} 页)");
});

// 4. 执行同步
var auth = new EpayAuth();
await auth.LoginAsync("2024001", "password", captcha);

var account = new AccountInfo { Username = "2024001" };
var result = await BillSync.RunAsync(auth, account, store, options, progress);

Console.WriteLine($"同步完成: 新增 {result.NewCount}, 早停 {result.StoppedEarly}");
```

## 同步选项

```csharp
public sealed record SyncOptions
{
    public int StartPage { get; init; } = 1;
    public int MaxPages { get; init; } = 100;
    public BillType BillType { get; init; } = BillType.All;
    public int EarlyStopThreshold { get; init; } = 5;
}
```

| 字段 | 默认 | 说明 |
|---|---|---|
| `StartPage` | 1 | 从哪页开始（全量=1，增量=从状态恢复） |
| `MaxPages` | 100 | 最多翻多少页（防失控） |
| `BillType` | All | `All` / `Consume` / `Recharge` |
| `EarlyStopThreshold` | 5 | 连续多少条已存在即停 |

## 同步结果

```csharp
public sealed record SyncResult
{
    public int NewCount { get; init; }
    public int PagesFetched { get; init; }
    public bool StoppedEarly { get; init; }
    public TimeSpan Duration { get; init; }
}
```

## 进度推送

```csharp
public sealed record PageSyncProgress
{
    public int CurrentPage { get; init; }
    public int TotalPages { get; init; }   // -1 表示未知
    public int NewCount { get; init; }
    public int PagesFetched { get; init; }
}
```

`IProgress<T>.Report()` 是线程安全的，可在任意线程调用。UI 层用 `Progress<T>` 自动捕获 `SynchronizationContext`。

## IBillStore 适配

### Sqlite 实现（参考 `shmtu-terminal-desktop`）

```csharp
public class SqliteBillStore : IBillStore
{
    private readonly IDbConnection _db;
    private readonly HashSet<string> _cache = new();

    public SqliteBillStore(string connectionString)
    {
        _db = new SqliteConnection(connectionString);
        // 预加载已存在的交易号到缓存
        _cache = _db.Query<string>("SELECT number FROM bills").ToHashSet();
    }

    public bool Contains(string number) => _cache.Contains(number);

    public void Merge(List<BillItemInfo> newBills)
    {
        using var tx = _db.BeginTransaction();
        foreach (var b in newBills.Where(x => _cache.Add(x.Number)))
        {
            _db.Insert("bills", b);
        }
        tx.Commit();
    }
}
```

### 内存实现（适合测试）

```csharp
public class InMemoryBillStore : IBillStore
{
    public List<BillItemInfo> Bills { get; } = new();
    private readonly HashSet<string> _numbers = new();

    public bool Contains(string number) => _numbers.Contains(number);

    public void Merge(List<BillItemInfo> newBills)
    {
        foreach (var b in newBills.Where(x => _numbers.Add(x.Number)))
            Bills.Add(b);
    }
}
```

### JSON 文件实现

```csharp
public class JsonFileBillStore : IBillStore
{
    private readonly string _path;
    private List<BillItemInfo> _cache = new();
    private HashSet<string> _numbers = new();

    public JsonFileBillStore(string path)
    {
        _path = path;
        if (File.Exists(path))
        {
            _cache = JsonSerializer.Deserialize<List<BillItemInfo>>(
                File.ReadAllText(path));
            _numbers = _cache.Select(b => b.Number).ToHashSet();
        }
    }

    public bool Contains(string number) => _numbers.Contains(number);

    public void Merge(List<BillItemInfo> newBills)
    {
        var added = newBills.Where(x => _numbers.Add(x.Number)).ToList();
        _cache.AddRange(added);
        File.WriteAllText(_path, JsonSerializer.Serialize(_cache));
    }
}
```

## 早停机制

增量同步的关键优化：

```
第 1 页：[新, 新, 新, 新, 新]                    count=0
第 2 页：[新, 新, 新, 新, 新]                    count=0
第 3 页：[新, 新, 已存在, 已存在, 已存在]           count=3
第 4 页：[已存在, 已存在, 已存在, 已存在, 已存在]   count=8 → 早停
```

阈值 `EarlyStopThreshold` 的选择：

- **太小**（如 1）：CAS 偶尔乱序时容易误停
- **太大**（如 50）：增量同步速度慢
- **推荐**：3-10

## 取消与超时

```csharp
var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
try
{
    var result = await BillSync.RunAsync(auth, account, store, options,
        progress: null, ct: cts.Token);
}
catch (OperationCanceledException)
{
    Console.WriteLine("同步被取消");
}
```

## 错误处理

```csharp
try
{
    await BillSync.RunAsync(...);
}
catch (HttpRequestException ex)
{
    // 网络问题
}
catch (ParseException ex)
{
    // CAS 改了 HTML 结构，需更新解析器
}
catch (CasAuthException ex)
{
    // 登录态失效，需重新登录
}
```

## 下一步

- [HTML 解析](/guide/html-parser) — 解析器细节
- [数据导出](/guide/export) — 同步后导出
