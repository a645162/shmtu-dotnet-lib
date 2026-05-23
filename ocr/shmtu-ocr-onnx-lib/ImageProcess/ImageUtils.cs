using SkiaSharp;

namespace shmtu.captcha.onnx.ImageProcess;

public static class ImageUtils
{
    /// <summary>
    /// In-place 二值化：等价于 OpenCV cvtColor(BGR2GRAY) + threshold(thresh, 255, THRESH_BINARY) + merge 三通道。
    /// 亮度 (R*299 + G*587 + B*114)/1000 >= thresh -> 255，否则 -> 0；三通道写相同值。
    /// </summary>
    public static void BinarizeInPlace(SKBitmap image, int thresh)
    {
        var pixels = image.Pixels;
        for (var i = 0; i < pixels.Length; i++)
        {
            var p = pixels[i];
            var lum = (p.Red * 299 + p.Green * 587 + p.Blue * 114) / 1000;
            var v = (byte)(lum >= thresh ? 255 : 0);
            pixels[i] = new SKColor(v, v, v, 255);
        }
        image.Pixels = pixels;
    }
}
