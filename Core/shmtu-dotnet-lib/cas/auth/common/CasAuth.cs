using System.Net;
using HtmlAgilityPack;

namespace shmtu.cas.auth.common;

/// <summary>
/// CAS 登录原子操作。对齐 Rust 的 <c>cas::{get_execution, cas_login, cas_redirect}</c>。
/// 所有方法共享同一个 <see cref="CasHttpClient"/>，cookie 由 CookieContainer 自动管理。
/// </summary>
public static class CasAuth
{
    public const string UserAgent = CasHttpClient.DefaultUserAgent;

    /// <summary>
    /// 拉登录页，从中提取 execution 令牌。
    /// </summary>
    public static async Task<string> GetExecutionString(
        CasHttpClient client,
        string url = "https://cas.shmtu.edu.cn/cas/login",
        CancellationToken cancellationToken = default)
    {
        using var response = await client.HttpClient.GetAsync(url, cancellationToken);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var htmlCode = await response.Content.ReadAsStringAsync(cancellationToken);
            var document = new HtmlDocument();
            document.LoadHtml(htmlCode);
            var element = document.DocumentNode.SelectSingleNode("//input[@name='execution']");
            return (element?.GetAttributeValue("value", "") ?? "").Trim();
        }

        if (response.StatusCode == HttpStatusCode.Redirect)
        {
            // TGC 仍有效 — CAS 自动认证，跟随重定向以建立目标站点的 session
            var location = response.Headers.Location?.ToString() ?? "";
            if (!string.IsNullOrEmpty(location))
                await CasRedirect(client, location, cancellationToken);
            return ""; // 空字符串表示无需提交登录表单
        }

        Console.WriteLine($"Get execution string error:{(int)response.StatusCode}");
        return "";
    }

    /// <summary>
    /// 提交登录表单。
    /// </summary>
    public static async Task<CasAuthResult> CasLogin(
        CasHttpClient client,
        string url,
        string username,
        string password,
        string validateCode,
        string execution,
        CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, string>
        {
            ["username"] = username.Trim(),
            ["password"] = password.Trim(),
            ["validateCode"] = validateCode.Trim(),
            ["execution"] = execution.Trim(),
            ["_eventId"] = "submit",
            ["geolocation"] = "",
        };

        using var content = new FormUrlEncodedContent(body);
        using var response = await client.HttpClient.PostAsync(url, content, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Redirect)
        {
            var location = response.Headers.Location?.ToString() ?? "";
            return new CasAuthResult.Success(location);
        }

        var htmlCode = await response.Content.ReadAsStringAsync(cancellationToken);
        var document = new HtmlDocument();
        document.LoadHtml(htmlCode);
        var element = document.DocumentNode.SelectSingleNode("//*[@id=\"loginErrorsPanel\"]");
        var errorText = (element?.InnerText ?? "").Trim();

        if (errorText.Contains("account is not recognized") || errorText.Contains("用户名或密码"))
            return new CasAuthResult.PasswordError();

        if (errorText.Contains("reCAPTCHA") || errorText.Contains("验证码"))
            return new CasAuthResult.ValidateCodeError();

        return new CasAuthResult.Failure(errorText);
    }

    /// <summary>
    /// 跟随重定向链，最多循环 10 次。对齐 Rust 的 <c>cas_redirect</c>。
    /// </summary>
    public static async Task CasRedirect(
        CasHttpClient client,
        string url,
        CancellationToken cancellationToken = default)
    {
        var currentUrl = url;

        for (var i = 0; i < 10; i++)
        {
            using var response = await client.HttpClient.GetAsync(currentUrl, cancellationToken);

            if (response.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.MovedPermanently)
            {
                var location = response.Headers.Location?.ToString();
                if (string.IsNullOrEmpty(location)) break;
                currentUrl = location;
            }
            else
            {
                break;
            }
        }
    }
}
