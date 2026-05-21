using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace shmtu.captcha.onnx.ImageProcess;

public static class ImageUtils
{
    /// <summary>
    /// In-place 二值化：等价于 OpenCV cvtColor(BGR2GRAY) + threshold(thresh, 255, THRESH_BINARY) + merge 三通道。
    /// 亮度 (R*299 + G*587 + B*114)/1000 >= thresh -> 255，否则 -> 0；三通道写相同值。
    /// </summary>
    public static void BinarizeInPlace(Image<Rgba32> image, int thresh)
    {
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    var p = row[x];
                    var lum = (p.R * 299 + p.G * 587 + p.B * 114) / 1000;
                    var v = (byte)(lum >= thresh ? 255 : 0);
                    row[x] = new Rgba32(v, v, v, 255);
                }
            }
        });
    }
}
