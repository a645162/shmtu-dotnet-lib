using System.Text.Json;
using System.Text.Json.Serialization;
using shmtu.utils;

namespace shmtu.datatype.bill;

public class BillItemInfo
{
    private string _dateStrFormated = "";

    private string _dateString = "";
    private string _timeStrFormat = "";
    private string _timeString = "";

    // (交易)名称
    [JsonInclude] public string ItemType;

    // 付款方式
    [JsonInclude] public string Method;

    // 金额
    [JsonInclude] public string MoneyString;

    // 交易号
    [JsonInclude] public string Number;

    // 状态
    [JsonInclude] public string StatusString;

    // 对方
    [JsonInclude] public string TargetUser;

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
    }

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

    public override string ToString()
    {
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