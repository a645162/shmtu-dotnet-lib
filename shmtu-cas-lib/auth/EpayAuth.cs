using System.Net;
using shmtu.cas.auth.common;
using shmtu.cas.captcha;

namespace shmtu.cas.auth;

using System;
using Flurl.Http;

public class EpayAuth
{
    private string _savedCookie = "";
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
        var url =
            "https://ecard.shmtu.edu.cn/epay/consume/query" +
            "?pageNo=" + pageNo +
            "&tabNo=" + tabNo;

        var finalCookie = string.IsNullOrEmpty(cookie) ? _savedCookie : cookie;

        try
        {
            var response = await url
                .WithCookie("Cookie", finalCookie)
                .WithAutoRedirect(false)
                .AllowHttpStatus([302])
                .GetAsync();

            var responseCodeInt = response.StatusCode;
            var responseCode = (HttpStatusCode)responseCodeInt;

            if (responseCode == HttpStatusCode.OK)
            {
                _htmlCode = (response.ResponseMessage.Content.ReadAsStringAsync().Result ?? "").Trim();
                return (responseCodeInt, _htmlCode, cookie);
            }

            if (responseCode == HttpStatusCode.Redirect)
            {
                if (response.ResponseMessage.Headers.Location == null)
                {
                    Console.WriteLine("Location is null");
                    return (responseCodeInt, "", "");
                }

                var location = response.ResponseMessage.Headers.Location.ToString();

                // Get all "Set-Cookie" Header
                var setCookieHeaders =
                    response.ResponseMessage.Headers.GetValues("Set-Cookie").ToList();

                var newCookie = cookie;
                foreach (
                    var currentSetCookie in setCookieHeaders
                        .Where(currentSetCookie => currentSetCookie.Contains("JSESSIONID"))
                )
                {
                    newCookie = currentSetCookie;
                    break;
                }

                _savedCookie = newCookie;

                return (responseCodeInt, location, newCookie);
            }

            return (responseCodeInt, "", "");
        }
        catch (Exception ex)
        {
            // Handle exception
            return (0, ex.Message, "");
        }
    }

    public async Task<bool> TestLoginStatus()
    {
        var resultBill =
            await GetBill(cookie: this._savedCookie);

        switch (resultBill.Item1)
        {
            case 200:
                // OK
                return true;
            case 302:
                _loginUrl = resultBill.Item2;
                _savedCookie = resultBill.Item3;
                return false;
            default:
                return false;
        }
    }

    public async Task<bool> Login(string username, string password)
    {
        if (string.IsNullOrEmpty(_loginUrl) || string.IsNullOrEmpty(_savedCookie))
        {
            if (await TestLoginStatus())
            {
                return true;
            }
        }

        var executionStr = await CasAuth.GetExecution(_loginUrl, _savedCookie);

        // Download captcha
        var (imageData, item2) =
            await Captcha.GetImageDataFromUrlUsingGet(cookie: this._savedCookie);

        if (imageData == null)
        {
            Console.WriteLine("获取验证码图片失败");
            return false;
        }

        _loginCookie = item2;

        // Call remote recognition interface
        var validateCode = Captcha.OcrByRemoteTcpServer("127.0.0.1", 21601, imageData);
        Captcha.SaveImageToFile(imageData, ".");
        Console.WriteLine(validateCode);
        var exprResult = Captcha.GetExprResultByExprString(validateCode);

        var resultCas =
            await CasAuth.CasLogin(
                this._loginUrl,
                username, password,
                exprResult,
                executionStr,
                this._loginCookie
            );

        if (resultCas.Item1 != 302)
        {
            Console.WriteLine($"程序出错，状态码：{resultCas.Item1}");
            return false;
        }

        _loginCookie = resultCas.Item3;

        var resultRedirect =
            await CasAuth.CasRedirect(resultCas.Item2, this._savedCookie);

        if (resultRedirect.Item1 != 302)
        {
            Console.WriteLine("Login Ok,but cannot redirect to bill page.");
            Console.WriteLine($"Status code：{resultRedirect.Item1}");
            return false;
        }

        var resultBill =
            await GetBill(cookie: this._savedCookie);

        return resultBill.Item1 == 200;
    }
}