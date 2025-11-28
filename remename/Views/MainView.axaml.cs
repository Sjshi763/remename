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
        
        // 订阅按钮点击事件来处理文件夹选择
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.SelectFolderCommand = new AsyncRelayCommand(SelectFolderAsync);
        }
    }

    private async System.Threading.Tasks.Task SelectFolderAsync()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var folderPicker = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "选择文件夹",
            AllowMultiple = false
        });

        if (folderPicker.Count > 0)
        {
            var selectedFolder = folderPicker[0].Path.LocalPath;
            if (DataContext is MainViewModel viewModel)
            {
                viewModel.LoadFilesFromPath(selectedFolder);
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
