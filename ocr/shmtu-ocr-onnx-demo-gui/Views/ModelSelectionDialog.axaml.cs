using Avalonia.Controls;
using shmtu.captcha.onnx.gui.ViewModels;

namespace shmtu.captcha.onnx.gui.Views;

public partial class ModelSelectionDialog : Window
{
    public ModelSelectionDialog()
    {
        InitializeComponent();
    }

    public ModelSelectionDialog(ModelSelectionViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.RequestClose += Close;
    }
}
