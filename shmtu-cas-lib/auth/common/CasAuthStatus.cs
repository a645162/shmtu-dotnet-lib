namespace shmtu.cas.auth.common;

public enum CasAuthStatus
{
    Success = 200,
    ValidateCodeError = -100,
    PasswordError = -200,
    Failure = 404
}

public static class EnumExtensions
{
    public static int ToInt(this CasAuthStatus status)
    {
        return (int)status;
    }
}