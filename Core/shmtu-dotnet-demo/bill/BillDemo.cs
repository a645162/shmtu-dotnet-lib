using shmtu.cas.auth;
using shmtu.cas.captcha;
using shmtu.datatype.bill;
using shmtu.parser.bill;

namespace shmtu.cas.demo.bill;

public static class BillDemo
{
    public static async Task TestBill(string userId, string password, ICaptchaResolver captchaResolver)
    {
        var billHtmlCode = await GetBillFromNet(userId, password, captchaResolver);

        if (string.IsNullOrEmpty(billHtmlCode))
        {
            Console.WriteLine("无法获取账单 HTML，登录失败或网络异常。");
            return;
        }

        var parser = new BillHtmlParser(billHtmlCode);
        parser.Parse();

        var billItems = parser.BillItems;
        foreach (var billItem in billItems) Console.WriteLine(billItem.ToString());

        Console.WriteLine();
    }

    private static async Task<string> GetBillFromNet(string userId, string password, ICaptchaResolver captchaResolver)
    {
        var epayAuth = new EpayAuth(captchaResolver);
        var isSuccess = await epayAuth.Login(userId, password);
        Console.WriteLine(isSuccess);
        if (!isSuccess)
        {
            Console.WriteLine("Login failed!");
            return "";
        }

        var billResult = await epayAuth.GetBill(EpayAuth.GetBillUrl(1));
        Console.WriteLine("Status Code:" + billResult.Item1);

        return billResult.Item2;
    }
}
