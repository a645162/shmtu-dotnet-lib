using shmtu.datatype.bill;

namespace shmtu.cas.demo.bill;

public static class BillItemDemo
{
    public static void TestBillItem()
    {
        var billItemInfo = new BillItemInfo(
            "2024.06.17", "123456",
            "交易",
            "1234567890",
            "对方",
            "100.00",
            "支付宝",
            "#succ"
        );
        Console.WriteLine();
    }
}