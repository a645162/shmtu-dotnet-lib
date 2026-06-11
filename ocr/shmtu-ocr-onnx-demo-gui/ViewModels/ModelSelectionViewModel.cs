using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using shmtu.captcha.onnx;
using shmtu.captcha.onnx.Backend;

namespace shmtu.captcha.onnx.gui.ViewModels;

public sealed class ModelSelectionViewModel : ObservableObject
{
    private readonly HttpClient _http = new();

    private string _selectedTag = "";
    private ModelInfo? _selectedModel;
    private string _selectedPrecision = "fp16";
    private bool _isLoading;
    private string _statusText = "";
    private float _downloadProgress;
    private bool _isDownloading;
    private string _downloadStatus = "";

    public ModelSelectionViewModel()
    {
        ApplyCommand = new RelayCommand(OnApply, () => SelectedModel != null && !string.IsNullOrWhiteSpace(SelectedTag));
        CancelCommand = new RelayCommand(OnCancel);
        RefreshTagsCommand = new RelayCommand(async () => await LoadTagsAsync(), () => !IsLoading);
        DownloadModelCommand = new RelayCommand(async () => await DownloadModelAsync(),
            () => SelectedModel != null && !string.IsNullOrWhiteSpace(SelectedTag) && !IsDownloading);
    }

    // ---- Commands ----
    public RelayCommand ApplyCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand RefreshTagsCommand { get; }
    public RelayCommand DownloadModelCommand { get; }

    // ---- Properties ----

    public ObservableCollection<string> Tags { get; } = new();

    public string SelectedTag
    {
        get => _selectedTag;
        set
        {
            if (SetField(ref _selectedTag, value))
            {
                _ = LoadModelsForTagAsync(value);
                RaiseCommandsChanged();
            }
        }
    }

    public ObservableCollection<ModelInfo> Models { get; } = new();

    public ModelInfo? SelectedModel
    {
        get => _selectedModel;
        set
        {
            if (SetField(ref _selectedModel, value))
            {
                OnPropertyChanged(nameof(SelectedModelDisplay));
                OnPropertyChanged(nameof(SelectedModelMetrics));
                RaiseCommandsChanged();
            }
        }
    }

    public string SelectedModelDisplay =>
        SelectedModel == null
            ? "未选择"
            : $"{SelectedModel.DisplayName} ({SelectedModel.Backbone})";

    public string SelectedModelMetrics
    {
        get
        {
            if (SelectedModel?.Metrics == null) return "";
            var parts = new List<string>();
            if (SelectedModel.Metrics.TestAccExpression.HasValue)
                parts.Add($"Test Acc: {SelectedModel.Metrics.TestAccExpression.Value:P1}");
            if (SelectedModel.Metrics.ValAccExpression.HasValue)
                parts.Add($"Val Acc: {SelectedModel.Metrics.ValAccExpression.Value:P1}");
            return string.Join(" | ", parts);
        }
    }

    public ObservableCollection<string> Precisions { get; } =
        new() { "fp16", "fp32" };

    public string SelectedPrecision
    {
        get => _selectedPrecision;
        set => SetField(ref _selectedPrecision, value);
    }

    public bool IsLoading { get => _isLoading; set => SetField(ref _isLoading, value); }

    public string StatusText { get => _statusText; set => SetField(ref _statusText, value); }

    public float DownloadProgress
    {
        get => _downloadProgress;
        set => SetField(ref _downloadProgress, value);
    }

    public bool IsDownloading
    {
        get => _isDownloading;
        set
        {
            if (SetField(ref _isDownloading, value))
                RaiseCommandsChanged();
        }
    }

    public string DownloadStatus
    {
        get => _downloadStatus;
        set => SetField(ref _downloadStatus, value);
    }

    // ---- Selection Result (set after Apply) ----
    public bool Applied { get; private set; }
    public bool Cancelled { get; private set; }

    // ---- Exposed via parent after Apply ----
    public string? AppliedTag => SelectedTag;
    public ModelInfo? AppliedModel => SelectedModel;
    public string? AppliedPrecision => SelectedPrecision;

    // ---- Methods ----

