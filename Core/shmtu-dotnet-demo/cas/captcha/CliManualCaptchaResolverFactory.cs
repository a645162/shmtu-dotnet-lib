using shmtu.cas.captcha;

namespace shmtu.cas.demo.cas.captcha;

public static class CliManualCaptchaResolverFactory
{
    public static ICaptchaResolver Create()
    {
        return new ManualCaptchaResolver((imageData, _) =>
        {
            Captcha.SaveImageToFile(imageData);
            Console.WriteLine("验证码图片已保存到当前目录，请打开查看。");
            Console.Write("请输入验证码计算结果（直接输入数字）: ");
            var input = Console.ReadLine() ?? "";
            return Task.FromResult(new CaptchaAnswer(input.Trim(), CaptchaAnswerKind.Answer));
        });
    }
}
