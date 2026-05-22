using System.Net;

namespace shmtu.cas.auth.common;

/// <summary>
/// 共享 HttpClient + CookieContainer，提供给 CasAuth/Captcha/EpayAuth 复用。
/// 对齐 Rust 的 <c>reqwest::Client</c>（cookie_store + redirect::none）。
/// </summary>
public sealed class CasHttpClient : IDisposable
{
    public const string DefaultUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
        "AppleWebKit/537.36 (KHTML, like Gecko) " +
        "Chrome/126.0.0.0 Safari/537.36 Edg/126.0.0.0";

    public CookieContainer CookieContainer { get; }
    public HttpClient HttpClient { get; }
    public string UserAgent { get; }

    public CasHttpClient(string userAgent = DefaultUserAgent)
    {
        UserAgent = userAgent;
        CookieContainer = new CookieContainer();

        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = false,
            UseCookies = true,
            CookieContainer = CookieContainer,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
        };

        HttpClient = new HttpClient(handler);
        HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
    }

    public void Dispose()
    {
        HttpClient.Dispose();
    }
}
