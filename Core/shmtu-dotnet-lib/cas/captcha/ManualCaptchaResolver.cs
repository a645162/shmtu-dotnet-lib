namespace shmtu.cas.captcha;

public sealed class ManualCaptchaResolver : ICaptchaResolver
{
    private readonly Func<byte[], CancellationToken, Task<CaptchaAnswer>> _handler;

    public ManualCaptchaResolver(Func<byte[], CancellationToken, Task<CaptchaAnswer>> handler)
    {
        _handler = handler;
    }

    public ManualCaptchaResolver(Func<byte[], Task<CaptchaAnswer>> handler)
        : this((img, _) => handler(img))
    {
    }

    public ManualCaptchaResolver(Func<byte[], Task<string>> handler)
        : this(async (img, _) => new CaptchaAnswer(await handler(img), CaptchaAnswerKind.Answer))
    {
    }

    public Task<CaptchaAnswer> ResolveAsync(byte[] imageData, CancellationToken cancellationToken = default)
        => _handler(imageData, cancellationToken);
}
