using SkiaSharp;

namespace shmtu.captcha.onnx.Backend;

/// <summary>
/// 统一 v1 / v2 backend 签名。
/// 实现方负责持有自己的 <c>InferenceSession</c>、preprocess、output mapping。
/// </summary>
public interface ICasOcrBackend : IDisposable
{
    /// <summary>backend 是否已加载模型。</summary>
    bool IsLoaded { get; }

    /// <summary>当前 backend 标识（用于日志 / 配置持久化）。</summary>
    string BackendName => GetType().Name;

    /// <summary>
    /// 从目录加载模型文件。返回是否成功。
    /// </summary>
    bool LoadModel(string directoryPath, bool useGpu = false, int gpuDeviceId = 0);

    /// <summary>
    /// 单图推理，返回统一的预测结果。
    /// <c>EqualSymbol</c>：v1 = 0/1 (Chs/Symbol)，v2 = -1 (NotApplicable)。
    /// <c>Operator</c>：v1 = 0..5 (6-class 含 Chs)，v2 = 0..2 (3-class: +/-,×)。
    /// </summary>
    (int Result, string Expr, int EqualSymbol, int Operator, int Digit1, int Digit2)
        PredictValidateCode(SKBitmap originalImage);
}