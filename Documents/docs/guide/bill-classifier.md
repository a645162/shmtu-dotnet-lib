# 账单分类

`BillClassifier` 把 `BillItemInfo.Position`（商户/位置描述）映射到 13 个内置分类。本章说明分类规则、扩展方式和性能。

## 13 个内置分类

```csharp
public enum BillCategory
{
    Deposit,       // 充值
    Electricity,   // 电费
    Bath,          // 洗澡
    HotWater,      // 热水
    Cake,          // 点心
    Canteen,       // 食堂
    Library,       // 图书馆
    Hospital,      // 校医院
    Shop,          // 超市
    Laundry,       // 洗衣
    Network,       // 网络
    Transport,     // 交通
    Other          // 其他
}
```

## 分类规则

`PositionTranslator.cs` 维护一个关键字 → 分类的映射表：

```csharp
private static readonly Dictionary<string, BillCategory> _rules = new()
{
    ["充值"]      = BillCategory.Deposit,
    ["电费"]      = BillCategory.Electricity,
    ["浴室"]      = BillCategory.Bath,
    ["热水"]      = BillCategory.HotWater,
    ["面包"]      = BillCategory.Cake,
    ["食堂"]      = BillCategory.Canteen,
    ["图书馆"]    = BillCategory.Library,
    ["医院"]      = BillCategory.Hospital,
    ["超市"]      = BillCategory.Shop,
    ["洗衣"]      = BillCategory.Laundry,
    ["网费"]      = BillCategory.Network,
    ["公交"]      = BillCategory.Transport,
    // ...
};
```

**匹配算法**：对每个规则的关键字做**子串包含**检查（不区分大小写），命中即返回对应分类。

```
"第一食堂一楼"     → 包含 "食堂"   → Canteen
"教育超市便利店"   → 包含 "超市"   → Shop
"热水充值"         → 包含 "热水"   → HotWater
```

## API

```csharp
public class BillClassifier
{
    public BillCategory Classify(BillItemInfo item);
    public BillCategory Classify(string position);
    public void AddRule(string keyword, BillCategory category);
    public void RemoveRule(string keyword);
    public IReadOnlyDictionary<string, BillCategory> GetRules();
}
```

## 单条分类

```csharp
var classifier = new BillClassifier();
var item = new BillItemInfo { Position = "第一食堂一楼" };
var category = classifier.Classify(item);  // BillCategory.Canteen
Console.WriteLine(category.DisplayName()); // "食堂"
```

## 批量分类

```csharp
foreach (var item in bills)
{
    item.Category = classifier.Classify(item);
}
```

## 自定义规则

```csharp
// 添加新规则
classifier.AddRule("快递", BillCategory.Other);
classifier.AddRule("打印", BillCategory.Other);

// 删除错误规则
classifier.RemoveRule("图书馆");  // 不再归类为 Library

// 优先级：后加的规则先匹配
classifier.AddRule("便利店", BillCategory.Shop);
```

## 与持久化集成

把自定义规则保存到配置文件：

```toml
[billing.classifier]
rules = [
    { keyword = "快递", category = "Other" },
    { keyword = "打印", category = "Other" },
    { keyword = "便利店", category = "Shop" }
]
```

启动时加载：

```csharp
var classifier = new BillClassifier();
foreach (var rule in config.Billing.Classifier.Rules)
{
    classifier.AddRule(rule.Keyword, Enum.Parse<BillCategory>(rule.Category));
}
```

## 性能

- 单条分类 < 1μs（关键字匹配）
- 1000 条 < 1ms
- 缓存友好（Dictionary 查找）

## 局限性

关键字匹配无法处理：

- **同义词**（"餐"、"饮食"、"饭" → 都应是 Canteen）— 需自己加规则
- **缩写**（"一卡通" — 不会归到任何分类）
- **新商户**（校园新开的商户，规则表里没有）— 默认归到 `Other`

## 下一步

- [数据导出](/guide/export) — 分类后导出
- [账单同步 (BillSync)](/guide/bill-sync) — 在同步链路中使用
