namespace shmtu.datatype.bill;

using System;

public class BillItemInfo
{
    // 交易(创建)时间
    public BillItemInfo(
        string dateStr, string timeStr,
        string itemType,
        string number,
        string targetUser,
        string moneyStr,
        string method,
        string statusString
    )
    {
        DateStr = dateStr;
        TimeStr = timeStr;

        ItemType = itemType;
        Number = number;
        TargetUser = targetUser;
        MoneyStr = moneyStr;
        Method = method;
        StatusString = statusString;
    }

    private string _dateStr = "";
    private string _timeStr = "";

    // 2024.06.17
    public string DateStr
    {
        get => _dateStr;
        set
        {
            if (value.Length != 10)
                throw new ArgumentException("DateStr must be 10 characters long");

            _dateStr = value;

            // Format date string
            _dateStrFormated = value;
            _dateStrFormated = _dateStrFormated.Replace(".", "-");
        }
    }

    // 123456
    public string TimeStr
    {
        get => _timeStr;
        set
        {
            if (value.Length != 6)
                throw new ArgumentException("TimeStr must be 6 characters long");

            _timeStr = value;

            // Format time string
            _timeStrFormat = value;
            _timeStrFormat = _timeStrFormat.Insert(2, ":").Insert(5, ":");
        }
    }

    private string _dateStrFormated = "";
    private string _timeStrFormat = "";

    public string DateTimeStringFormated
    {
        get
        {
            if (_dateStrFormated.Length > 0 && _timeStrFormat.Length > 0)
                return $"{_dateStrFormated} {_timeStrFormat}";
            return "";
        }
    }

    public DateTime DatetimeObject
    {
        get => DateTime.Parse(DateTimeStringFormated);
        set
        {
            _dateStrFormated = value.ToString("yyyy-MM-dd");
            _timeStrFormat = value.ToString("HH:mm:ss");
        }
    }

    public long TimeStamp
    {
        get => ((DateTimeOffset)DatetimeObject).ToUnixTimeSeconds();
        set => DatetimeObject = DateTimeOffset.FromUnixTimeSeconds(value).DateTime;
    }

    // (交易)名称
    public string ItemType;

    // 交易号
    public string Number;

    // 对方
    public string TargetUser;

    // 金额
    public string MoneyStr;

    public float Money
    {
        get
        {
            if (float.TryParse(MoneyStr, out var money))
                return money;
            return 0;
        }
        set =>
            // 格式化输出为只有两位小数的字符串
            MoneyStr = value.ToString("F2");
    }

    // 付款方式
    public string Method;

    // 状态
    public string StatusString;

    public BillItemStatus Status
    {
        get
        {
            if (StatusString.Trim() == BillItemStatus.All.GetDescription())
            {
                return BillItemStatus.All;
            }

            if (StatusString.Trim() == BillItemStatus.WaitFor.GetDescription())
            {
                return BillItemStatus.WaitFor;
            }

            if (StatusString.Trim() == BillItemStatus.Success.GetDescription())
            {
                return BillItemStatus.Success;
            }

            if (StatusString.Trim() == BillItemStatus.Failure.GetDescription())
            {
                return BillItemStatus.Failure;
            }

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
        return $"{DateTimeStringFormated} {ItemType} {Number} {TargetUser} {MoneyStr} {Method} {StatusString}";
    }
}