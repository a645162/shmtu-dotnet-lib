using shmtu.cas.auth;
using shmtu.cas.demo.cas.auth;
using shmtu.datatype.bill;
using shmtu.parser.bill;

namespace shmtu.cas.demo.bill;

public static class BillDemo
{
    private const string HtmlFilePath = "bill.html";

    public static async Task TestBill(string userId, string password)
    {
        var billHtmlCode = "";
        billHtmlCode = await GetBillFromNet(userId, password);

        if (string.IsNullOrEmpty(billHtmlCode))
        {
            // Check if file exists
            if (!File.Exists(HtmlFilePath))
            {
                Console.WriteLine("Html File not found");
                return;
            }

            // Read From File
            billHtmlCode = await File.ReadAllTextAsync(HtmlFilePath);
        }
        else
        {
            // Write to file
            await File.WriteAllTextAsync(HtmlFilePath, billHtmlCode);
        }

        var parser = new BillHtmlParser(billHtmlCode);
        parser.Parse();

        var billItems = parser.BillItems;
        foreach (var billItem in billItems) Console.WriteLine(billItem.ToString());

        var jsonString =
            BillItemInfo.ListToJsonString(billItems);
        // Save Json
        await File.WriteAllTextAsync("bill.json", jsonString);

        Console.WriteLine();
    }

    private static async Task<string> GetBillFromNet(string userId, string password)
    {
        var epayAuth = new EpayAuthOverwrite();
        var isSuccess =
            await epayAuth.Login(userId, password);
        Console.WriteLine(isSuccess);
        if (!isSuccess)
        {
            Console.WriteLine("Login failed!");
            return "";
        }

        var billResult =
            await epayAuth.GetBill(EpayAuth.GetBillUrl(1));
        Console.WriteLine("Status Code:" + billResult.Item1);
        // Console.WriteLine(billResult.Item2);

        return billResult.Item2;
    }
}