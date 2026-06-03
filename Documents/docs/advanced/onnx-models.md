# ONNX 模型格式

本章说明 `shmtu-ocr-onnx-lib` 使用的三个 ONNX 模型的格式、输入输出规范、训练流程与如何微调。

## 模型清单

| 文件 | 用途 | Backbone | 输入 | 输出 | 大小 |
|---|---|---|---|---|---|
| `resnet18_equal_symbol_latest.onnx` | 等号分类 | ResNet-18 | 1×1×32×32 (float) | 1×3 (logits) | 42.6MB |
| `resnet18_operator_latest.onnx` | 运算符分类 | ResNet-18 | 1×1×32×32 (float) | 1×4 (logits) | 42.6MB |
| `resnet34_digit_latest.onnx` | 数字分类 | ResNet-34 | 1×1×32×32 (float) | 1×10 (logits) | 81.2MB |

## 输入输出格式

所有模型使用相同的输入规范：

| 字段 | 类型 | 取值 | 说明 |
|---|---|---|---|
| `input` | float[N, 1, 32, 32] | 0.0 ~ 1.0 | N=1, 单通道, 灰度, 32×32 像素 |

输出 logits 的解读：

| 模型 | 索引 | 类别 |
|---|---|---|
| **equal_symbol** | 0 | `=` |
| | 1 | `≠` |
| | 2 | `≈` |
| **operator** | 0 | `+` |
| | 1 | `-` |
| | 2 | `×` |
| | 3 | `÷` |
| **digit** | 0-9 | `0` ~ `9` |

## 推理代码

### CasOnnxBackend

```csharp
public class CasOnnxBackend : IDisposable
{
    private readonly InferenceSession _equalSymbol;
    private readonly InferenceSession _operator;
    private readonly InferenceSession _digit;

    public CasOnnxBackend(string modelDir, OcrOptions options)
    {
        _equalSymbol = new InferenceSession(
            Path.Combine(modelDir, $"resnet18_equal_symbol_e{options.EqualSymbolModelEpoch}.onnx"));
        _operator = new InferenceSession(
            Path.Combine(modelDir, $"resnet18_operator_e{options.OperatorModelEpoch}.onnx"));
        _digit = new InferenceSession(
            Path.Combine(modelDir, $"resnet34_digit_e{options.DigitModelEpoch}.onnx"));
    }

    public int PredictEqualSymbol(float[,,,] input)
    {
        return Predict(_equalSymbol, input);
    }

    public int PredictOperator(float[,,,] input)
    {
        return Predict(_operator, input);
    }

    public int PredictDigit(float[,,,] input)
    {
        return Predict(_digit, input);
    }

    private static int Predict(InferenceSession session, float[,,,] input)
    {
        var tensor = OrtValue.CreateTensorValueFromMemory(
            OrtMemoryInfo.DefaultInstance,
            input.AsMemory(),
            new long[] { 1, 1, 32, 32 });

        var inputs = new Dictionary<string, OrtValue> { ["input"] = tensor };
        using var results = session.Run(inputs);
        var logits = results[0].GetTensorDataAsSpan<float>().ToArray();

        return ArgMax(logits);
    }
}
```

### 图像预处理

```csharp
public class CasCaptchaImage
{
    public static (List<float[,,,]> chars, int width, int height) Process(byte[] pngBytes)
    {
        using var img = Image.Load<L8>(pngBytes);  // 灰度

        // 1. 二值化
        var binary = Binarize(img, threshold: 128);

        // 2. 字符切割（垂直投影）
        var segments = VerticalSegment(binary);

        // 3. 归一化到 32x32
        var normalized = segments
            .Select(seg => Normalize(seg, 32, 32))
            .ToList();

        return (normalized, img.Width, img.Height);
    }

    private static float[,,,] Normalize(byte[] seg, int w, int h)
    {
        var tensor = new float[1, 1, h, w];
        // ... 缩放 + 居中 + 归一化到 [0, 1]
        return tensor;
    }
}
```

## 训练流程

训练数据来自校园验证码的标注：

```
captchas/
├── 2024-01-15/
│   ├── 0001.png  # 标注 "1234"
│   ├── 0002.png  # 标注 "5678"
│   └── labels.json
├── 2024-01-16/
│   └── ...
```

训练脚本（PyTorch）：

```python
import torch
import torchvision
from torch import nn

class CaptchaResNet(nn.Module):
    def __init__(self, num_classes):
        super().__init__()
        self.backbone = torchvision.models.resnet18(weights=None)
        self.backbone.conv1 = nn.Conv2d(1, 64, kernel_size=3, padding=1)
        self.backbone.fc = nn.Linear(512, num_classes)

    def forward(self, x):
        return self.backbone(x)

# 训练循环
model = CaptchaResNet(num_classes=10)
optimizer = torch.optim.Adam(model.parameters(), lr=1e-3)
criterion = nn.CrossEntropyLoss()

for epoch in range(50):
    for images, labels in dataloader:
        logits = model(images)
        loss = criterion(logits, labels)
        optimizer.zero_grad()
        loss.backward()
        optimizer.step()
```

## 导出 ONNX

```python
# 训练完成后
dummy_input = torch.randn(1, 1, 32, 32)
torch.onnx.export(
    model,
    dummy_input,
    "resnet34_digit_e50.onnx",
    input_names=["input"],
    output_names=["output"],
    dynamic_axes={"input": {0: "batch"}, "output": {0: "batch"}},
    opset_version=17
)
```

## 验证模型

```python
import onnx
import onnxruntime as ort

# 验证模型格式
model = onnx.load("resnet34_digit_e50.onnx")
onnx.checker.check_model(model)

# 验证推理
session = ort.InferenceSession("resnet34_digit_e50.onnx")
result = session.run(None, {"input": dummy_input.numpy()})
print(result[0].shape)  # (1, 10)
```

## 微调建议

| 现象 | 调整 |
|---|---|
| 某字符经常识别错 | 增加该字符的训练样本 |
| 整体准确率下降 | 加入更多样化的验证码 |
| 推理变慢 | 换 ResNet-18 或量化 |
| 模型文件过大 | 量化 (int8) 后从 81MB → 20MB |

## 量化

```python
from onnxruntime.quantization import quantize_dynamic, QuantType

quantize_dynamic(
    "resnet34_digit_e50.onnx",
    "resnet34_digit_e50_int8.onnx",
    weight_type=QuantType.QInt8
)
```

## 部署到生产

新模型上线流程：

1. 在训练环境导出新 ONNX
2. 上传到 GitHub Release（与 `shmtu-cas-ocr-models` 仓库）
3. 提升 epoch 编号
4. OCR HTTP 服务启动时自动从 Release 下载新模型

## 下一步

- [多语言绑定](/advanced/multi-language) — 用其他语言调用本库
- [NuGet 发布与 CI](/advanced/nuget-ci) — 模型发布流水线
