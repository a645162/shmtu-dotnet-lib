namespace shmtu.cas.captcha;

public sealed class RemoteOcrCaptchaResolver : ICaptchaResolver
{
    public string Host { get; }
    public int Port { get; }

    public RemoteOcrCaptchaResolver(string host = "127.0.0.1", int port = 21601)
    {
        Host = host;
        Port = port;
    }

    public async Task<CaptchaAnswer> ResolveAsync(byte[] imageData, CancellationToken cancellationToken = default)
    {
        var expression = await Captcha.OcrByRemoteTcpServerAsync(Host, Port, imageData);
        return new CaptchaAnswer(expression, CaptchaAnswerKind.Expression);
    }
}
