namespace shmtu.captcha.onnx;

public static class ConstValue
{
    public const string ModelOnnxBaseUrl =
        "https://gitee.com/a645162/shmtu-cas-ocr-model/releases/download/v1.0-ONNX";

    public const string ModelOnnxEqualFp32 = "resnet18_equal_symbol_latest.onnx";
    public const string ModelOnnxOperatorFp32 = "resnet18_operator_latest.onnx";
    public const string ModelOnnxDigitFp32 = "resnet34_digit_latest.onnx";

    public static readonly string[] AllModelFiles =
    {
        ModelOnnxEqualFp32,
        ModelOnnxOperatorFp32,
        ModelOnnxDigitFp32
    };
}
