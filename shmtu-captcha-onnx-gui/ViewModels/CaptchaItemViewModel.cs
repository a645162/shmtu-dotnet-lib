using Avalonia.Media.Imaging;

namespace shmtu.captcha.onnx.gui.ViewModels;

public sealed class CaptchaItemViewModel : ObservableObject
{
    private string _source = "";
    private Bitmap? _preview;
    private string _expr = "";
    private long _elapsedMs;
    private string _status = "待识别";

    public string Source { get => _source; set => SetField(ref _source, value); }
    public Bitmap? Preview { get => _preview; set => SetField(ref _preview, value); }
    public string Expr { get => _expr; set => SetField(ref _expr, value); }
    public long ElapsedMs { get => _elapsedMs; set => SetField(ref _elapsedMs, value); }
    public string Status { get => _status; set => SetField(ref _status, value); }

    public byte[]? RawBytes { get; init; }
}
