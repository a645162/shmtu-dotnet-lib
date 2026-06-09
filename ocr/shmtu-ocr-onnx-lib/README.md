# shmtu-captcha-onnx-lib

跨平台的上海海事大学 CAS 验证码 OCR 识别库，基于 **ONNX Runtime + SixLabors.ImageSharp**，无 `System.Drawing` 依赖。

支持 v1（legacy）与 v2（**默认**）两套模型，可在构造时显式切换。

## 模型版本

| 版本 | 模型数量 | Backbone | 精度 | 输入 | Tag | 默认 |
|---|---|---|---|---|---|---|
| v1 | 3 | resnet18 / resnet34 | fp32 | RGB 3×224×224 | `v1.0-ONNX` | 否 |
| **v2** | **1** | `mobilenet_v3_small` | fp16 | 灰度 1×64×192 | `v2.0.x`（含 `model-assets.json`） | **是** |

详细对比与下载策略见根仓库 [Documents/docs/ocr-model-versions.md](../../../Documents/docs/ocr-model-versions.md)。

## 安装

将 `shmtu-ocr-onnx-lib.csproj` 添加为项目引用，或直接编译产出 `shmtu-ocr-onnx-lib.dll` 后引用。

依赖：

- .NET 8
- `Microsoft.ML.OnnxRuntime`（含可选 GPU EP）
- `SixLabors.ImageSharp` / `SkiaSharp`

## 快速开始（默认 v2）

```csharp
using shmtu.captcha.onnx;

// 默认走 v2 (mobilenet_v3_small + fp16)
using var ocr = new CasOcr(
    modelDirectoryPath: "./models",
    useGpu: false
);

// 检查 / 下载模型（缺失自动从 release 下载）
await ocr.EnsureModelsAsync();

ocr.LoadModel();
var (result, expr, eq, op, d1, d2) = ocr.PredictValidateCode("captcha.png");
Console.WriteLine($"{expr} = {result}");
```

## 显式使用 v1

```csharp
using shmtu.captcha.onnx;

using var ocr = new CasOcr(
    modelDirectoryPath: "./models",
    useGpu: false,
    version: ConstValue.ModelVersion.V1   // 走老的 3 模型 ResNet 路径
);

await ocr.EnsureModelsAsync();    // 拉取 3 个 ONNX + SHA256SUMS.txt
ocr.LoadModel();
var (result, expr, eq, op, d1, d2) = ocr.PredictValidateCode("captcha.png");
```

## API 概览

### `CasOcr` 主入口

```csharp
public sealed class CasOcr : IDisposable
{
    public CasOcr(
        string? modelDirectoryPath = null,
        bool useGpu = false,
        int gpuDeviceId = 0,
        ConstValue.ModelVersion version = ConstValue.DefaultVersion  // V2
    );

    // 当前 backend 的模型版本
    public ConstValue.ModelVersion Version { get; }

    // backend 标识（用于日志 / 配置持久化）
    public string BackendName { get; }

    // 模型目录，可读写
    public string ModelDirectoryPath { get; set; }

    // 模型是否已加载到内存
    public bool IsLoaded { get; }

    // 检查必需文件是否齐全
    public bool CheckModelIsExist();

    // 列出缺失的模型文件名
    public string[] GetMissingModelFiles();

    // 缺失则下载（GitHub / Gitee 互为 fallback）
    public Task<bool> EnsureModelsAsync(
        IProgress<float>? progress = null,
        HttpClient? httpClient = null,
        Action<string>? log = null
    );

    // 显式加载模型到 ONNX Runtime
    public bool LoadModel();

    // 推理：返回 (Result, Expr, EqualSymbol, Operator, Digit1, Digit2)
    public (int Result, string Expr, int EqualSymbol, int Operator, int Digit1, int Digit2)
        PredictValidateCode(SKBitmap image);

    public (int Result, string Expr, int EqualSymbol, int Operator, int Digit1, int Digit2)
        PredictValidateCode(string imagePath);

    public (int Result, string Expr, int EqualSymbol, int Operator, int Digit1, int Digit2)
        PredictValidateCode(Stream stream);

    public (int Result, string Expr, int EqualSymbol, int Operator, int Digit1, int Digit2)
        PredictValidateCode(byte[] imageBytes);
}
```

### `ConstValue`

- `ConstValue.ModelVersion` 枚举：`V1` / `V2`
- `ConstValue.DefaultVersion` 常量：`V2`
- `ConstValue.V1.*`：v1 模型文件名、URL、SHA256SUMS 文件名
- `ConstValue.V2.*`：v2 默认 tag (`v2.0.2`)、backbone、precision、清单文件名 (`model-assets.json`)

## 模型下载策略

### v1（legacy）

下载 3 个 ONNX 模型（`equal_symbol` / `operator` / `digit`），通过 release 中的 `SHA256SUMS.txt` 校验。GitHub 与 Gitee 互为 fallback。

### v2（默认）

通过 release 根目录的 `model-assets.json` 清单，按 `{tag, backbone, precision, engine}` 维度查找匹配资产并下载，使用清单内嵌的 `sha256` 字段校验。

默认下载：`mobilenet_v3_small.trislot_decoder.v2_0.fp16.onnx`（约几 MB，单文件替代 v1 的 3 个模型）。

## GPU 加速

设置 `useGpu: true` 并确保 ONNX Runtime 已安装 GPU EP（CUDA / DirectML / TensorRT）。无 GPU 时该参数被忽略，CPU 推理照常工作。

## 返回值说明

`PredictValidateCode` 返回 `(int Result, string Expr, int EqualSymbol, int Operator, int Digit1, int Digit2)`：

- `Result`：算式结果（digit1 运算符 digit2 的值）
- `Expr`：完整算式字符串，如 `"3+5"`
- `EqualSymbol`：v1 返回 `0` = 中文等号 / `1` = 标准等号；v2 始终为 `-1`（v2 不区分等号样式）
- `Operator`：`0` = `+`，`1` = 中文加号，`2` = `-`，`3` = 中文减号，`4` = `*`，`5` = 中文乘号
- `Digit1` / `Digit2`：两个数字

识别失败时返回 `(-1, "", -1, -1, -1, -1)`。

## 平台

- Windows / Linux / macOS（任意支持 .NET 8 的平台）
- 移动端：可通过 `net8.0-android` / `net8.0-ios` 目标集成

## 相关链接

- 根仓库 OCR 总览：[Documents/docs/ocr-model-versions.md](../../../Documents/docs/ocr-model-versions.md)
- 模型训练与导出：[shmtu-cas-ocr-model V2 文档](https://a645162.github.io/shmtu-cas-ocr-model/usage/v2-quickstart)
