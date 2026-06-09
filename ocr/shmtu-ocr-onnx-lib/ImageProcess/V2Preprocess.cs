using Microsoft.ML.OnnxRuntime.Tensors;
using SkiaSharp;

namespace shmtu.captcha.onnx.ImageProcess;

/// <summary>
/// v2 trislot decoder 模型的图像预处理。
/// - 单通道灰度 (R*299 + G*587 + B*114)/1000
/// - resize 到 64×192 (height × width)
/// - 像素值 / 255.0 → [0, 1]
/// - 输出 shape = [1, 1, 64, 192] 的 NCHW float32 tensor
/// </summary>
public static class V2Preprocess
{
    public const int Width = 192;
    public const int Height = 64;
    public const int Channels = 1;

    /// <summary>
    /// 把任意尺寸的 SKBitmap resize 到 64×192 灰度并归一化为 [0,1]。
    /// </summary>
    public static DenseTensor<float> ConvertToV2Tensor(SKBitmap image)
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
        var pixels = resized.Pixels;
        const float inv255 = 1f / 255f;

        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var p = pixels[y * Width + x];
                // ITU-R BT.601 luma
                var lum = (p.Red * 299 + p.Green * 587 + p.Blue * 114) / 1000;
                tensor[0, 0, y, x] = lum * inv255;
            }
        }

        return tensor;
    }
}