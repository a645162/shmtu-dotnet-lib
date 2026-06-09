namespace shmtu.captcha.onnx;

public static class ConstValue
{
    /// <summary>
    /// OCR 模型版本。默认 V2（单模型 trislot decoder）。
    /// </summary>
    public enum ModelVersion
    {
        V1 = 1,
        V2 = 2
    }

    /// <summary>默认模型版本。新接入用户使用 V2。</summary>
    public const ModelVersion DefaultVersion = ModelVersion.V2;

    /// <summary>
    /// v1 模型：3 个独立的 resnet18/34 ONNX 模型 (fp32)。原始行为，保留以兼容旧调用方。
    /// </summary>
    public static class V1
    {
        public const string ModelOnnxEqual = "resnet18_equal_symbol_latest.onnx";
        public const string ModelOnnxOperator = "resnet18_operator_latest.onnx";
        public const string ModelOnnxDigit = "resnet34_digit_latest.onnx";

        public static readonly string[] AllFiles =
        {
            ModelOnnxEqual,
            ModelOnnxOperator,
            ModelOnnxDigit
        };

        public const string BaseUrlGitee =
            "https://gitee.com/a645162/shmtu-cas-ocr-model/releases/download/v1.0-ONNX";
        public const string BaseUrlGithub =
            "https://github.com/a645162/shmtu-cas-ocr-model/releases/download/v1.0-ONNX";

        public const string ChecksumName = "SHA256SUMS.txt";
    }

    /// <summary>
    /// v2 模型：单个 trislot decoder 多输出 ONNX（mobilenet_v3_small, 默认 fp16）。
    /// 一次前向输出 digit_left/operator/digit_right 三个 tensor。
    /// </summary>
    public static class V2
    {
        public const string DefaultTag = "v2.0.2";
        public const string DefaultBackbone = "mobilenet_v3_small";
        public const string DefaultPrecision = "fp16";
        public const string ModelFamily = "trislot_decoder";

        public const string ManifestName = "model-assets.json";

        public const string BaseUrlGithub =
            "https://github.com/a645162/shmtu-cas-ocr-model/releases/download";
        public const string BaseUrlGitee =
            "https://gitee.com/a645162/shmtu-cas-ocr-model/releases/download";

        /// <summary>
        /// v2 单模型文件命名: {backbone}.{family}.v2_0.{precision}.onnx
        /// 例: mobilenet_v3_small.trislot_decoder.v2_0.fp16.onnx
        /// </summary>
        public static string BuildModelName(string backbone, string precision)
            => $"{backbone}.{ModelFamily}.v2_0.{precision}.onnx";

        /// <summary>v2 默认主模型文件名（与 mobilenet_v3_small + fp16 一致）。</summary>
        public static string DefaultModelName =>
            BuildModelName(DefaultBackbone, DefaultPrecision);
    }

    // ---- 兼容旧 API 的 deprecated re-export（指向 v1，调用方无感） ----
    public const string ModelOnnxEqualFp32 = V1.ModelOnnxEqual;
    public const string ModelOnnxOperatorFp32 = V1.ModelOnnxOperator;
    public const string ModelOnnxDigitFp32 = V1.ModelOnnxDigit;

    public static readonly string[] AllModelFiles = V1.AllFiles;

    public const string ModelOnnxBaseUrl = V1.BaseUrlGitee;
    public const string ModelOnnxChecksumUrl = V1.BaseUrlGitee + "/" + V1.ChecksumName;
}