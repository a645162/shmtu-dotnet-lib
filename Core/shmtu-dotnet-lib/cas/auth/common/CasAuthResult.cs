namespace shmtu.cas.auth.common;

/// <summary>
/// CAS 登录原子操作的状态。对齐 Rust 的 <c>CasAuthResult</c> enum。
/// </summary>
public abstract record CasAuthResult
{
    private CasAuthResult() { }

    public sealed record Success(string Location) : CasAuthResult;
    public sealed record ValidateCodeError : CasAuthResult;
    public sealed record PasswordError : CasAuthResult;
    public sealed record Failure(string Message) : CasAuthResult;
}
