using System.Net;
using shmtu.cas.auth.common;
using shmtu.cas.captcha;
using shmtu.datatype.bill;

namespace shmtu.cas.auth;

/// <summary>
/// ePay（一卡通）账单的登录与拉取。对齐 Rust 的 <c>cas::epay::EpayAuth</c>。
/// 提供 4 个原子方法：ProbeLoginAsync / PrepareChallengeAsync / SubmitLoginAsync / TestLoginStatusAsync。
/// 重试循环由调用方负责（参见 BillDemo）。
/// </summary>
public sealed class EpayAuth : IDisposable
{
    public const string EpayBillUrl = "https://ecard.shmtu.edu.cn/epay/consume/query";

    public CasHttpClient HttpClient { get; }
    public ICaptchaResolver CaptchaResolver { get; }

    private string? _loginUrl;
    private readonly bool _ownsHttpClient;

    public EpayAuth(ICaptchaResolver captchaResolver)
        : this(captchaResolver, new CasHttpClient(), ownsHttpClient: true)
    {
    }

    public EpayAuth(ICaptchaResolver captchaResolver, CasHttpClient httpClient)
        : this(captchaResolver, httpClient, ownsHttpClient: false)
    {
    }

    private EpayAuth(ICaptchaResolver captchaResolver, CasHttpClient httpClient, bool ownsHttpClient)
    {
        CaptchaResolver = captchaResolver;
        HttpClient = httpClient;
        _ownsHttpClient = ownsHttpClient;
    }

    public void Dispose()
    {
        if (_ownsHttpClient) HttpClient.Dispose();
    }

    public static string GetBillUrl(int pageNo = 1, BillType type = BillType.All)
        => GetBillUrl(pageNo.ToString(), type);

    public static string GetBillUrl(string pageNoString = "1", BillType type = BillType.All)
        => GetBillUrl(pageNoString, GetTabNo(type));

    public static string GetBillUrl(string pageNoString = "1", string tabNoString = "1")
        => $"{EpayBillUrl}?pageNo={pageNoString}&tabNo={tabNoString}";

    public static string GetTabNo(BillType type) => type switch
    {
        BillType.All => "1",
        BillType.Success => "2",
        BillType.NotPaid => "3",
        BillType.Failure => "4",
        _ => "1",
    };

    /// <summary>
    /// 探测当前是否已登录。对齐 Rust 的 <c>probe_login</c>。
    /// </summary>
    public async Task<LoginProbe> ProbeLoginAsync(CancellationToken cancellationToken = default)
    {
        var url = GetBillUrl(1, BillType.All);
        using var response = await HttpClient.HttpClient.GetAsync(url, cancellationToken);

        if (response.StatusCode == HttpStatusCode.OK)
            return new LoginProbe.AlreadyLoggedIn();

        if (response.StatusCode == HttpStatusCode.Redirect)
        {
            var location = response.Headers.Location?.ToString() ?? "";
            if (string.IsNullOrEmpty(location))
                throw new InvalidOperationException("重定向URL为空");

            _loginUrl = location;
            return new LoginProbe.NeedLogin(location);
        }

        throw new HttpRequestException(
            $"探测登录状态失败，状态码: {(int)response.StatusCode}");
    }

    /// <summary>
    /// 获取 execution 令牌 + 验证码图片，交给 <see cref="CaptchaResolver"/> 解。
    /// 对齐 Rust 的 <c>prepare_challenge</c>。
    /// </summary>
    public async Task<LoginChallenge> PrepareChallengeAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_loginUrl))
            throw new InvalidOperationException("尚未探测登录状态，请先调用 ProbeLoginAsync");

        var execution = await CasAuth.GetExecutionString(HttpClient, _loginUrl, cancellationToken);
        var captchaImage = await Captcha.FetchCaptcha(HttpClient, cancellationToken);
        return new LoginChallenge(execution, captchaImage);
    }

    /// <summary>
    /// 完整的"准备挑战 + 让 resolver 解 + 提交登录"流程，便于调用方一行搞定。
    /// </summary>
    public async Task<LoginSubmitResult> SubmitLoginAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        var challenge = await PrepareChallengeAsync(cancellationToken);
        var answer = await CaptchaResolver.ResolveAsync(challenge.CaptchaImage, cancellationToken);
        var validateCode = answer.Kind == CaptchaAnswerKind.Expression
            ? Captcha.GetExprResult(answer.Value)
            : answer.Value.Trim();

        return await SubmitLoginAsync(username, password, validateCode, challenge.Execution, cancellationToken);
    }

    /// <summary>
    /// 提交一次登录尝试（外部已解出 validateCode）。对齐 Rust 的 <c>submit_login</c>。
    /// </summary>
    public async Task<LoginSubmitResult> SubmitLoginAsync(
        string username,
        string password,
        string validateCode,
        string execution,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_loginUrl))
            throw new InvalidOperationException("尚未探测登录状态，请先调用 ProbeLoginAsync");

        var result = await CasAuth.CasLogin(
            HttpClient, _loginUrl, username, password, validateCode, execution, cancellationToken);

        switch (result)
        {
            case CasAuthResult.Success success:
                await CasAuth.CasRedirect(HttpClient, success.Location, cancellationToken);
                return new LoginSubmitResult.Success();
            case CasAuthResult.ValidateCodeError:
                return new LoginSubmitResult.ValidateCodeError();
            case CasAuthResult.PasswordError:
                return new LoginSubmitResult.PasswordError();
            case CasAuthResult.Failure failure:
                return new LoginSubmitResult.Failure(failure.Message);
            default:
                return new LoginSubmitResult.Failure("未知状态");
        }
    }

    /// <summary>
    /// 测试是否已登录。对齐 Rust 的 <c>test_login_status</c>。
    /// </summary>
    public async Task<bool> TestLoginStatusAsync(CancellationToken cancellationToken = default)
    {
        var url = GetBillUrl(1, BillType.All);
        using var response = await HttpClient.HttpClient.GetAsync(url, cancellationToken);
        return response.StatusCode == HttpStatusCode.OK;
    }

    /// <summary>
    /// 获取账单页面 HTML。对齐 Rust 的 <c>get_bill</c>。
    /// </summary>
    public async Task<string> GetBillAsync(
        int pageNo = 1,
        BillType type = BillType.All,
        CancellationToken cancellationToken = default)
    {
        var url = GetBillUrl(pageNo, type);
        using var response = await HttpClient.HttpClient.GetAsync(url, cancellationToken);

        if (response.StatusCode == HttpStatusCode.OK)
            return await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.StatusCode == HttpStatusCode.Redirect)
            throw new InvalidOperationException("未登录，需要重新登录");

        throw new HttpRequestException(
            $"获取账单失败，状态码: {(int)response.StatusCode}");
    }
}
