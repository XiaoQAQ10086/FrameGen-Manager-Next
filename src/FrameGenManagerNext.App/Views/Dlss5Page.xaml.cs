using System.Windows.Controls;
using FrameGenManagerNext.App.ViewModels;

namespace FrameGenManagerNext.App.Views;

public partial class Dlss5Page : UserControl
{
    public Dlss5Page(Dlss5ViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += async (_, _) => await viewModel.LoadAsync();
    }
}
