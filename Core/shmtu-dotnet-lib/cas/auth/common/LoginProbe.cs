namespace shmtu.cas.auth.common;

/// <summary>
/// 探测登录状态的结果。对齐 Rust 的 <c>LoginProbe</c> enum。
/// </summary>
public abstract record LoginProbe
{
    private LoginProbe() { }

    public sealed record AlreadyLoggedIn : LoginProbe;
    public sealed record NeedLogin(string LoginUrl) : LoginProbe;
}
