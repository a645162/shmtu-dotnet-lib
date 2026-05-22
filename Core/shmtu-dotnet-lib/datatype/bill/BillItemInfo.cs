using System.Text.Json;
using System.Text.Json.Serialization;
using shmtu.utils;

namespace shmtu.datatype.bill;

public class BillItemInfo : IEquatable<BillItemInfo>, IComparable<BillItemInfo>
{
    private string _dateStrFormated = "";

    private string _dateString = "";
    private string _timeStrFormat = "";
    private string _timeString = "";

    // (交易)名称
    [JsonInclude] public string ItemType = "";

    // 付款方式
    [JsonInclude] public string Method = "";

    // 金额
    [JsonInclude] public string MoneyString = "";

    // 交易号（单条有值，合并为空）
    [JsonInclude] public string Number = "";

    // 状态
    [JsonInclude] public string StatusString = "";

    // 对方
    [JsonInclude] public string TargetUser = "";

    // === 合并相关字段 ===

    // 结束时间字符串（单条 = 开始时间，合并 = 最晚时间）
    private string _endDateTimeStringFormatted = "";

    // 结束时间戳
    private long _endTimeStamp;

    // 所有交易号（单条 = [Number]，合并 = 多个，按时间升序）
    private List<string> _numberList = [];

    // 是否为合并条目
    private readonly bool _isCombined;

    /// 创建单条账单。
    public BillItemInfo(
        string dateString, string timeString,
        string itemType,
        string number,
        string targetUser,
        string moneyString,
        string method,
        string statusString
    )
    {
        // 创建时间
        DateString = dateString;
        TimeString = timeString;

        // 名称
        ItemType = itemType;
        // 交易号
        Number = number;

        // 对方
        TargetUser = targetUser;

        // 金额
        MoneyString = moneyString;

        // 付款方式
        Method = method;

        // 状态
        StatusString = statusString;

        // 单条：结束 = 开始
        _endDateTimeStringFormatted = DateTimeStringFormated;
        _endTimeStamp = TimeStamp;
        _numberList = [number];
        _isCombined = false;
    }

    /// 私有构造：仅由 Merge 使用。
    private BillItemInfo(bool isCombined)
    {
        _isCombined = isCombined;
    }

    // === 合并 ===

    /// <summary>
    /// 合并多条账单（单条/混合/合并条目均可）。按时间升序排列，金额求和。
    /// 1 条直接返回原样；0 条抛异常。
    /// </summary>
    public static BillItemInfo Merge(List<BillItemInfo> items)
    {
        if (items.Count == 0)
            throw new ArgumentException("不能合并空的账单列表");
        if (items.Count == 1)
            return items[0];

        var sorted = items.OrderBy(b => b.TimeStamp).ToList();
        var first = sorted[0];
        var last = sorted[^1];

        var totalMoney = sorted.Sum(b => b.Money);

        var allNumbers = new List<string>();
        foreach (var item in sorted)
            allNumbers.AddRange(item.NumberList);

        var merged = new BillItemInfo(isCombined: true)
        {
            _dateString = first._dateString,
            _timeString = first._timeString,
            _dateStrFormated = first._dateStrFormated,
            _timeStrFormat = first._timeStrFormat,
            ItemType = first.ItemType,
            Number = "",
            TargetUser = first.TargetUser,
            MoneyString = totalMoney.ToString("F2"),
            Method = first.Method,
            StatusString = first.StatusString,
            _endDateTimeStringFormatted = last.EndDateTimeStringFormatted,
            _endTimeStamp = last.EndTimeStamp,
            _numberList = allNumbers,
        };

        return merged;
    }

    /// 与另一条合并的快捷方法。
    public BillItemInfo MergeWith(BillItemInfo other) => Merge([this, other]);

    // === 时间属性 ===

    // Example:2024.06.17
    [JsonInclude]
    public string DateString
    {
        get => _dateString;
        set
        {
            value = value.Trim();

            if (value.Length != 10)
                throw new ArgumentException("DateStr must be 10 characters long");

            _dateString = value;

            // Format date string
            _dateStrFormated = value;
            _dateStrFormated =
                _dateStrFormated
                    .Replace("_", "-")
                    .Replace(".", "-");
        }
    }

    // Example:123456
    [JsonInclude]
    public string TimeString
    {
        get => _timeString;
        set
        {
            value = value.Trim();

            if (value.Length != 6 && value.Length != 8)
                throw new ArgumentException("TimeStr must be 6 characters long");

            _timeString = value;

            // Format time string
            _timeStrFormat =
                value
                    .Replace("-", ":")
                    .Replace("_", ":")
                    .Replace(".", ":");

            if (value.Length == 8)
                return;

            _timeStrFormat = _timeStrFormat.Insert(2, ":").Insert(5, ":");
        }
    }

