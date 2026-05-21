using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

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
    public static DenseTensor<float> ConvertImageToTensor(Image<Rgba32> image)
    {
        using var resized = image.Clone(ctx =>
            ctx.Resize(new ResizeOptions
            {
                Size = new Size(Width, Height),
                Mode = ResizeMode.Stretch,
                Sampler = KnownResamplers.Triangle
            }));

        var tensor = new DenseTensor<float>(new[] { 1, Channels, Height, Width });

        resized.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < Width; x++)
                {
                    var p = row[x];
                    tensor[0, 0, y, x] = (p.R - Mean[0]) * NormInv[0];
                    tensor[0, 1, y, x] = (p.G - Mean[1]) * NormInv[1];
                    tensor[0, 2, y, x] = (p.B - Mean[2]) * NormInv[2];
                }
            }
        });

        return tensor;
    }
}
