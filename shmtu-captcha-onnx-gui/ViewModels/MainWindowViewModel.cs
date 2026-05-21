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
    private readonly CasOcr _ocr;
    private readonly HttpClient _http = new();

    private string _modelDirectory;
    private string _statusMessage = "Ready.";
    private string _urlInput = "";
    private float _downloadProgress;
    private bool _modelsReady;
    private bool _isBusy;
    private double _averageMs;

    public MainWindowViewModel()
    {
        _modelDirectory = AppContext.BaseDirectory;
        _ocr = new CasOcr(_modelDirectory);
        _modelsReady = _ocr.CheckModelIsExist();
        _statusMessage = _modelsReady ? "Models found." : "Models missing — click \"Check / Download Models\".";

        EnsureModelsCommand = new RelayCommand(async () => await EnsureModelsAsync(), () => !IsBusy);
        SelectFilesCommand = new RelayCommand(async () => await SelectFilesAsync(), () => !IsBusy);
        LoadUrlCommand = new RelayCommand(async () => await LoadFromUrlAsync(), () => !IsBusy && !string.IsNullOrWhiteSpace(UrlInput));
        ClearCommand = new RelayCommand(() => Items.Clear(), () => !IsBusy);
        RecognizeAllCommand = new RelayCommand(async () => await RecognizeAllAsync(), () => !IsBusy && _modelsReady && Items.Count > 0);
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
    public string UrlInput
    {
        get => _urlInput;
        set { if (SetField(ref _urlInput, value)) RaiseCommandsChanged(); }
    }
    public float DownloadProgress { get => _downloadProgress; set => SetField(ref _downloadProgress, value); }

    public bool ModelsReady
    {
        get => _modelsReady;
        set { if (SetField(ref _modelsReady, value)) RaiseCommandsChanged(); }
    }

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

    public double AverageMs { get => _averageMs; set => SetField(ref _averageMs, value); }

    public RelayCommand EnsureModelsCommand { get; }
    public RelayCommand SelectFilesCommand { get; }
    public RelayCommand LoadUrlCommand { get; }
    public RelayCommand ClearCommand { get; }
    public RelayCommand RecognizeAllCommand { get; }

    private void RaiseCommandsChanged()
    {
        EnsureModelsCommand.RaiseCanExecuteChanged();
        SelectFilesCommand.RaiseCanExecuteChanged();
        LoadUrlCommand.RaiseCanExecuteChanged();
        ClearCommand.RaiseCanExecuteChanged();
        RecognizeAllCommand.RaiseCanExecuteChanged();
    }

    private async Task EnsureModelsAsync()
    {
        IsBusy = true;
        try
        {
            StatusMessage = "Checking / downloading models...";
            DownloadProgress = 0;
            var progress = new Progress<float>(p =>
                Dispatcher.UIThread.Post(() => DownloadProgress = p));
            var ok = await _ocr.EnsureModelsAsync(progress, _http);
            ModelsReady = ok && _ocr.CheckModelIsExist();
            StatusMessage = ModelsReady ? "Models ready." : "Failed to obtain models.";
            if (ModelsReady)
            {
                _ocr.LoadModel();
                StatusMessage = "Models loaded.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static Window? GetMainWindow() =>
        (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

    private async Task SelectFilesAsync()
    {
        var window = GetMainWindow();
        if (window?.StorageProvider is null) return;

        var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select captcha image(s)",
            AllowMultiple = true,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Images") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp" } }
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
                StatusMessage = $"Failed to load file: {ex.Message}";
            }
        }
        StatusMessage = $"{Items.Count} item(s) in list.";
        RaiseCommandsChanged();
    }

    private async Task LoadFromUrlAsync()
    {
        if (string.IsNullOrWhiteSpace(UrlInput)) return;
        IsBusy = true;
        try
        {
            var bytes = await _http.GetByteArrayAsync(UrlInput);
            AddItem(UrlInput, bytes);
            StatusMessage = $"Loaded from {UrlInput}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"URL load failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
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
            // ignore preview decode failures; recognition can still run via ImageSharp
        }

        Items.Add(new CaptchaItemViewModel
        {
            Source = source,
            Preview = preview,
            RawBytes = bytes
        });
    }

    private async Task RecognizeAllAsync()
    {
        if (!_modelsReady)
        {
            StatusMessage = "Models not ready.";
            return;
        }
        IsBusy = true;
        try
        {
            if (!_ocr.IsLoaded) _ocr.LoadModel();
            await Task.Run(() =>
            {
                long total = 0;
                var count = 0;
                foreach (var item in Items.ToArray())
                {
                    if (item.RawBytes is null) continue;
                    var sw = Stopwatch.StartNew();
                    (int Result, string Expr, int EqualSymbol, int Operator, int Digit1, int Digit2) r;
                    try
                    {
                        using var ms = new MemoryStream(item.RawBytes);
                        r = _ocr.PredictValidateCode(ms);
                    }
                    catch (Exception ex)
                    {
                        Dispatcher.UIThread.Post(() => item.Status = $"Error: {ex.Message}");
                        continue;
                    }
                    sw.Stop();
                    total += sw.ElapsedMilliseconds;
                    count++;
                    Dispatcher.UIThread.Post(() =>
                    {
                        item.Expr = r.Expr;
                        item.ElapsedMs = sw.ElapsedMilliseconds;
                        item.Status = "Done";
                    });
                }
                Dispatcher.UIThread.Post(() =>
                {
                    AverageMs = count == 0 ? 0 : (double)total / count;
                    StatusMessage = $"Recognized {count} item(s), avg {AverageMs:F1} ms.";
                });
            });
        }
        finally
        {
            IsBusy = false;
        }
    }
}
