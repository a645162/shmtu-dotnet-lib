using System.ComponentModel;

namespace shmtu.datatype.bill;

public enum BillItemStatus
{
    [Description("#all")] All,
    [Description("#waitfor")] WaitFor,
    [Description("交易成功")] Success,
    [Description("#fail")] Failure
}

public static class EnumExtensions
{
    public static string GetDescription(this Enum value)
    {
        var fieldInfo = value.GetType().GetField(value.ToString());
        if (fieldInfo == null)
        {
            return value.ToString();
        }

        var descriptionAttributes =
            (DescriptionAttribute[])fieldInfo.GetCustomAttributes(
                typeof(DescriptionAttribute),
                false
            );
        return descriptionAttributes.Length > 0 ? descriptionAttributes[0].Description : value.ToString();
    }
}