    public async Task LoadTagsAsync()
    {
        IsLoading = true;
        StatusText = "正在获取 release tags...";
        try
        {
            Tags.Clear();
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("shmtu-cas-ocr-dotnet/1.0");
            _http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

            var url = $"{ConstValue.V2.GithubReleasesApi}?per_page=100";
            var json = await _http.GetStringAsync(url);

            using var doc = JsonDocument.Parse(json);
            foreach (var rel in doc.RootElement.EnumerateArray())
            {
                if (rel.TryGetProperty("draft", out var d) && d.GetBoolean()) continue;
                if (rel.TryGetProperty("prerelease", out var p) && p.GetBoolean()) continue;
                if (!rel.TryGetProperty("tag_name", out var t)) continue;

                var tag = t.GetString() ?? "";
                if (!tag.StartsWith("v2.", StringComparison.OrdinalIgnoreCase)) continue;
                Tags.Add(tag);
            }

            if (Tags.Count > 0)
            {
                var latest = V2Downloader.ResolveLatestTagAsync(
                    ConstValue.V2.MaxSupportedMajor,
                    ConstValue.V2.MaxSupportedMinor,
                    ConstValue.V2.DefaultTag,
                    _http).GetAwaiter().GetResult();

                SelectedTag = Tags.Contains(latest) ? latest : Tags[0];
            }

            StatusText = Tags.Count > 0
                ? $"已加载 {Tags.Count} 个 release tags"
                : "未找到 v2.x 的 release tags";
        }
        catch (Exception ex)
        {
            StatusText = $"加载 tags 失败: {ex.Message}";
            // Fallback: add default tag
            if (Tags.Count == 0)
            {
                Tags.Add(ConstValue.V2.DefaultTag);
                SelectedTag = ConstValue.V2.DefaultTag;
                await LoadModelsForTagAsync(ConstValue.V2.DefaultTag);
            }
        }
        finally { IsLoading = false; }
    }

    private async Task LoadModelsForTagAsync(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return;
        IsLoading = true;
        StatusText = $"正在获取 {tag} 的模型列表...";
        try
        {
            Models.Clear();
            var list = await CasOnnxBackendV2.ListAvailableModelsAsync(tag, _http);
            foreach (var m in list)
            {
                if (m.Artifacts?.ContainsKey("onnx") == true &&
                    m.Artifacts["onnx"].ContainsKey(SelectedPrecision))
                {
                    Models.Add(m);
                }
            }

            if (Models.Count > 0)
            {
                var dflt = Models.FirstOrDefault(m =>
                    string.Equals(m.Backbone, ConstValue.V2.DefaultBackbone, StringComparison.OrdinalIgnoreCase));
                SelectedModel = dflt ?? Models[0];
            }

            StatusText = Models.Count > 0
                ? $"已加载 {Models.Count} 个 ONNX 模型"
                : "此 tag 下未找到 ONNX 模型";
        }
        catch (Exception ex)
        {
            StatusText = $"加载模型列表失败: {ex.Message}";
        }
        finally { IsLoading = false; }
    }

    private async Task DownloadModelAsync()
    {
        if (SelectedModel == null || string.IsNullOrWhiteSpace(SelectedTag)) return;
        IsDownloading = true;
        DownloadStatus = "正在下载...";
        DownloadProgress = 0;
        try
        {
            var destDir = AppContext.BaseDirectory;
            var backbone = SelectedModel.Backbone;
            var precision = SelectedPrecision;
            var tag = SelectedTag;
            var assetStem = SelectedModel.AssetStem;

            bool ok;
            if (!string.IsNullOrWhiteSpace(assetStem) && assetStem != "unknown")
            {
                ok = await V2Downloader.DownloadAsync(
                    destDir, tag, backbone, precision,
                    new Progress<float>(p =>
                    {
                        DownloadProgress = p;
                        DownloadStatus = $"下载中... {p:F0}%";
                    }),
                    _http,
                    msg => DownloadStatus = msg,
                    assetStem: assetStem);
            }
            else
            {
                ok = await V2Downloader.DownloadAsync(
                    destDir, tag, backbone, precision,
                    new Progress<float>(p =>
                    {
                        DownloadProgress = p;
                        DownloadStatus = $"下载中... {p:F0}%";
                    }),
                    _http,
                    msg => DownloadStatus = msg);
            }

            DownloadStatus = ok ? "下载完成" : "下载失败";
            DownloadProgress = ok ? 100 : 0;
        }
        catch (Exception ex)
        {
            DownloadStatus = $"下载出错: {ex.Message}";
        }
        finally { IsDownloading = false; }
    }

    private void OnApply()
    {
        Applied = true;
        RequestClose?.Invoke();
    }

    private void OnCancel()
    {
        Cancelled = true;
        RequestClose?.Invoke();
    }

    public event Action? RequestClose;

    private void RaiseCommandsChanged()
    {
        ApplyCommand.RaiseCanExecuteChanged();
        CancelCommand.RaiseCanExecuteChanged();
        RefreshTagsCommand.RaiseCanExecuteChanged();
        DownloadModelCommand.RaiseCanExecuteChanged();
    }
}
