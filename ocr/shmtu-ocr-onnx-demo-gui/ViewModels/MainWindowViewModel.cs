using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using shmtu.captcha.onnx;

namespace shmtu.captcha.onnx.gui.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private const string DefaultCaptchaUrl = "https://cas.shmtu.edu.cn/cas/captcha";

    private readonly CasOcr _ocr;
    private readonly HttpClient _http = new();

    private string _modelDirectory;
    private string _statusMessage = "等待您的操作";
    private string _captchaUrl = DefaultCaptchaUrl;
    private float _downloadProgress;
    private bool _modelsReady;
    private bool _isBusy;
    private double _averageMs;
    private ConstValue.ModelVersion _selectedVersion = ConstValue.DefaultVersion;

    private Bitmap? _currentPreview;
    private byte[]? _currentBytes;
    private string _currentSource = "";
    private string _resultExpr = "（暂无识别结果）";
    private long _currentElapsedMs;

    public MainWindowViewModel()
    {
        _modelDirectory = AppContext.BaseDirectory;
        _ocr = new CasOcr(_modelDirectory, version: _selectedVersion);
        _modelsReady = _ocr.CheckModelIsExist();
        if (_modelsReady)
        {
            _ocr.LoadModel();
            _statusMessage = $"模型已加载（{_ocr.Version}），可开始识别";
        }
        else
        {
            _statusMessage = $"模型缺失（{_ocr.Version}），请先点击「检查 / 下载模型」";
        }

        EnsureModelsCommand = new RelayCommand(async () => await EnsureModelsAsync(), () => !IsBusy);
        DownloadFromCasCommand = new RelayCommand(async () => await DownloadFromCasAsync(), () => !IsBusy);
        OpenLocalCommand = new RelayCommand(async () => await OpenLocalAsync(), () => !IsBusy);
        RecognizeCommand = new RelayCommand(async () => await RecognizeCurrentAsync(),
            () => !IsBusy && _modelsReady && _currentBytes != null);
        ReleaseModelCommand = new RelayCommand(ReleaseModel, () => !IsBusy && _ocr.IsLoaded);

        AddToBatchCommand = new RelayCommand(AddCurrentToBatch,
            () => !IsBusy && _currentBytes != null);
        SelectFilesCommand = new RelayCommand(async () => await SelectFilesAsync(), () => !IsBusy);
        RecognizeAllCommand = new RelayCommand(async () => await RecognizeAllAsync(),
            () => !IsBusy && _modelsReady && Items.Count > 0);
        ClearCommand = new RelayCommand(() => { Items.Clear(); RaiseCommandsChanged(); },
            () => !IsBusy && Items.Count > 0);
    }

    public ObservableCollection<CaptchaItemViewModel> Items { get; } = new();

    public string ModelDirectory
    {
        get => _modelDirectory;
        set
        {
            if (SetField(ref _modelDirectory, value))
            {
                _ocr.ModelDirectoryPath = value;
                ModelsReady = _ocr.CheckModelIsExist();
            }
        }
    }

    public string StatusMessage { get => _statusMessage; set => SetField(ref _statusMessage, value); }

    public string CaptchaUrl
    {
        get => _captchaUrl;
        set => SetField(ref _captchaUrl, value);
    }

    public float DownloadProgress { get => _downloadProgress; set => SetField(ref _downloadProgress, value); }

    public bool ModelsReady
    {
        get => _modelsReady;
        set
        {
            if (SetField(ref _modelsReady, value))
            {
                OnPropertyChanged(nameof(ModelStatusText));
                RaiseCommandsChanged();
            }
        }
    }

    public string ModelStatusText => _modelsReady ? "模型已就绪" : "模型未就绪";

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetField(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(IsNotBusy));
                RaiseCommandsChanged();
            }
        }
    }

    public bool IsNotBusy => !IsBusy;

    /// <summary>当前选中的模型版本（变更后必须重建 CasOcr 才能生效）。</summary>
    public ConstValue.ModelVersion SelectedVersion
    {
        get => _selectedVersion;
        set
        {
            if (!SetField(ref _selectedVersion, value)) return;
            StatusMessage = $"已切换模型版本为 {value}（下次启动或重建客户端时生效）";
        }
    }

    /// <summary>ComboBox 数据源：所有可用模型版本。</summary>
    public IReadOnlyList<ConstValue.ModelVersion> AvailableVersions { get; } =
        new[] { ConstValue.ModelVersion.V1, ConstValue.ModelVersion.V2 };

    public Bitmap? CurrentPreview
    {
        get => _currentPreview;
        set => SetField(ref _currentPreview, value);
    }

    public string CurrentSource
    {
        get => _currentSource;
        set => SetField(ref _currentSource, value);
    }

    public string ResultExpr
    {
        get => _resultExpr;
        set => SetField(ref _resultExpr, value);
    }

    public long CurrentElapsedMs
    {
        get => _currentElapsedMs;
        set => SetField(ref _currentElapsedMs, value);
    }

    public double AverageMs { get => _averageMs; set => SetField(ref _averageMs, value); }

    public RelayCommand EnsureModelsCommand { get; }
    public RelayCommand DownloadFromCasCommand { get; }
    public RelayCommand OpenLocalCommand { get; }
    public RelayCommand RecognizeCommand { get; }
    public RelayCommand ReleaseModelCommand { get; }

    public RelayCommand AddToBatchCommand { get; }
    public RelayCommand SelectFilesCommand { get; }
    public RelayCommand RecognizeAllCommand { get; }
    public RelayCommand ClearCommand { get; }

    private void RaiseCommandsChanged()
    {
        EnsureModelsCommand.RaiseCanExecuteChanged();
        DownloadFromCasCommand.RaiseCanExecuteChanged();
        OpenLocalCommand.RaiseCanExecuteChanged();
        RecognizeCommand.RaiseCanExecuteChanged();
        ReleaseModelCommand.RaiseCanExecuteChanged();
        AddToBatchCommand.RaiseCanExecuteChanged();
        SelectFilesCommand.RaiseCanExecuteChanged();
        RecognizeAllCommand.RaiseCanExecuteChanged();
        ClearCommand.RaiseCanExecuteChanged();
    }

    private async Task EnsureModelsAsync()
    {
        IsBusy = true;
        try
        {
            StatusMessage = "正在检查 / 下载模型...";
            DownloadProgress = 0;
            var progress = new Progress<float>(p =>
                Dispatcher.UIThread.Post(() => DownloadProgress = p));
            var ok = await _ocr.EnsureModelsAsync(progress, _http);
            ModelsReady = ok && _ocr.CheckModelIsExist();
            if (ModelsReady)
            {
                _ocr.LoadModel();
                StatusMessage = $"模型已加载（{_ocr.Version}），可开始识别";
            }
            else
            {
                StatusMessage = "模型获取失败，请检查网络后重试";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"错误：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ReleaseModel()
    {
        _ocr.Dispose();
        ModelsReady = _ocr.CheckModelIsExist();
        StatusMessage = "已释放模型（如需再次识别请点击「检查 / 下载模型」重新加载）";
    }

    private static Window? GetMainWindow() =>
        (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

    private async Task DownloadFromCasAsync()
    {
        IsBusy = true;
        try
        {
            StatusMessage = $"正在下载验证码：{CaptchaUrl}";
            var bytes = await _http.GetByteArrayAsync(CaptchaUrl);
            SetCurrent(CaptchaUrl, bytes);
            StatusMessage = "验证码下载完成，点击「OCR 识别」开始识别";
        }
        catch (Exception ex)
        {
            StatusMessage = $"下载失败：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task OpenLocalAsync()
    {
        var window = GetMainWindow();
        if (window?.StorageProvider is null) return;

        var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择验证码图片",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("图片文件") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp" } }
            }
        });

        var file = files.FirstOrDefault();
        if (file is null) return;

        try
        {
            var path = file.TryGetLocalPath() ?? file.Name;
            var bytes = await File.ReadAllBytesAsync(path);
            SetCurrent(path, bytes);
            StatusMessage = $"已加载本地图片：{Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"加载失败：{ex.Message}";
        }
    }

    private void SetCurrent(string source, byte[] bytes)
    {
        _currentBytes = bytes;
        CurrentSource = source;
        try
        {
            using var ms = new MemoryStream(bytes);
            CurrentPreview = new Bitmap(ms);
        }
        catch
        {
            CurrentPreview = null;
        }
        ResultExpr = "（已加载图片，点击「OCR 识别」开始识别）";
        CurrentElapsedMs = 0;
        RaiseCommandsChanged();
    }

    private async Task RecognizeCurrentAsync()
    {
        if (_currentBytes is null) return;
        IsBusy = true;
        try
        {
            if (!_ocr.IsLoaded) _ocr.LoadModel();
            StatusMessage = "正在识别...";
            var bytes = _currentBytes;
            var (expr, ms) = await Task.Run(() =>
            {
                var sw = Stopwatch.StartNew();
                var r = _ocr.PredictValidateCode(bytes);
                sw.Stop();
                return (r.Expr, sw.ElapsedMilliseconds);
            });
            ResultExpr = string.IsNullOrWhiteSpace(expr) ? "（识别失败）" : expr;
            CurrentElapsedMs = ms;
            StatusMessage = $"识别完成，用时 {ms} 毫秒";
        }
        catch (Exception ex)
        {
            StatusMessage = $"识别出错：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void AddCurrentToBatch()
    {
        if (_currentBytes is null) return;
        AddItem(CurrentSource, _currentBytes);
        StatusMessage = $"已添加到批量列表，共 {Items.Count} 项";
        RaiseCommandsChanged();
    }

    private async Task SelectFilesAsync()
    {
        var window = GetMainWindow();
        if (window?.StorageProvider is null) return;

        var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择一张或多张验证码图片",
            AllowMultiple = true,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("图片文件") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp" } }
            }
        });

        foreach (var f in files)
        {
            try
            {
                var path = f.TryGetLocalPath();
                if (path is null) continue;
                var bytes = await File.ReadAllBytesAsync(path);
                AddItem(path, bytes);
            }
            catch (Exception ex)
            {
                StatusMessage = $"加载文件失败：{ex.Message}";
            }
        }
        StatusMessage = $"批量列表共 {Items.Count} 项";
        RaiseCommandsChanged();
    }

    private void AddItem(string source, byte[] bytes)
    {
        Bitmap? preview = null;
        try
        {
            using var ms = new MemoryStream(bytes);
            preview = new Bitmap(ms);
        }
        catch
        {
            // ignore decode failure
        }
        Items.Add(new CaptchaItemViewModel
        {
            Source = source,
            Preview = preview,
            RawBytes = bytes,
            Status = "待识别"
        });
    }

    private async Task RecognizeAllAsync()
    {
        if (!_modelsReady)
        {
            StatusMessage = "模型未就绪";
            return;
        }
        IsBusy = true;
        try
        {
            if (!_ocr.IsLoaded) _ocr.LoadModel();
            StatusMessage = $"正在批量识别 {Items.Count} 项...";
            await Task.Run(() =>
            {
                long total = 0;
                var count = 0;
                foreach (var item in Items.ToArray())
                {
                    if (item.RawBytes is null) continue;
                    Dispatcher.UIThread.Post(() => item.Status = "识别中…");
                    var sw = Stopwatch.StartNew();
                    string expr;
                    try
                    {
                        var r = _ocr.PredictValidateCode(item.RawBytes);
                        expr = r.Expr;
                    }
                    catch (Exception ex)
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            item.Status = "失败";
                            item.Expr = ex.Message;
                        });
                        continue;
                    }
                    sw.Stop();
                    total += sw.ElapsedMilliseconds;
                    count++;
                    var ms = sw.ElapsedMilliseconds;
                    Dispatcher.UIThread.Post(() =>
                    {
                        item.Expr = expr;
                        item.ElapsedMs = ms;
                        item.Status = "完成";
                    });
                }
                Dispatcher.UIThread.Post(() =>
                {
                    AverageMs = count == 0 ? 0 : (double)total / count;
                    StatusMessage = $"批量识别完成 {count} 项，平均用时 {AverageMs:F1} 毫秒";
                });
            });
        }
        finally
        {
            IsBusy = false;
        }
    }
}
