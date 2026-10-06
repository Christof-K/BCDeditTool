using System.Windows;
using BcdEditTool.ViewModels.BootManager;
using Microsoft.Win32;

namespace BcdEditTool.Views.BootManager;

/// <summary>
/// Code-behind for the MainWindow view.
/// </summary>
public sealed partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        _viewModel = viewModel;
        DataContext = _viewModel;

        InitializeComponent();

        Loaded += OnWindowLoaded;
    }

    private async void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.InitializeAsync();
    }

    private async void OnBackupBcdClick(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Save BCD Store Backup",
            Filter = "BCD Backup File (*.bcd)|*.bcd|All Files (*.*)|*.*",
            FileName = $"BcdBackup_{DateTimeOffset.Now:yyyyMMdd_HHmmss}.bcd",
            DefaultExt = ".bcd"
        };

        if (dialog.ShowDialog(this) == true)
        {
            await _viewModel.ExportBackupAsync(dialog.FileName);
        }
    }
}
