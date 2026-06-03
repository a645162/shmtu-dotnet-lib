# 数据导出

`shmtu-dotnet-lib/export/` 提供多种格式的账单导出：CSV、JSON、钱迹 CSV。本章说明每种格式的字段、示例和使用方法。

## 支持的格式

| 格式 | 适配器 | 用途 |
|---|---|---|
| CSV | `CsvFile` | Excel 打开分析 |
| JSON | `JsonFile` | 二次处理、备份 |
| 钱迹 | `Qianji` | 导入"钱迹"记账 App |

## CSV 导出

```csharp
using shmtu.export.bill;

var exporter = new CsvFile("bills.csv");
exporter.WriteHeader();
foreach (var bill in bills)
{
    exporter.WriteRow(bill);
}
exporter.Close();
```

### 字段

```csv
交易号,交易时间,商户,金额,余额,类型,分类,状态,描述
20240115001,2024-01-15 12:30:45,第一食堂,-12.50,87.50,消费,食堂,正常,
```

| 字段 | 说明 |
|---|---|
| 交易号 | 唯一标识 |
| 交易时间 | ISO 8601 |
| 商户 | position 字段 |
| 金额 | 正数=消费，负数=充值 |
| 余额 | 交易后余额 |
| 类型 | 消费/充值 |
| 分类 | 中文 |
| 状态 | 正常/已退 |
| 描述 | 备注 |

## JSON 导出

```csharp
var exporter = new JsonFile("bills.json");
exporter.WriteAll(bills);
```

### 格式

```json
[
  {
    "number": "20240115001",
    "serverTime": "2024-01-15T12:30:45",
    "position": "第一食堂",
    "amount": -12.50,
    "balance": 87.50,
    "type": "Consume",
    "category": "Canteen",
    "status": "Normal",
    "description": ""
  }
]
```

> JSON 是**类型安全**的 — `amount` 保留为数字，`serverTime` 是 ISO 8601 字符串。

## 钱迹 CSV

适配"钱迹"App 的导入格式：

```csharp
var exporter = new Qianji("bills-qianji.csv");
exporter.WriteHeader();
foreach (var bill in bills.Where(b => b.Type == BillType.Consume))
{
    exporter.WriteRow(bill);
}
```

### 钱迹格式

```csv
,2024-01-15 12:30:45,第一食堂,12.50,,,支出,食堂,微信支付,,
```

字段含义：

| 钱迹字段 | 来源 |
|---|---|
| 1 | （空） |
| 2 | 交易时间 |
| 3 | 商户 |
| 4 | 金额（正数） |
| 5-6 | （空） |
| 7 | "支出" / "收入" |
| 8 | 分类 |
| 9-11 | 账户/备注 |

> 充值/退款不写入钱迹 CSV（钱迹对账户间转账支持有限）。

## 批量导出示例

```csharp
public class BillExportService
{
    public async Task ExportAllAsync(IEnumerable<BillItemInfo> bills, string outputDir)
    {
        Directory.CreateDirectory(outputDir);
        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

        // CSV
        var csv = new CsvFile(Path.Combine(outputDir, $"bills_{stamp}.csv"));
        csv.WriteHeader();
        foreach (var b in bills) csv.WriteRow(b);
        csv.Close();

        // JSON
        var json = new JsonFile(Path.Combine(outputDir, $"bills_{stamp}.json"));
        await json.WriteAllAsync(bills);

        // 钱迹
        var qj = new Qianji(Path.Combine(outputDir, $"bills_{stamp}_qianji.csv"));
        qj.WriteHeader();
        foreach (var b in bills.Where(b => b.Type == BillType.Consume)) qj.WriteRow(b);
        qj.Close();
    }
}
```

## 自定义导出

继承基类实现自定义格式：

```csharp
public class MyCustomExporter : BillExporterBase
{
    public MyCustomExporter(string path) : base(path) { }

    protected override void WriteHeader()
    {
        WriteLine("id\tdatetime\tamount\tcategory");
    }

    protected override void WriteRow(BillItemInfo bill)
    {
        WriteLine($"{bill.Number}\t{bill.ServerTime:yyyy-MM-dd}\t" +
                  $"{bill.Amount}\t{bill.Category}");
    }
}
```

## 编码与换行

所有导出器默认使用 **UTF-8 with BOM**，确保 Excel 正确识别中文：

```csharp
var exporter = new CsvFile(path, encoding: new UTF8Encoding(true));
```

如需纯 UTF-8（不带 BOM）：

```csharp
var exporter = new CsvFile(path, encoding: new UTF8Encoding(false));
```

## 下一步

- [账单分类](/guide/bill-classifier) — 导出前确保分类正确
- [Docker 部署](/guide/docker-deployment) — 在容器中导出
