# 同步抽象与存储

`IBillStore` 是 `BillSync` 与具体存储后端的边界。本章讲解抽象设计、典型适配器、并发安全与持久化策略。

## 抽象定义

```csharp
namespace shmtu.sync;

public interface IBillStore
{
    /// <summary>判断某条交易号是否已存在于本地。</summary>
    bool Contains(string number);

    /// <summary>将新增条目合并到本地（调用方自行决定持久化策略）。</summary>
    void Merge(List<BillItemInfo> newBills);
}
```

## 设计动机

`BillSync` 只负责"拉取 + 解析 + 去重"，把"存哪里"完全交给调用方：

| 调用方 | 存储 |
|---|---|
| `shmtu-terminal-desktop` | Sqlite (SqlSugar) |
| 控制台 demo | 内存 List |
| 第三方 .NET 服务 | Sqlite / PostgreSQL / MySQL |
| 单元测试 | 内存 HashSet |

**好处**：

- 库不依赖任何具体数据库 → 编译快、体积小
- 调用方完全控制 schema、索引、事务
- 库代码可独立测试（用 InMemoryBillStore）

## 内存实现（库内置）

```csharp
public class InMemoryBillStore : IBillStore
{
    public List<BillItemInfo> Bills { get; } = new();
    private readonly HashSet<string> _numbers = new(StringComparer.Ordinal);

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

## 典型适配器

### Sqlite (Dapper)

```csharp
public class SqliteBillStore : IBillStore
{
    private readonly IDbConnection _db;
    private readonly HashSet<string> _cache = new(StringComparer.Ordinal);

    public SqliteBillStore(string connectionString)
    {
        _db = new SqliteConnection(connectionString);
        _db.Open();
        EnsureSchema();
        LoadCache();
    }

