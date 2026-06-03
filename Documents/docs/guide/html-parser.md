# HTML 解析

CAS 服务器返回的是**结构化 HTML 页面**（账单列表、详情、热水等）。`shmtu-dotnet-lib/parser/` 提供 `HtmlAgilityPack` 封装的解析器。

## 解析器列表

| 解析器 | 用途 | 入口类 |
|---|---|---|
| `BillParser` | 账单列表页 | `BillParser.Parse(string html)` |
| `BillHtmlParser` | 账单 HTML 包装 | `BillHtmlParser.Parse(string html)` |
| `BillItemHtmlParser` | 单笔交易 | `BillItemHtmlParser.Parse(HtmlNode row)` |
| `PageCountHtmlParser` | 总页数 | `PageCountHtmlParser.Parse(string html)` |
| `HotWaterHtmlParser` | 热水使用 | `HotWaterHtmlParser.Parse(string html)` |

## 账单列表解析

`BillParser.Parse(html)` 返回 `(int totalPages, List<BillItemInfo> items)`。

```csharp
using shmtu.parser.bill;
using Flurl.Http;

var html = await "https://epay.shmtu.edu.cn/bill/page/1"
    .WithCookies(auth.Cookies)
    .GetStringAsync();

var (totalPages, items) = BillParser.Parse(html);

Console.WriteLine($"共 {totalPages} 页,本页 {items.Count} 条");
foreach (var item in items)
{
    Console.WriteLine($"{item.ServerTime}  {item.Position}  {item.Amount}");
}
```

## BillItemInfo 字段

```csharp
public sealed record BillItemInfo
{
    public string Number { get; init; }       // 交易号
    public DateTime ServerTime { get; init; }
    public string Position { get; init; }     // 商户/位置
    public decimal Amount { get; init; }      // 元
    public decimal? Balance { get; init; }    // 交易后余额
    public BillType Type { get; init; }       // 消费/充值
    public BillItemStatus Status { get; init; } // 正常/已退
    public string Description { get; init; }
}
```

## 单笔解析

如果想自定义表格选择器：

```csharp
using HtmlAgilityPack;
using shmtu.parser.bill;

var doc = new HtmlDocument();
doc.LoadHtml(html);
var rows = doc.DocumentNode.SelectNodes("//table[@id='bills']/tbody/tr");

var items = rows.Select(BillItemHtmlParser.Parse).ToList();
```

## 翻页

```csharp
// 第一页
var (totalPages, _) = BillParser.Parse(htmlPage1);

// 后续页
for (int page = 2; page <= totalPages; page++)
{
    var html = await $"https://epay.shmtu.edu.cn/bill/page/{page}"
        .WithCookies(auth.Cookies)
        .GetStringAsync();
    var (_, items) = BillParser.Parse(html);
    store.Merge(items);
}
```

## 自定义解析器

如果 CAS 改了 HTML 结构，可以继承 `BillHtmlParser` 重写：

```csharp
public class MyBillParser : BillHtmlParser
{
    protected override HtmlNode FindBillsTable(HtmlDocument doc)
    {
        // 自定义 XPath
        return doc.DocumentNode
            .SelectSingleNode("//div[@class='new-bills-table']/table");
    }
}
```

## 错误处理

```csharp
try
{
    var (pages, items) = BillParser.Parse(html);
}
catch (ParseException ex)
{
    // HTML 结构不匹配预期
    // 1. 检查 CAS 是否改版
    // 2. 用浏览器开发者工具对比 HTML
    // 3. 提 Issue 附 HTML 样本
}
```

## 字符串扩展

`StringExtensions` 提供一些 HTML 处理工具：

```csharp
using shmtu.parser;

var text = "<td>2024-01-15</td>".StripHtmlTags();              // "2024-01-15"
var num = "￥12.34".ParseCurrency();                           // 12.34m
var date = "2024-01-15 12:30:45".ParseServerDateTime();        // DateTime
```

## 性能

- 单页解析 < 10ms（典型 10-30 条）
- 100 页 < 1s
- 主要瓶颈是网络请求，解析本身不是热点

## 单元测试

```csharp
[Fact]
public void BillParser_HandlesEmptyPage()
{
    var html = "<html><body>暂无数据</body></html>";
    var (pages, items) = BillParser.Parse(html);
    Assert.Equal(0, pages);
    Assert.Empty(items);
}

[Fact]
public void BillParser_ParsesMultipleItems()
{
    var html = File.ReadAllText("Samples/bill-page-1.html");
    var (pages, items) = BillParser.Parse(html);
    Assert.True(pages > 0);
    Assert.NotEmpty(items);
    Assert.All(items, i => Assert.NotEmpty(i.Number));
}
```

## 下一步

- [账单分类](/guide/bill-classifier) — 解析后分类
- [数据导出](/guide/export) — 解析后导出
