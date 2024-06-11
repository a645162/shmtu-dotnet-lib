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

    public (int, string, string) GetBill(string pageNo = "1", string tabNo = "1", string cookie = "")
    {
        var url =
            "https://ecard.shmtu.edu.cn/epay/consume/query" +
            "?pageNo=" + pageNo +
            "&tabNo=" + tabNo;

        var finalCookie = string.IsNullOrEmpty(cookie) ? this._savedCookie : cookie;

        try
        {
            using (var response = url.WithCookie("Cookie", finalCookie).AllowAnyHttpStatus().GetAsync().Result)
            {
                int responseCode = (int)response.StatusCode;

                if (responseCode == 200)
                {
                    this._htmlCode = (response.ResponseMessage.Content.ReadAsStringAsync().Result ?? "").Trim();
                    return (responseCode, this._htmlCode, cookie);
                }

                if (responseCode == 302)
                {
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

                    this._savedCookie = newCookie;

                    return (responseCode, location, newCookie);
                }

                return (responseCode, "", "");
            }
        }
        catch (Exception ex)
        {
            // Handle exception
            return (0, ex.Message, "");
        }
    }

    public bool TestLoginStatus()
    {
        var resultBill = GetBill(cookie: this._savedCookie);

        if (resultBill.Item1 == 200)
        {
            // OK
            return true;
        }
        else if (resultBill.Item1 == 302)
        {
            this._loginUrl = resultBill.Item2;
            this._savedCookie = resultBill.Item3;
            return false;
        }
        else
        {
            return false;
        }
    }

    public async Task<bool> Login(string username, string password)
    {
        if (string.IsNullOrEmpty(this._loginUrl) || string.IsNullOrEmpty(this._savedCookie))
        {
            if (TestLoginStatus())
            {
                return true;
            }
        }

        string executionStr = await CasAuth.GetExecution(this._loginUrl, this._savedCookie);

        // Download captcha
        var resultCaptcha =
            await Captcha.GetImageDataFromUrlUsingGet(cookie: this._savedCookie);

        if (resultCaptcha.Item1 == null)
        {
            Console.WriteLine("获取验证码图片失败");
            return false;
        }

        byte[] imageData = resultCaptcha.Item1;
        this._loginCookie = resultCaptcha.Item2;

        // Call remote recognition interface
        string validateCode = Captcha.OcrByRemoteTcpServer("127.0.0.1", 21601, imageData);
        string exprResult = Captcha.GetExprResultByExprString(validateCode);

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

        this._loginCookie = resultCas.Item3;

        var resultRedirect =
            await CasAuth.CasRedirect(resultCas.Item2, this._savedCookie);

        if (resultRedirect.Item1 != 302)
        {
            Console.WriteLine("Login Ok,but cannot redirect to bill page.");
            Console.WriteLine($"Status code：{resultRedirect.Item1}");
            return false;
        }

        (int, string, string) resultBill = GetBill(cookie: this._savedCookie);

        return resultBill.Item1 == 200;
    }
}