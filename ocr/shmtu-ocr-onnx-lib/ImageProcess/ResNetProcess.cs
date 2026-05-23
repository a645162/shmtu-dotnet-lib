using Microsoft.ML.OnnxRuntime.Tensors;
using SkiaSharp;

namespace shmtu.captcha.onnx.ImageProcess;

public static class ResNetProcess
{
    private const int Width = 224;
    private const int Height = 224;
    private const int Channels = 3;

    // ImageNet 标准 (R, G, B)
    private static readonly float[] Mean = { 123.675f, 116.28f, 103.53f };
    private static readonly float[] NormInv = { 1f / 58.395f, 1f / 57.12f, 1f / 57.375f };

    /// <summary>
    /// 把图像 resize 到 224x224 后转成 [1,3,224,224] NCHW float32 张量。
    /// 通道顺序: ch0=R, ch1=G, ch2=B；归一化: (v - mean) * (1/std)。
    /// 等价于 OpenCV cv::resize(INTER_LINEAR) + substract_mean_normalize。
    /// </summary>
    public static DenseTensor<float> ConvertImageToTensor(SKBitmap image)
    {
        var resized = new SKBitmap(Width, Height, image.ColorType, image.AlphaType);
        using (var canvas = new SKCanvas(resized))
        {
#pragma warning disable CS0618 // SKFilterQuality is obsolete in SkiaSharp 3.x but still functional
            using var paint = new SKPaint { FilterQuality = SKFilterQuality.Medium };
#pragma warning restore CS0618
            canvas.DrawBitmap(image, new SKRect(0, 0, Width, Height), paint);
        }

        var tensor = new DenseTensor<float>(new[] { 1, Channels, Height, Width });

        if (resized == null) return tensor;

        var pixels = resized.Pixels;
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var p = pixels[y * Width + x];
                tensor[0, 0, y, x] = (p.Red - Mean[0]) * NormInv[0];
                tensor[0, 1, y, x] = (p.Green - Mean[1]) * NormInv[1];
                tensor[0, 2, y, x] = (p.Blue - Mean[2]) * NormInv[2];
            }
        }

        return tensor;
    }
}
