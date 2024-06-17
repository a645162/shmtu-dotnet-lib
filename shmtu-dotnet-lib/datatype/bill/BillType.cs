using System.ComponentModel;

namespace shmtu.datatype.bill;

public enum BillType
{
    [Description("全部")] All,
    [Description("未付款")] NotPaid,
    [Description("成功")] Success,
    [Description("失败")] Failure
}