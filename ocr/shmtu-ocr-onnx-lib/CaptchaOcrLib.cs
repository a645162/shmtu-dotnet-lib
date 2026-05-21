using System.Reflection;

namespace shmtu.captcha.onnx;

public static class CaptchaOcrLib
{
    public static string Version => Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "";
}
