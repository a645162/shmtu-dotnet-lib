using Avalonia.Controls;
using shmtu.captcha.onnx.gui.ViewModels;

namespace shmtu.captcha.onnx.gui.Views;

public partial class LocalModelsDialog : Window
{
    public LocalModelsDialog()
    {
        InitializeComponent();
    }

    public LocalModelsDialog(LocalModelsViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.RequestClose += Close;
    }
}
