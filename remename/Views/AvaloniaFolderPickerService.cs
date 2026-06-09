using Avalonia.Controls;
using Avalonia.Platform.Storage;
using remename.ViewModels;
using System.Threading.Tasks;

namespace remename.Views;

public sealed class AvaloniaFolderPickerService : IFolderPickerService
{
    private readonly Control _owner;

    public AvaloniaFolderPickerService(Control owner)
    {
        _owner = owner;
    }

    public async Task<string?> PickFolderAsync()
    {
        var topLevel = TopLevel.GetTopLevel(_owner);
        if (topLevel == null)
        {
            return null;
        }

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "选择文件夹",
            AllowMultiple = false
        });

        if (folders.Count == 0)
        {
            return null;
        }

        var path = folders[0].Path;
        return path.IsFile ? path.LocalPath : path.ToString();
    }
}
