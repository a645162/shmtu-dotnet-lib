using SkiaSharp;

namespace shmtu.captcha.onnx.ImageProcess;

public static class CasCaptchaImage
{
    public enum CasExprEqualSymbol
    {
        Chs = 0,
        Symbol = 1
    }

    public enum CasExprOperator
    {
        Add = 0,
        AddChs = 1,
        Sub = 2,
        SubChs = 3,
        Mul = 4,
        MulChs = 5
    }

    public const float EqualSymbolKeyStart = 0.7f;
    public const float EqualSymbolKeyEnd = 1.0f;
    public const int ConfigThresh = 200;
    public static readonly float[] KeyPointSymbol = { 0.25f, 0.58f, 0.75f };
    public static readonly float[] KeyPointChs = { 0.15f, 0.33f, 0.46f };

    public static string GetOperatorString(CasExprOperator exprOperator) =>
        exprOperator switch
        {
            CasExprOperator.Add or CasExprOperator.AddChs => "+",
            CasExprOperator.Sub or CasExprOperator.SubChs => "-",
            CasExprOperator.Mul or CasExprOperator.MulChs => "×",
            _ => ""
        };

    public static int CalculateOperator(int digit1, int digit2, CasExprOperator exprOperator) =>
        exprOperator switch
        {
            CasExprOperator.Add or CasExprOperator.AddChs => digit1 + digit2,
            CasExprOperator.Sub or CasExprOperator.SubChs => digit1 - digit2,
            CasExprOperator.Mul or CasExprOperator.MulChs => digit1 * digit2,
            _ => -1
        };

    public static SKBitmap SplitImgByRatio(SKBitmap image, float startRatio, float endRatio)
    {
        var width = image.Width;
        var height = image.Height;

        if (startRatio > endRatio) (startRatio, endRatio) = (endRatio, startRatio);

        var horizontalStart = (int)(width * startRatio);
        var horizontalEnd = endRatio >= 1f ? width : (int)(width * endRatio);

        var cropWidth = horizontalEnd - horizontalStart;
        var subset = new SKBitmap(cropWidth, height, image.ColorType, image.AlphaType);
        using (var canvas = new SKCanvas(subset))
        {
            var srcRect = SKRectI.Create(horizontalStart, 0, cropWidth, height);
            var dstRect = new SKRect(0, 0, cropWidth, height);
            canvas.DrawBitmap(image, srcRect, dstRect);
        }
        return subset;
    }
}
