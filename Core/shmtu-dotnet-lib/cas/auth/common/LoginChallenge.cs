namespace shmtu.cas.auth.common;

/// <summary>
/// 一次登录尝试所需的材料：execution 令牌 + 验证码图片字节。
/// 对齐 Rust 的 <c>LoginChallenge</c> struct。
/// </summary>
public sealed record LoginChallenge(string Execution, byte[] CaptchaImage);
