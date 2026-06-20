using System.Collections.ObjectModel;
using System.Linq;
using shmtu.captcha.onnx.Backend;

namespace shmtu.captcha.onnx.gui.ViewModels;

/// <summary>
/// ViewModel 用于本地模型扫描和选择对话框。
/// </summary>
public sealed class LocalModelsViewModel : ObservableObject
{
    private LocalModelEntry? _selectedEntry;
    private bool _isScanning;
    private string _statusText = "";

    public LocalModelsViewModel()
    {
        ScanCommand = new RelayCommand(async () => await ScanAsync(), () => !IsScanning);
        LoadCommand = new RelayCommand(OnLoad, () => SelectedEntry != null);
        CancelCommand = new RelayCommand(OnCancel);
    }

    // ---- Commands ----
    public RelayCommand ScanCommand { get; }
    public RelayCommand LoadCommand { get; }
    public RelayCommand CancelCommand { get; }

    // ---- Properties ----

    /// <summary>扫描到的本地模型条目列表。</summary>
    public ObservableCollection<LocalModelEntry> Entries { get; } = new();

    /// <summary>当前选中的模型条目。</summary>
    public LocalModelEntry? SelectedEntry
    {
        get => _selectedEntry;
        set
        {
            if (SetField(ref _selectedEntry, value))
                RaiseCommandsChanged();
        }
    }

    public bool IsScanning
    {
        get => _isScanning;
        set
        {
            if (SetField(ref _isScanning, value))
                RaiseCommandsChanged();
        }
    }

    public string StatusText
    {
        get => _statusText;
        set => SetField(ref _statusText, value);
    }

    // ---- Selection Result ----
    public bool Applied { get; private set; }
    public bool Cancelled { get; private set; }

    /// <summary>用户选择的模型条目（Apply 后可用）。</summary>
    public LocalModelEntry? ChosenEntry { get; private set; }

    /// <summary>设置模型目录，用于扫描。</summary>
    public string ModelDirectory { get; set; } = "";

    // ---- Methods ----

    public async Task ScanAsync()
    {
        if (string.IsNullOrWhiteSpace(ModelDirectory))
        {
            StatusText = "模型目录未设置";
            return;
        }

        IsScanning = true;
        StatusText = "正在扫描本地模型...";
        try
        {
            // 模拟短暂延迟以让 UI 更新
            await Task.Delay(50);
            var results = LocalModelScanner.Scan(ModelDirectory);
            Entries.Clear();
            foreach (var entry in results)
                Entries.Add(entry);

            if (Entries.Count == 0)
            {
                StatusText = "未找到任何本地 ONNX 模型文件";
            }
            else
            {
                var v1Count = results.Count(e => e.Version == ConstValue.ModelVersion.V1);
                var v2Count = results.Count(e => e.Version == ConstValue.ModelVersion.V2);
                StatusText = $"扫描完成：共 {Entries.Count} 个模型（V1: {v1Count}, V2: {v2Count}）";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"扫描出错：{ex.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }

    private void OnLoad()
    {
        if (SelectedEntry == null) return;
        Applied = true;
        ChosenEntry = SelectedEntry;
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
        ScanCommand.RaiseCanExecuteChanged();
        LoadCommand.RaiseCanExecuteChanged();
        CancelCommand.RaiseCanExecuteChanged();
    }
}
