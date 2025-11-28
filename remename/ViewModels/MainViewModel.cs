using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.IO;
using System.Collections.ObjectModel;
using System;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace remename.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _selectedPath = string.Empty;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _replaceText = string.Empty;

    [ObservableProperty]
    private ObservableCollection<string> _fileList = new();

    private IRelayCommand? _selectFolderCommand;
    public IRelayCommand SelectFolderCommand 
    { 
        get => _selectFolderCommand ??= new RelayCommand(ExecuteSelectFolder);
        set => _selectFolderCommand = value;
    }

    private RelayCommand? _renameCommand;
    public IRelayCommand RenameCommand => _renameCommand ??= new RelayCommand(ExecuteRename);

    private void ExecuteSelectFolder()
    {
        // 这个方法需要在View中调用，因为需要访问TopLevel
        // 我们会在MainView.axaml.cs中实现具体的文件夹选择逻辑
    }

    private void ExecuteRename()
    {
        if (string.IsNullOrEmpty(SelectedPath) || string.IsNullOrEmpty(SearchText))
            return;

        var files = Directory.GetFiles(SelectedPath);
        foreach (var file in files)
        {
            string fileName = Path.GetFileName(file);
            string newFileName = fileName.Replace(SearchText, ReplaceText);
            string newPath = Path.Combine(SelectedPath, newFileName);

            File.Move(file, newPath);
        }
    }

    public void LoadFilesFromPath(string path)
    {
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            return;

        SelectedPath = path;
        FileList.Clear();

        try
        {
            var files = Directory.GetFiles(path);
            foreach (var file in files)
            {
                FileList.Add(Path.GetFileName(file));
            }
        }
        catch (Exception ex)
        {
            // 处理访问权限等异常
            System.Diagnostics.Debug.WriteLine($"Error loading files: {ex.Message}");
        }
    }
}
