namespace shmtu.cas.auth.common;

/// <summary>
/// 提交登录表单后的结果。对齐 Rust 的 <c>LoginSubmitResult</c> enum。
/// </summary>
public abstract record LoginSubmitResult
{
    private LoginSubmitResult() { }

    public sealed record Success : LoginSubmitResult;
    public sealed record ValidateCodeError : LoginSubmitResult;
    public sealed record PasswordError : LoginSubmitResult;
    public sealed record Failure(string Message) : LoginSubmitResult;
}
