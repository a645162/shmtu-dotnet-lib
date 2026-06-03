# ONNX 推理

`shmtu-ocr-onnx-lib` 提供基于 ResNet 模型的本地 ONNX 推理引擎，用于识别 CAS 验证码。本章说明模型加载、推理流程和性能调优。

## 三个模型

| 模型 | 文件 | 用途 | 输入 | 大小 |
|---|---|---|---|---|
| 等号 | `resnet18_equal_symbol_latest.onnx` | 区分 `=` `≈` `≠` | 32x32x1 灰度 | 42.6MB |
| 运算符 | `resnet18_operator_latest.onnx` | `+` `-` `×` `÷` | 32x32x1 灰度 | 42.6MB |
| 数字 | `resnet34_digit_latest.onnx` | 0-9 数字 | 32x32x1 灰度 | 81.2MB |

## 核心 API

```csharp
namespace shmtu.ocr.onnx;

public class CasOcr
{
    public CasOcr(string modelDir, OcrOptions options = null);
    public string Recognize(byte[] imageBytes);
    public Task<string> RecognizeAsync(byte[] imageBytes, CancellationToken ct = default);
}

public sealed record OcrOptions
{
    public int EqualSymbolModelEpoch { get; init; } = -1;  // -1 = latest
    public int OperatorModelEpoch { get; init; } = -1;
    public int DigitModelEpoch { get; init; } = -1;
    public bool UseGpu { get; init; } = false;
    public int IntraOpNumThreads { get; init; } = 2;
}
```

## 推理流程

```
输入验证码 PNG/JPG
   ↓
CasCaptchaImage.Process
   ├─ 灰度化（luma = 0.299R + 0.587G + 0.114B）
   ├─ 二值化（threshold = 128）
   ├─ 字符切割（垂直投影 + 连通域）
   └─ 归一化到 32x32
   ↓
每个字符调用 ResNet 模型
   ├─ 等号模型（如字符是 =）
   ├─ 运算符模型（如字符是 +/-/×/÷）
   └─ 数字模型（如字符是 0-9）
   ↓
拼接结果 → "1234" 或 "12+34"
```

## 基础用法

```csharp
using shmtu.ocr.onnx;

// 1. 初始化
var ocr = new CasOcr(
    modelDir: "ocr/models",
    options: new OcrOptions
    {
        UseGpu = false,
        IntraOpNumThreads = 2
    });

// 2. 识别
var captchaBytes = await EpayAuth.FetchCaptchaAsync();
var text = ocr.Recognize(captchaBytes);

Console.WriteLine($"识别结果: {text}");
```

## 异步与并发

```csharp
// 单张识别
var text = await ocr.RecognizeAsync(captchaBytes);

// 批量识别
var tasks = captchas.Select(c => ocr.RecognizeAsync(c));
var results = await Task.WhenAll(tasks);
```

> `CasOcr` 实例**不是线程安全的**。多线程需要每个线程一个实例（或用对象池）。

## GPU 加速

需要安装 ONNX Runtime GPU 变体：

```bash
dotnet add package Microsoft.ML.OnnxRuntime.Gpu
# 或
dotnet add package Microsoft.ML.OnnxRuntime.DirectML  # Windows
```

启用：

```csharp
var ocr = new CasOcr(modelDir, new OcrOptions
{
    UseGpu = true,
    // DirectML: 自动检测 NVIDIA/AMD/Intel GPU
    // CUDA: 需指定 cudaDeviceId
});
```

性能对比（单张验证码）：

| 后端 | 耗时 |
|---|---|
| CPU (2 threads) | ~50ms |
| CPU (8 threads) | ~20ms |
| DirectML (NVIDIA) | ~5ms |
| CUDA (RTX 3060) | ~3ms |

## 模型自动下载

OCR HTTP 服务（`shmtu-ocr-onnx-server`）首次启动会自动从 GitHub Release 下载模型：

```csharp
// 在 server/Program.cs 中
var modelDownloader = new ModelDownloader("https://github.com/a645162/shmtu-cas-ocr-models/releases/latest");
await modelDownloader.EnsureModelsAsync("ocr/models");
```

## 手动加载特定版本

```csharp
var options = new OcrOptions
{
    EqualSymbolModelEpoch = 5,   // 用第 5 轮的权重
    OperatorModelEpoch = 3,
    DigitModelEpoch = 7
};
```

模型文件名包含 epoch：`resnet18_equal_symbol_e5.onnx`。

## 预热

首次推理会比较慢（onnxruntime 初始化），生产环境可以预热：

```csharp
// 预热：跑一次假数据
var dummy = new byte[1024];
ocr.Recognize(dummy);  // 首次慢

// 之后每次都很快
var text = ocr.Recognize(realCaptcha);
```

## 错误处理

```csharp
try
{
    var text = ocr.Recognize(captchaBytes);
}
catch (ModelNotFoundException ex)
{
    Console.WriteLine($"模型文件缺失: {ex.Path}");
    // 下载模型
}
catch (InvalidImageException ex)
{
    Console.WriteLine("验证码图片无效");
}
catch (OnnxRuntimeException ex)
{
    Console.WriteLine($"推理失败: {ex.Message}");
}
```

## 单元测试

```csharp
[Fact]
public void CasOcr_RecognizesSample()
{
    var ocr = new CasOcr("ocr/models");
    var sample = File.ReadAllBytes("Samples/captcha-1234.png");
    var text = ocr.Recognize(sample);
    Assert.Equal("1234", text);
}
```

## 下一步

- [OCR HTTP 服务](/guide/ocr-server) — 独立部署的 HTTP 服务
- [OCR TCP 服务](/guide/ocr-tcp-server) — 高性能 TCP 版本
