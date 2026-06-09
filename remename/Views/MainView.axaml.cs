using Avalonia.Controls;
using Avalonia.Platform.Storage;
using remename.ViewModels;
using System;
using CommunityToolkit.Mvvm.Input;

namespace remename.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
        
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.SelectFolderCommand = new AsyncRelayCommand(SelectFolderAsync);
        }
    }

    private async System.Threading.Tasks.Task SelectFolderAsync()
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var folderPicker = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "选择文件夹",
                AllowMultiple = false
            });

            if (folderPicker.Count == 0)
                return;

            var selectedFolder = folderPicker[0];
            var selectedPath = selectedFolder.Path.IsFile
                ? selectedFolder.Path.LocalPath
                : selectedFolder.Path.ToString();

            if (DataContext is MainViewModel viewModel)
            {
                viewModel.LoadFilesFromPath(selectedPath);
            }
        }
        catch (Exception ex)
        {
            if (DataContext is MainViewModel viewModel)
            {
                viewModel.StatusMessage = $"选择文件夹失败: {ex.Message}";
            }
        }
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.SelectFolderCommand = new AsyncRelayCommand(SelectFolderAsync);
        }
    }
}
