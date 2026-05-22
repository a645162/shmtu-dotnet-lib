using shmtu.cas.auth;
using shmtu.cas.auth.common;
using shmtu.cas.captcha;
using shmtu.parser.bill;

namespace shmtu.cas.demo.bill;

public static class BillDemo
{
    public const int MaxLoginAttempts = 5;

    public static async Task TestBill(string userId, string password, ICaptchaResolver captchaResolver)
    {
        using var epayAuth = new EpayAuth(captchaResolver);

        var loggedIn = await LoginWithRetry(epayAuth, userId, password);
        if (!loggedIn)
        {
            Console.WriteLine("登录失败");
            return;
        }

        var billHtmlCode = await epayAuth.GetBillAsync(pageNo: 1);
        if (string.IsNullOrEmpty(billHtmlCode))
        {
            Console.WriteLine("无法获取账单 HTML。");
            return;
        }

        var parser = new BillHtmlParser(billHtmlCode);
        parser.Parse();

        foreach (var billItem in parser.BillItems)
            Console.WriteLine(billItem.ToString());

        Console.WriteLine();
    }

    /// <summary>
    /// 重试循环放在调用方（与 Rust CLI 对齐）。返回是否最终登录成功。
    /// </summary>
    private static async Task<bool> LoginWithRetry(
        EpayAuth epayAuth,
        string username,
        string password,
        int maxAttempts = MaxLoginAttempts,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine("正在探测登录状态...");
        var probe = await epayAuth.ProbeLoginAsync(cancellationToken);
        if (probe is LoginProbe.AlreadyLoggedIn)
        {
            Console.WriteLine("已经登录");
            return true;
        }

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            Console.WriteLine($"第{attempt}/{maxAttempts}次登录尝试");

            var result = await epayAuth.SubmitLoginAsync(username, password, cancellationToken);
            switch (result)
            {
                case LoginSubmitResult.Success:
                    if (await epayAuth.TestLoginStatusAsync(cancellationToken))
                    {
                        Console.WriteLine("登录验证成功！");
                        return true;
                    }
                    Console.WriteLine("登录验证失败");
                    return false;

                case LoginSubmitResult.ValidateCodeError:
                    Console.WriteLine("验证码错误，重试中...");
                    continue;

                case LoginSubmitResult.PasswordError:
                    Console.WriteLine("用户名或密码错误");
                    return false;

                case LoginSubmitResult.Failure failure:
                    Console.WriteLine($"登录失败: {failure.Message}");
                    return false;
            }
        }

        Console.WriteLine($"超过最大重试次数 {maxAttempts}");
        return false;
    }
}
