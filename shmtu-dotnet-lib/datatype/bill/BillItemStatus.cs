using System.ComponentModel;
using System.Reflection;

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

    public static TEnum FromDescription<TEnum>(string description) where TEnum : struct, Enum
    {
        foreach (var field in typeof(TEnum).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            var descriptionAttributes = (DescriptionAttribute[])field.GetCustomAttributes(
                typeof(DescriptionAttribute),
                false
            );
            if (descriptionAttributes.Length > 0 && descriptionAttributes[0].Description == description)
            {
                return (TEnum)field.GetValue(null);
            }
        }

        throw new ArgumentException(
            $"No matching enum value found for description '{description}'.",
            nameof(description)
            );
    }
}