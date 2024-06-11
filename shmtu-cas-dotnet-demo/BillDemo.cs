using shmtu.cas.auth;

namespace shmtu.cas.demo;

public static class BillDemo
{
    public static async Task
        TestBill(
            string userId, string password
        )
    {
        var epayAuth = new EpayAuth();
        var isSuccess =
            await epayAuth.Login(userId, password);
        Console.WriteLine(isSuccess);
        if (!isSuccess)
        {
            Console.WriteLine("Login failed!");
            return;
        }

        var billResult =
            await epayAuth.GetBill(pageNo: "1");
        Console.WriteLine(billResult.Item1);
        Console.WriteLine(billResult.Item2);
    }
}