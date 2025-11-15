using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.IO;

namespace remename.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _greeting = "Welcome to Avalonia!";

    [ObservableProperty]
    private string _selectedPath = string.Empty;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _replaceText = string.Empty;

    private RelayCommand _renameCommand;
    public IRelayCommand RenameCommand => _renameCommand ??= new RelayCommand(ExecuteRename);

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
}
