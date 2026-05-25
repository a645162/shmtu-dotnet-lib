using System.Text.Json;
using System.Text.Json.Serialization;

namespace shmtu.classifier;

/// <summary>
/// 分类规则 — 对应 type.json 中每个分类的匹配关键词
/// 对齐 Rust 版本的 CategoryRule
/// </summary>
public class CategoryRule
{
    /// <summary>
    /// 按交易名称 (name/item_type) 匹配的关键词列表
    /// </summary>
    [JsonPropertyName("name")]
    public List<string> Name { get; set; } = [];

    /// <summary>
    /// 按对方账户 (target) 匹配的关键词列表
    /// </summary>
    [JsonPropertyName("target")]
    public List<string> Target { get; set; } = [];
}

/// <summary>
/// 账单分类器 — 从 type.json 加载规则，根据 name 和 target 字段分类
/// 对齐 Rust 版本 shmtu-cas-rs 的 BillClassifier
/// </summary>
public class BillClassifier
{
    /// <summary>
    /// 分类规则字典，Key 为分类标识（如 "deposit"、"electricity"）
    /// </summary>
    public Dictionary<string, CategoryRule> Categories { get; set; } = [];

    /// <summary>
    /// 从 JSON 字符串加载分类规则
    /// </summary>
    public static BillClassifier FromJson(string json)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var categories = JsonSerializer.Deserialize<Dictionary<string, CategoryRule>>(json, options);
        return new BillClassifier { Categories = categories ?? [] };
    }

    /// <summary>
    /// 根据交易名称和对方账户进行分类
    /// 匹配逻辑（与 Rust 版本完全一致）：
    /// 1. 遍历所有分类规则
    /// 2. 在每个分类中，先按 name 字段匹配（contains）
    /// 3. 再按 target 字段匹配（contains）
    /// 4. 首次匹配即返回，未匹配返回 Other
    /// </summary>
    public BillCategory Classify(string name, string target)
    {
        foreach (var (catName, rule) in Categories)
        {
            // 按 name 字段匹配
            foreach (var kw in rule.Name)
            {
                if (name.Contains(kw, StringComparison.OrdinalIgnoreCase))
                {
                    return ParseCategory(catName);
                }
            }

            // 按 target 字段匹配
            foreach (var kw in rule.Target)
            {
                if (target.Contains(kw, StringComparison.OrdinalIgnoreCase))
                {
                    return ParseCategory(catName);
                }
            }
        }

        return BillCategory.Other;
    }

    /// <summary>
    /// 将分类标识字符串解析为 BillCategory 枚举
    /// </summary>
    private static BillCategory ParseCategory(string s) => s.ToLowerInvariant() switch
    {
        "deposit" => BillCategory.Deposit,
        "electricity" => BillCategory.Electricity,
        "bath" => BillCategory.Bath,
        "hot_water" => BillCategory.HotWater,
        "cake" => BillCategory.Cake,
        "canteen" => BillCategory.Canteen,
        "library" => BillCategory.Library,
        "hospital" => BillCategory.Hospital,
        "shop" => BillCategory.Shop,
        "laundry" => BillCategory.Laundry,
        "network" => BillCategory.Network,
        "transport" => BillCategory.Transport,
        _ => BillCategory.Other
    };
}
