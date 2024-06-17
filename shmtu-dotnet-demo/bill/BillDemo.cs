using shmtu.cas.auth;
using shmtu.parser.bill;

namespace shmtu.cas.demo.bill;

public static class BillDemo
{
    public static async Task TestBill(string userId, string password)
    {
        var billHtmlCode = "";
        // billHtmlCode = await GetBillFromNet(userId, password);

        if (string.IsNullOrEmpty(billHtmlCode))
        {
            // Read From File
            billHtmlCode = await File.ReadAllTextAsync("bill.html");
        }
        else
        {
            // Write to file
            await File.WriteAllTextAsync("bill.html", billHtmlCode);
        }
        
        var parser = new BillHtmlParser(billHtmlCode);
        parser.Parse();
    }

    private static async Task<string> GetBillFromNet(string userId, string password)
    {
        var epayAuth = new EpayAuth();
        var isSuccess =
            await epayAuth.Login(userId, password);
        Console.WriteLine(isSuccess);
        if (!isSuccess)
        {
            Console.WriteLine("Login failed!");
            return "";
        }

        var billResult =
            await epayAuth.GetBill(pageNo: "1");
        Console.WriteLine(billResult.Item1);
        Console.WriteLine(billResult.Item2);

        return billResult.Item2;
    }
}