using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using shmtu.captcha.onnx.ImageProcess;
using shmtu.captcha.onnx.Utils;
using SkiaSharp;

namespace shmtu.captcha.onnx.Backend;

public sealed class CasOnnxBackend : IDisposable
{
    private InferenceSession? _sessionDigit;
    private InferenceSession? _sessionEqualSymbol;
    private InferenceSession? _sessionOperator;
    private bool _isLoaded;

    public bool IsLoaded =>
        _isLoaded &&
        _sessionOperator != null &&
        _sessionDigit != null &&
        _sessionEqualSymbol != null;

    public static bool CheckModelIsExist(string directoryPath)
    {
        if (!Directory.Exists(directoryPath)) return false;
        var basePath = Path.GetFullPath(directoryPath);
        return File.Exists(Path.Combine(basePath, ConstValue.ModelOnnxEqualFp32)) &&
               File.Exists(Path.Combine(basePath, ConstValue.ModelOnnxOperatorFp32)) &&
               File.Exists(Path.Combine(basePath, ConstValue.ModelOnnxDigitFp32));
    }

    public static async Task<bool> DownloadModelAsync(
        string directoryPath,
        IProgress<float>? progress = null,
        HttpClient? httpClient = null)
    {
        directoryPath = Path.GetFullPath(directoryPath);
        if (!Directory.Exists(directoryPath)) Directory.CreateDirectory(directoryPath);

        var ownClient = httpClient is null;
        var client = httpClient ?? new HttpClient();

        try
        {
            var files = ConstValue.AllModelFiles;
            var increment = 100f / files.Length;

            for (var i = 0; i < files.Length; i++)
            {
                var fileName = files[i];
                var url = $"{ConstValue.ModelOnnxBaseUrl}/{fileName}";
                var localPath = Path.Combine(directoryPath, fileName);

                var start = i * increment;
                await NetworkFile.DownloadFileAsync(client, url, localPath, new Progress<float>(p =>
                {
                    var adjusted = start + p / 100f * increment;
                    if (adjusted > 100) adjusted = 100;
                    progress?.Report(adjusted);
                }));
            }

            progress?.Report(100f);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"模型下载出错：{ex.Message}");
            return false;
        }
        finally
        {
            if (ownClient) client.Dispose();
        }
    }

    public bool LoadModel(string directoryPath, bool useGpu = false, int gpuDeviceId = 0)
    {
        if (!CheckModelIsExist(directoryPath)) return false;
        var basePath = Path.GetFullPath(directoryPath);

        var options = new SessionOptions();

#if GPU_BUILD
        if (useGpu)
        {
            options.AppendExecutionProvider_CUDA(gpuDeviceId);
            Console.WriteLine($"[OCR] 使用 CUDA GPU 设备 {gpuDeviceId}");
        }
#endif

        _sessionEqualSymbol = new InferenceSession(Path.Combine(basePath, ConstValue.ModelOnnxEqualFp32), options);
        _sessionOperator = new InferenceSession(Path.Combine(basePath, ConstValue.ModelOnnxOperatorFp32), options);
        _sessionDigit = new InferenceSession(Path.Combine(basePath, ConstValue.ModelOnnxDigitFp32), options);
        _isLoaded = true;
        return true;
    }

    private static int PredictModel(InferenceSession? session, DenseTensor<float> inputTensor)
    {
        if (session == null) return -1;

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input", inputTensor)
        };

        using var results = session.Run(inputs);
        if (results.Count == 0) return -1;

        var outputTensor = results[0].AsTensor<float>();
        var array = outputTensor.ToArray();
        return Array.IndexOf(array, array.Max());
    }

    private static int PredictResNet(InferenceSession? session, SKBitmap image)
    {
        var tensor = ResNetProcess.ConvertImageToTensor(image);
        return PredictModel(session, tensor);
    }

    public (int Result, string Expr, int EqualSymbol, int Operator, int Digit1, int Digit2)
        PredictValidateCode(SKBitmap originalImage)
    {
        var defaultValue = (-1, "", -1, -1, -1, -1);
        if (!IsLoaded) return defaultValue;
        if (originalImage.Width == 0 || originalImage.Height == 0) return defaultValue;

        using var image = originalImage.Copy();
        ImageUtils.BinarizeInPlace(image, CasCaptchaImage.ConfigThresh);

        using var imageEqualSymbol = CasCaptchaImage.SplitImgByRatio(
            image, CasCaptchaImage.EqualSymbolKeyStart, CasCaptchaImage.EqualSymbolKeyEnd);
        var predictedEqualSymbol = (CasCaptchaImage.CasExprEqualSymbol)
            PredictResNet(_sessionEqualSymbol, imageEqualSymbol);

        var keyPoint = predictedEqualSymbol == CasCaptchaImage.CasExprEqualSymbol.Chs
            ? CasCaptchaImage.KeyPointChs
            : CasCaptchaImage.KeyPointSymbol;

        using var imageDigit1 = CasCaptchaImage.SplitImgByRatio(image, 0, keyPoint[0]);
        using var imageOperator = CasCaptchaImage.SplitImgByRatio(image, keyPoint[0], keyPoint[1]);
        using var imageDigit2 = CasCaptchaImage.SplitImgByRatio(image, keyPoint[1], keyPoint[2]);

        var predictedOperator = (CasCaptchaImage.CasExprOperator)
            PredictResNet(_sessionOperator, imageOperator);
        var predictedDigit1 = PredictResNet(_sessionDigit, imageDigit1);
        var predictedDigit2 = PredictResNet(_sessionDigit, imageDigit2);

        var result = CasCaptchaImage.CalculateOperator(predictedDigit1, predictedDigit2, predictedOperator);
        var strOperator = CasCaptchaImage.GetOperatorString(predictedOperator);
        var expr = $"{predictedDigit1} {strOperator} {predictedDigit2} = {result}";

        return (result, expr, (int)predictedEqualSymbol, (int)predictedOperator, predictedDigit1, predictedDigit2);
    }

    public void Dispose()
    {
        _sessionEqualSymbol?.Dispose();
        _sessionOperator?.Dispose();
        _sessionDigit?.Dispose();
        _sessionEqualSymbol = null;
        _sessionOperator = null;
        _sessionDigit = null;
        _isLoaded = false;
    }
}
