using System.Net;
using shmtu.cas.auth.common;
using shmtu.cas.captcha;

namespace shmtu.cas.auth;

using System;
using Flurl.Http;

public class EpayAuth
{
    private string _epayCookie = "";
    private string _htmlCode = "";

    private string _loginUrl = "";
    private string _loginCookie = "";

    public async Task<(int, string, string)>
        GetBill(
            string pageNo = "1",
            string tabNo = "1",
            string cookie = ""
        )
    {
        // https://ecard.shmtu.edu.cn/epay/consume/query?pageNo=1&tabNo=1
        var url =
            $"https://ecard.shmtu.edu.cn/epay/consume/query?pageNo={pageNo}&tabNo={tabNo}";

        var finalCookie =
            string.IsNullOrEmpty(cookie) ? _epayCookie : cookie;

        try
        {
            var request = url
                .WithHeader("Cookie", finalCookie)
                .WithAutoRedirect(false)
                .AllowHttpStatus([302]);
            var response = await request.GetAsync();

            var responseCodeInt = response.StatusCode;
            var responseCode = (HttpStatusCode)responseCodeInt;

            if (responseCode == HttpStatusCode.OK)
            {
                _htmlCode =
                    response.ResponseMessage.Content.ReadAsStringAsync().Result.Trim();
                if (_htmlCode.Length > 0)
                {
                    _htmlCode += "\n";
                }

                return (CasAuthStatus.Success.ToInt(), _htmlCode, cookie);
            }

            if (responseCode == HttpStatusCode.Redirect)
            {
                if (response.ResponseMessage.Headers.Location == null)
                {
                    Console.WriteLine("Location is null");
                    return (CasAuthStatus.UnrecoverableError.ToInt(), "", "");
                }

                var location = response.ResponseMessage.Headers.Location.ToString();

                // Get all "Set-Cookie" Header
                var setCookieHeaders =
                    response.ResponseMessage.Headers.GetValues("Set-Cookie").ToList();

                var newCookie = cookie;
                foreach (
                    var currentSetCookie in setCookieHeaders
                        .Where(
                            currentSetCookie =>
                                currentSetCookie.Contains("JSESSIONID")
                        )
                )
                {
                    newCookie = currentSetCookie.Trim();
                    break;
                }

                _epayCookie = newCookie;

                return (CasAuthStatus.Redirect.ToInt(), location, newCookie);
            }

            return (responseCodeInt, "", "");
        }
        catch (Exception ex)
        {
            // Handle exception
            return (CasAuthStatus.UnrecoverableError.ToInt(), ex.Message, "");
        }
    }

    public async Task<bool> TestLoginStatus()
    {
        var resultBill =
            await GetBill(cookie: _epayCookie);

        switch (resultBill.Item1)
        {
            case 200:
                // OK
                return true;
            case 302:
                _loginUrl = resultBill.Item2;
                var newCookie = resultBill.Item3;

                if (newCookie.Length <= 0) return false;

                // Remove unused cookie
                // var spiltList = newCookie.Split(";");
                // foreach (var item in spiltList)
                // {
                //     if (!item.Contains("JSESSIONID")) continue;
                //     newCookie = item;
                //     break;
                // }

                _epayCookie = newCookie;

                return false;
            default:
                return false;
        }
    }

    public async Task<bool> Login(string username, string password)
    {
        if (string.IsNullOrEmpty(_loginUrl) || string.IsNullOrEmpty(_epayCookie))
        {
            if (await TestLoginStatus())
            {
                return true;
            }
        }

        var executionString =
            await CasAuth.GetExecutionString(_loginUrl, _epayCookie);

        // Download captcha
        var (imageData, loginCookie) =
            await Captcha.GetImageDataFromUrlUsingGet(
                cookie: _loginCookie,
                userAgent: CasAuth.UserAgent
            );

        if (imageData == null)
        {
            Console.WriteLine("获取验证码图片失败");
            return false;
        }

        // Got cas login cookie
        _loginCookie = loginCookie;

        // Call remote recognition interface
        var validateCodeResult =
            Captcha.OcrByRemoteTcpServer("127.0.0.1", 21601, imageData);
        Captcha.SaveImageToFile(imageData);
        Console.WriteLine(validateCodeResult);
        var exprResult =
            Captcha.GetExprResultByExprString(validateCodeResult);

        var resultCas =
            await CasAuth.CasLogin(
                _loginUrl,
                username, password,
                exprResult,
                executionString,
                _loginCookie
            );

        if (resultCas.Item1 != CasAuthStatus.Redirect.ToInt())
        {
            Console.WriteLine($"程序出错，状态码：{resultCas.Item1}");
            return false;
        }

        _loginCookie = resultCas.Item3;

        var redirectCookie = _epayCookie + ";" + _loginCookie;
        var resultRedirect =
            await CasAuth.CasRedirect(resultCas.Item2, redirectCookie);

        if (resultRedirect.Item1 != CasAuthStatus.Redirect.ToInt())
        {
            Console.WriteLine("Login Ok,but cannot redirect to bill page.");
            Console.WriteLine($"Status code：{resultRedirect.Item1}");
            return false;
        }

        // var finalTestStatus = await TestLoginStatus();

        var resultBill =
            await GetBill(cookie: _epayCookie);

        return resultBill.Item1 == CasAuthStatus.Success.ToInt();
    }
}