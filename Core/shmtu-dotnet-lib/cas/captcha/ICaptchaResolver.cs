namespace shmtu.cas.captcha;

public interface ICaptchaResolver
{
    Task<CaptchaAnswer> ResolveAsync(byte[] imageData, CancellationToken cancellationToken = default);
}
