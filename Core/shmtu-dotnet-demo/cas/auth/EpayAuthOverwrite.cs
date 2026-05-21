using shmtu.cas.auth;

namespace shmtu.cas.demo.cas.auth;

public class EpayAuthOverwrite : EpayAuth
{
    protected override async Task<string> GetCaptchaResult(byte[] imageData)
    {
        var result = await base.GetCaptchaResult(imageData);

        Console.WriteLine("GetCaptchaResult Overwritten!");

        return result;
    }
}