    [JsonIgnore]
    public string DateTimeStringFormated
    {
        get
        {
            if (_dateStrFormated.Length > 0 && _timeStrFormat.Length > 0)
                return $"{_dateStrFormated} {_timeStrFormat}";
            return "";
        }
    }

    /// 结束时间字符串（单条 = 开始时间，合并 = 最晚时间）。
    [JsonIgnore]
    public string EndDateTimeStringFormatted => _endDateTimeStringFormatted;

    [JsonIgnore]
    public DateTime DatetimeObject
    {
        get => DateTime.Parse(DateTimeStringFormated);
        set
        {
            DateString = value.ToString("yyyy-MM-dd");
            TimeString = value.ToString("HH:mm:ss");
        }
    }

    [JsonIgnore]
    public long TimeStamp
    {
        get => ((DateTimeOffset)DatetimeObject).ToUnixTimeSeconds();
        set => DatetimeObject = DateTimeOffset.FromUnixTimeSeconds(value).DateTime;
    }

    /// 结束时间戳（单条 = TimeStamp，合并 = 最晚条的 TimeStamp）。
    [JsonIgnore]
    public long EndTimeStamp => _endTimeStamp;

    [JsonIgnore]
    public float Money
    {
        get
        {
            if (float.TryParse(MoneyString, out var money))
                return money;
            return 0;
        }
        set =>
            // 格式化输出为只有两位小数的字符串
            MoneyString = value.ToString("F2");
    }

    [JsonIgnore]
    public BillItemStatus Status
    {
        get
        {
            if (StatusString.Trim() == BillItemStatus.All.GetDescription()) return BillItemStatus.All;

            if (StatusString.Trim() == BillItemStatus.WaitFor.GetDescription()) return BillItemStatus.WaitFor;

            if (StatusString.Trim() == BillItemStatus.Success.GetDescription()) return BillItemStatus.Success;

            if (StatusString.Trim() == BillItemStatus.Failure.GetDescription()) return BillItemStatus.Failure;

            return BillItemStatus.All;
        }
        set
        {
            StatusString = value switch
            {
                BillItemStatus.All => BillItemStatus.All.GetDescription(),
                BillItemStatus.WaitFor => BillItemStatus.WaitFor.GetDescription(),
                BillItemStatus.Success => BillItemStatus.Success.GetDescription(),
                BillItemStatus.Failure => BillItemStatus.Failure.GetDescription(),
                _ => StatusString
            };
        }
    }

    /// 所有交易号（单条 = [Number]，合并 = 多个，按时间升序）。只读。
    [JsonIgnore]
    public List<string> NumberList => _numberList;

    /// 是否为合并条目。只读。
    [JsonIgnore]
    public bool IsCombined => _isCombined;

    // === 相等比较（基于 NumberList） ===

    public bool Equals(BillItemInfo? other)
    {
        if (other is null) return false;
        return NumberList.SequenceEqual(other.NumberList);
    }

    public override bool Equals(object? obj) => Equals(obj as BillItemInfo);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var n in NumberList) hash.Add(n);
        return hash.ToHashCode();
    }

    public static bool operator ==(BillItemInfo? left, BillItemInfo? right)
        => Equals(left, right);

    public static bool operator !=(BillItemInfo? left, BillItemInfo? right)
        => !Equals(left, right);

    // === 排序（基于时间戳） ===

    public int CompareTo(BillItemInfo? other)
    {
        if (other is null) return 1;
        return TimeStamp.CompareTo(other.TimeStamp);
    }

    /// 按金额排序比较。
    public static int CompareByMoney(BillItemInfo a, BillItemInfo b)
        => a.Money.CompareTo(b.Money);

    /// 对一组账单求金额总和。
    public static float SumMoney(IEnumerable<BillItemInfo> items)
        => items.Sum(b => b.Money);

    // === ToString ===

    public override string ToString()
    {
        if (IsCombined)
            return $"{DateTimeStringFormated} - {EndDateTimeStringFormatted} | {ItemType} | {Money:F2} | [{NumberList.Count}条合并]";

        return $"{DateTimeStringFormated} {ItemType} {Number} {TargetUser} {MoneyString} {Method} {StatusString}";
    }

    public string ToJsonString()
    {
        return JsonSerializer.Serialize(
            this,
            JsonUtils.ProgramJsonSerializerOptions
        );
    }

    public static BillItemInfo? FromJsonString(string jsonString)
    {
        return JsonSerializer.Deserialize<BillItemInfo>(jsonString);
    }

    public static string ListToJsonString(List<BillItemInfo> billItemInfoList)
    {
        return JsonSerializer.Serialize(
            billItemInfoList,
            JsonUtils.ProgramJsonSerializerOptions
        );
    }

    public static List<BillItemInfo>? ListFromJsonString(string jsonString)
    {
        return JsonSerializer.Deserialize<List<BillItemInfo>>(jsonString);
    }
}
