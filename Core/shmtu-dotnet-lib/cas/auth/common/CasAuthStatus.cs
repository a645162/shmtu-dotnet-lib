namespace shmtu.cas.auth.common;

public enum CasAuthStatus
{
    Success = 200,
    Redirect = 302,
    ValidateCodeError = -100,
    PasswordError = -200,
    Failure = 404,
    UnrecoverableError = 0
}

public static class EnumExtensions
{
    public static int ToInt(this CasAuthStatus status)
    {
        return (int)status;
    }
}