using System.Net;
using Flurl.Http;
using HtmlAgilityPack;

namespace shmtu.cas.auth.common;

public static class CasAuth
{
    public const string UserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
        "AppleWebKit/537.36 (KHTML, like Gecko) " +
        "Chrome/126.0.0.0 Safari/537.36 Edg/126.0.0.0";

    public static async Task<string> GetExecutionString(
        string url = "https://cas.shmtu.edu.cn/cas/login",
        string cookie = ""
    )
    {
        try
        {
            var request = url
                .WithHeader("Cookie", cookie)
                .WithAutoRedirect(false)
                .AllowHttpStatus([302]);
            var response =
                await request.SendAsync(HttpMethod.Get);

            var responseCode = (HttpStatusCode)response.StatusCode;

            if (responseCode == HttpStatusCode.OK)
            {
                var htmlCode =
                    await response.ResponseMessage.Content.ReadAsStringAsync();

                var document = new HtmlDocument();
                document.LoadHtml(htmlCode);
                var element =
                    document.DocumentNode.SelectSingleNode("//input[@name='execution']");
                var value =
                    element?.GetAttributeValue("value", "") ?? "";
                return value.Trim();
            }

            Console.WriteLine($"Get execution string error:{response.StatusCode}");
            return "";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Get execution string error:{ex.Message}");
            return "";
        }
    }

    public static async
        Task<(int, string, string)>
        CasLogin(
            string url,
            string username, string password,
            string validateCode,
            string execution,
            string cookie
        )
    {
        try
        {
            var body = new
            {
                username = username.Trim(),
                password = password.Trim(),
                validateCode = validateCode.Trim(),
                execution = execution.Trim(),
                _eventId = "submit",
                geolocation = ""
            };

            var response = await url
                .WithAutoRedirect(false)
                .AllowHttpStatus([302])
                .WithHeader("Host", "cas.shmtu.edu.cn")
                .WithHeader(
                    "Content-Type",
                    "application/x-www-form-urlencoded"
                )
                .WithHeader("Connection", "keep-alive")
                .WithHeader("Accept-Encoding", "gzip, deflate, br")
                .WithHeader("Accept", "*/*")
                .WithHeader("User-Agent", UserAgent)
                .WithHeader("Cookie", cookie.Trim())
                .PostUrlEncodedAsync(body);

            var responseCodeInt = response.StatusCode;
            var responseCode = (HttpStatusCode)responseCodeInt;

            if (responseCode == HttpStatusCode.Redirect)
            {
                var location =
                    response.ResponseMessage
                        .Headers
                        .GetValues("Location").FirstOrDefault() ?? "";
                var newCookie =
                    response.ResponseMessage
                        .Headers
                        .GetValues("Set-Cookie").FirstOrDefault() ?? "";

                return (response.StatusCode, location, newCookie);
            }

            var htmlCode =
                await response.ResponseMessage.Content.ReadAsStringAsync();
            var document = new HtmlDocument();
            document.LoadHtml(htmlCode);
            var element =
                document.DocumentNode.SelectSingleNode("//*[@id=\"loginErrorsPanel\"]");
            var errorText = (element?.InnerText ?? "").Trim();
            Console.WriteLine($"登录失败，错误信息：{errorText}");
            if (errorText.Contains("account is not recognized"))
            {
                Console.WriteLine("用户名或密码错误");
                return (CasAuthStatus.PasswordError.ToInt(), htmlCode, "");
            }

            if (errorText.Contains("reCAPTCHA"))
            {
                Console.WriteLine("验证码错误");
                return (CasAuthStatus.ValidateCodeError.ToInt(), htmlCode, "");
            }

            return (response.StatusCode, htmlCode, errorText);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Core Login Exception: {ex.Message}");
            return (0, "", "");
        }
    }

    public static async
        Task<(int, string, string)>
        CasRedirect(string url, string cookie)
    {
        try
        {
            var request = url
                .WithAutoRedirect(false)
                .AllowHttpStatus([302])
                .WithHeader("User-Agent", UserAgent)
                .WithHeader("Cookie", cookie);
            var response = await request.SendAsync(HttpMethod.Get);

            var responseCodeInt = response.StatusCode;
            var responseCode = (HttpStatusCode)responseCodeInt;

            if (responseCode == HttpStatusCode.Redirect)
            {
                var location =
                    response.ResponseMessage
                        .Headers
                        .GetValues("Location").FirstOrDefault() ?? "";
                var newCookie =
                    response.ResponseMessage
                        .Headers
                        .GetValues("Set-Cookie").FirstOrDefault() ?? "";

                return (responseCodeInt, location, newCookie);
            }

            Console.WriteLine($"请求失败，状态码：{response.StatusCode}");
            return (responseCodeInt, "", "");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return (CasAuthStatus.Failure.ToInt(), "", "");
        }
    }
}