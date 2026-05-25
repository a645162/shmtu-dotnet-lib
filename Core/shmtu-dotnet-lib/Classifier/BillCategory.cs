namespace shmtu.classifier;

/// <summary>
/// 账单分类类型 — 对齐 Rust 版本 shmtu-cas-rs 的 BillCategory 枚举
/// </summary>
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

/// <summary>
/// BillCategory 的扩展方法 — 提供中文显示名
/// </summary>
public static class BillCategoryExtensions
{
    /// <summary>
    /// 返回中文显示名，对齐 Rust 版本的 display_name()
    /// </summary>
    public static string DisplayName(this BillCategory category) => category switch
    {
        BillCategory.Deposit => "充值",
        BillCategory.Electricity => "电费",
        BillCategory.Bath => "洗澡",
        BillCategory.HotWater => "热水",
        BillCategory.Cake => "点心",
        BillCategory.Canteen => "食堂",
        BillCategory.Library => "图书馆",
        BillCategory.Hospital => "校医院",
        BillCategory.Shop => "超市",
        BillCategory.Laundry => "洗衣",
        BillCategory.Network => "网络",
        BillCategory.Transport => "交通",
        BillCategory.Other => "其他",
        _ => "其他"
    };
}
