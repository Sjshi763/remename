using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using remename.ViewModels;
using System;
using System.IO;
using System.Linq;

namespace remename.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();

        DragDrop.SetAllowDrop(LayoutRoot, true);
        DragDrop.AddDragOverHandler(LayoutRoot, OnDragOver);
        DragDrop.AddDropHandler(LayoutRoot, OnDrop);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.FolderPickerService = new AvaloniaFolderPickerService(this);
        }
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = TryGetDroppedFolderPath(e, out _)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        e.Handled = true;

        if (DataContext is not MainViewModel viewModel)
            return;

        if (viewModel.IsSmbMode)
        {
            viewModel.StatusMessage = "请先关闭SMB模式，再拖入本地文件夹";
            return;
        }

        if (!TryGetDroppedFolderPath(e, out var folderPath))
        {
            viewModel.StatusMessage = "请拖入一个本地文件夹";
            return;
        }

        viewModel.LoadFilesFromPath(folderPath);
    }

    private static bool TryGetDroppedFolderPath(DragEventArgs e, out string folderPath)
    {
        folderPath = string.Empty;

        var folder = e.DataTransfer.TryGetFiles()?.OfType<IStorageFolder>().FirstOrDefault();
        if (folder == null)
            return false;

        var path = folder.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            return false;

        folderPath = path;
        return true;
    }
}