    private void EnsureSchema()
    {
        _db.Execute(@"
            CREATE TABLE IF NOT EXISTS bills (
                number        TEXT PRIMARY KEY,
                server_time   TEXT NOT NULL,
                position      TEXT,
                amount        REAL NOT NULL,
                balance       REAL,
                type          INTEGER NOT NULL,
                category      INTEGER,
                status        INTEGER NOT NULL,
                description   TEXT
            );
            CREATE INDEX IF NOT EXISTS idx_server_time ON bills(server_time);
        ");
    }

    private void LoadCache()
    {
        var numbers = _db.Query<string>("SELECT number FROM bills");
        foreach (var n in numbers) _cache.Add(n);
    }

    public bool Contains(string number) => _cache.Contains(number);

    public void Merge(List<BillItemInfo> newBills)
    {
        var toAdd = newBills.Where(b => _cache.Add(b.Number)).ToList();
        if (toAdd.Count == 0) return;

        using var tx = _db.BeginTransaction();
        foreach (var b in toAdd)
        {
            _db.Execute(@"
                INSERT INTO bills (number, server_time, position, amount, balance, type, category, status, description)
                VALUES (@Number, @ServerTime, @Position, @Amount, @Balance, @Type, @Category, @Status, @Description)
            ", b);
        }
        tx.Commit();
    }
}
```

### PostgreSQL (Npgsql)

```csharp
public class PostgresBillStore : IBillStore
{
    private readonly NpgsqlConnection _conn;
    private readonly HashSet<string> _cache = new();

    public PostgresBillStore(string connectionString)
    {
        _conn = new NpgsqlConnection(connectionString);
        _conn.Open();
        EnsureSchema();
        LoadCache();
    }

    // ... 类似 Sqlite，但 SQL 语法换为 PostgreSQL 方言
}
```

### JSON 文件

适合小数据量场景：

```csharp
public class JsonFileBillStore : IBillStore
{
    private readonly string _path;
    private readonly object _lock = new();
    private List<BillItemInfo> _bills = new();
    private HashSet<string> _numbers = new();

    public JsonFileBillStore(string path)
    {
        _path = path;
        if (File.Exists(path))
        {
            var json = File.ReadAllText(path);
            _bills = JsonSerializer.Deserialize<List<BillItemInfo>>(json) ?? new();
            _numbers = _bills.Select(b => b.Number).ToHashSet();
        }
    }

    public bool Contains(string number) => _numbers.Contains(number);

    public void Merge(List<BillItemInfo> newBills)
    {
        lock (_lock)
        {
            var added = 0;
            foreach (var b in newBills)
            {
                if (_numbers.Add(b.Number))
                {
                    _bills.Add(b);
                    added++;
                }
            }

            if (added > 0)
            {
                var json = JsonSerializer.Serialize(_bills, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(_path, json);
            }
        }
    }
}
```

## 并发安全

`BillSync` 默认单线程调用 `IBillStore`，但调用方可能并发同步多个账号。

### 方案 1：单 store + lock

```csharp
public class ThreadSafeBillStore : IBillStore
{
    private readonly IBillStore _inner;
    private readonly object _lock = new();

    public ThreadSafeBillStore(IBillStore inner) => _inner = inner;

    public bool Contains(string number)
    {
        lock (_lock) return _inner.Contains(number);
    }

    public void Merge(List<BillItemInfo> newBills)
    {
        lock (_lock) _inner.Merge(newBills);
    }
}
```

### 方案 2：分片锁

```csharp
public class ShardedBillStore : IBillStore
{
    private readonly IBillStore[] _shards;
    private readonly int _mask;

    public ShardedBillStore(int shardCount = 16)
    {
        _shards = Enumerable.Range(0, shardCount)
            .Select(_ => (IBillStore)new InMemoryBillStore())
            .ToArray();
        _mask = shardCount - 1;
    }

    private IBillStore GetShard(string number)
    {
        var hash = (uint)number.GetHashCode();
        return _shards[hash & _mask];
    }

    public bool Contains(string number) => GetShard(number).Contains(number);
    public void Merge(List<BillItemInfo> newBills) => GetShard(newBills[0].Number).Merge(newBills);
}
```

### 方案 3：每账号独立 store

最简单也最推荐：

```csharp
var stores = accounts.ToDictionary(
    a => a.Id,
    a => new SqliteBillStore($"bills_{a.Id}.db"));

foreach (var account in accounts)
{
    await BillSync.RunAsync(auth, account, stores[account.Id], options);
}
```

## 性能优化

### 批量插入

```csharp
// ❌ 慢
foreach (var b in newBills) _db.Insert(b);

// ✅ 快
_db.BulkInsert(newBills);
```

SqlSugar、Entity Framework Core、EFCore.BulkExtensions 都支持批量插入。

### 索引

```sql
CREATE INDEX idx_bills_server_time ON bills(server_time);
CREATE INDEX idx_bills_account_time ON bills(account_id, server_time);
CREATE INDEX idx_bills_number ON bills(number);  -- 已经是主键
```

### 去重缓存预热

```csharp
public SqliteBillStore(...)
{
    // 一次性 SELECT 加载所有 number 到内存 HashSet
    _cache = _db.Query<string>("SELECT number FROM bills").ToHashSet();
    // 后续 Contains() 是 O(1)
}
```

## 事务一致性

批量 `Merge` 必须用事务，避免同步中断后留下半截数据：

```csharp
public void Merge(List<BillItemInfo> newBills)
{
    var toAdd = newBills.Where(b => _cache.Add(b.Number)).ToList();
    if (toAdd.Count == 0) return;

    using var tx = _db.BeginTransaction();
    try
    {
        foreach (var b in toAdd) _db.Insert(b);
        tx.Commit();
    }
    catch
    {
        tx.Rollback();
        // 从 _cache 移除已 add 但实际未写入的 number
        foreach (var b in toAdd) _cache.Remove(b.Number);
        throw;
    }
}
```

## 测试适配器

库内置的 `InMemoryBillStore` 即可满足大部分测试。需要更严格的行为验证时，编写 mock：

```csharp
public class MockBillStore : IBillStore
{
    public List<string> ContainsCalls { get; } = new();
    public List<List<BillItemInfo>> MergeCalls { get; } = new();

    public bool Contains(string number)
    {
        ContainsCalls.Add(number);
        return false;  // 模拟永远不存在
    }

    public void Merge(List<BillItemInfo> newBills)
    {
        MergeCalls.Add(newBills);
    }
}

[Fact]
public async Task BillSync_CallsMergeWithCorrectBills()
{
    var store = new MockBillStore();
    var result = await BillSync.RunAsync(mockAuth, mockAccount, store, options);

    Assert.NotEmpty(store.MergeCalls);
    Assert.All(store.MergeCalls, batch => Assert.NotEmpty(batch));
}
```

## 下一步

- [NuGet 发布与 CI](/advanced/nuget-ci) — 库本身如何发布
- [多语言绑定](/advanced/multi-language) — 用其他语言调用本库
