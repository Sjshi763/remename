using System.Threading.Tasks;

namespace remename.ViewModels;

public interface IFolderPickerService
{
    Task<string?> PickFolderAsync();
}
