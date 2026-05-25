using System.Text.Json;
using System.Text.Json.Serialization;

namespace shmtu.classifier;

/// <summary>
/// 位置翻译条目 — 对应 position.json 中每个关键词的映射值
/// 对齐 Rust 版本的 PositionEntry
/// </summary>
public class PositionEntry
{
    /// <summary>
    /// 楼栋名称（如"海馨楼"、"海琴楼"）
    /// </summary>
    [JsonPropertyName("position")]
    public string Position { get; set; } = "";

    /// <summary>
    /// 具体房间/窗口（如"海馨第1食堂"）
    /// </summary>
    [JsonPropertyName("room")]
    public string Room { get; set; } = "";
}

/// <summary>
/// 位置翻译器 — 将对方账户（target_user）翻译为楼栋和房间
/// 对齐 Rust 版本 shmtu-cas-rs 的 PositionTranslator
/// </summary>
public class PositionTranslator
{
    /// <summary>
    /// 匹配字段名（默认 "target"）
    /// </summary>
    [JsonPropertyName("field")]
    public string Field { get; set; } = "target";

    /// <summary>
    /// 关键词 → 位置映射字典
    /// </summary>
    [JsonPropertyName("keywords")]
    public Dictionary<string, PositionEntry> Keywords { get; set; } = [];

    /// <summary>
    /// 从 JSON 字符串加载翻译规则
    /// </summary>
    public static PositionTranslator FromJson(string json)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        return JsonSerializer.Deserialize<PositionTranslator>(json, options) ?? new PositionTranslator();
    }

    /// <summary>
    /// 翻译 target_user，返回 (position, room)
    /// 匹配逻辑（与 Rust 版本完全一致）：
    /// 1. 先精确匹配（整个 target_user）
    /// 2. 再模糊匹配（target_user 包含关键词）
    /// </summary>
    public (string position, string room)? Translate(string targetUser)
    {
        if (string.IsNullOrWhiteSpace(targetUser)) return null;

        var trimmed = targetUser.Trim();

        // 精确匹配
        if (Keywords.TryGetValue(trimmed, out var entry))
        {
            return (entry.Position, entry.Room);
        }

        // 模糊匹配：检查 target_user 是否包含某个关键词
        foreach (var (keyword, kwEntry) in Keywords)
        {
            if (trimmed.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return (kwEntry.Position, kwEntry.Room);
            }
        }

        return null;
    }

    /// <summary>
    /// 翻译，找不到匹配时返回 raw 值（position=targetUser, room=targetUser）
    /// 对齐 Rust 版本的 translate_or_raw
    /// </summary>
    public (string position, string room) TranslateOrRaw(string targetUser)
    {
        var result = Translate(targetUser);
        return result ?? (targetUser, targetUser);
    }
}
