using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.IO;
using System.Collections.ObjectModel;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace remename.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private ISmbService? _smbService;
    private AsyncRelayCommand? _selectFolderCommand;

    [ObservableProperty]
    private string _selectedPath = string.Empty;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _replaceText = string.Empty;

    [ObservableProperty]
    private ObservableCollection<string> _fileList = new();

    [ObservableProperty]
    private bool _isSmbMode = false;

    [ObservableProperty]
    private string _smbServer = string.Empty;

    [ObservableProperty]
    private string _smbUsername = string.Empty;

    [ObservableProperty]
    private string _smbPassword = string.Empty;

    [ObservableProperty]
    private bool _isSmbConnected = false;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public IFolderPickerService? FolderPickerService { get; set; }

    public IAsyncRelayCommand SelectFolderCommand =>
        _selectFolderCommand ??= new AsyncRelayCommand(ExecuteSelectFolderAsync);

    private AsyncRelayCommand? _renameCommand;
    public IAsyncRelayCommand RenameCommand => _renameCommand ??= new AsyncRelayCommand(ExecuteRenameAsync);

    private AsyncRelayCommand? _connectSmbCommand;
    public IAsyncRelayCommand ConnectSmbCommand =>
        _connectSmbCommand ??= new AsyncRelayCommand(ExecuteConnectSmbAsync);

    private AsyncRelayCommand? _disconnectSmbCommand;
    public IAsyncRelayCommand DisconnectSmbCommand =>
        _disconnectSmbCommand ??= new AsyncRelayCommand(ExecuteDisconnectSmbAsync);

    public MainViewModel()
    {
        _smbService = new SmbService();
    }

    private async Task ExecuteSelectFolderAsync()
    {
        if (FolderPickerService == null)
        {
            StatusMessage = "当前平台不支持选择文件夹";
            return;
        }

        try
        {
            var selectedPath = await FolderPickerService.PickFolderAsync();
            if (!string.IsNullOrWhiteSpace(selectedPath))
            {
                LoadFilesFromPath(selectedPath);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"选择文件夹失败: {ex.Message}";
        }
    }

    private async Task ExecuteConnectSmbAsync()
    {
        try
        {
            StatusMessage = "正在连接...";
            if (_smbService == null)
                _smbService = new SmbService();

            await _smbService.ConnectAsync(SmbServer, SmbUsername, SmbPassword);
            IsSmbConnected = true;
            IsSmbMode = true;
            StatusMessage = "已连接到SMB服务器";

            var initialPath = BuildInitialSmbPath();
            await LoadSmbFilesAsync(initialPath);
        }
        catch (Exception ex)
        {
            IsSmbConnected = false;
            StatusMessage = $"连接失败: {ex.Message}";
        }
    }

    private async Task ExecuteDisconnectSmbAsync()
    {
        try
        {
            if (_smbService != null)
            {
                await _smbService.DisconnectAsync();
            }
            IsSmbConnected = false;
            IsSmbMode = false;
            SelectedPath = string.Empty;
            FileList.Clear();
            StatusMessage = "已断开连接";
        }
        catch (Exception ex)
        {
            StatusMessage = $"断开连接失败: {ex.Message}";
        }
    }

    private async Task LoadSmbFilesAsync(string path)
    {
        if (_smbService == null || string.IsNullOrEmpty(path))
            return;

        try
        {
            SelectedPath = path;
            FileList.Clear();

            var files = await _smbService.GetFilesAsync(path);
            foreach (var file in files)
            {
                FileList.Add(file);
            }
            StatusMessage = $"已加载 {files.Count} 个文件";
        }
        catch (Exception ex)
        {
            StatusMessage = $"加载文件失败: {ex.Message}";
        }
    }

    private async Task ExecuteRenameAsync()
    {
        if (string.IsNullOrEmpty(SelectedPath) || string.IsNullOrEmpty(SearchText))
            return;

        try
        {
            if (IsSmbMode)
            {
                await ExecuteRenameSmbAsync();
            }
            else
            {
                await Task.Run(ExecuteRenameLocal);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"重命名失败: {ex.Message}";
        }
    }

    private string BuildInitialSmbPath()
    {
        var selectedPath = SelectedPath.Trim();
        if (LooksLikeSmbPath(selectedPath))
        {
            return NormalizeSmbPath(selectedPath);
        }

        return NormalizeSmbPath(SmbServer);
    }

    private bool LooksLikeSmbPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) ||
            path.StartsWith("content://", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (path.StartsWith("smb://", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith(@"\\", StringComparison.Ordinal))
        {
            return true;
        }

        if (path.Contains(':'))
        {
            return false;
        }

        return path.Contains('/') || path.Contains('\\');
    }

    private string NormalizeSmbPath(string path)
    {
        var normalized = path.Trim().Replace('\\', '/').Trim('/');
        var hadScheme = normalized.StartsWith("smb://", StringComparison.OrdinalIgnoreCase);
        if (hadScheme)
        {
            normalized = normalized[6..].Trim('/');
        }

        if (string.IsNullOrEmpty(normalized))
        {
            normalized = SmbServer.Trim().Replace('\\', '/').Trim('/');
        }

        var serverName = ExtractSmbServerName(SmbServer);
        var firstSegment = normalized.Split('/', 2, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (!hadScheme &&
            !string.IsNullOrEmpty(serverName) &&
            !string.Equals(firstSegment, serverName, StringComparison.OrdinalIgnoreCase))
        {
            normalized = $"{serverName}/{normalized}";
        }

        return $"smb://{normalized.TrimEnd('/')}/";
    }

    private static string ExtractSmbServerName(string server)
    {
        var normalized = server.Trim().Replace('\\', '/').Trim('/');
        if (normalized.StartsWith("smb://", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[6..].Trim('/');
        }

        return normalized.Split('/', 2, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
    }

    private void ExecuteRenameLocal()
    {
        if (SelectedPath.StartsWith("content://", StringComparison.OrdinalIgnoreCase))
        {
            StatusMessage = "Android 文档目录不能直接用本地路径重命名，请使用SMB模式";
            return;
        }

        var files = Directory.GetFiles(SelectedPath);
        int renamedCount = 0;
        foreach (var file in files)
        {
            string fileName = Path.GetFileName(file);
            string newFileName = fileName.Replace(SearchText, ReplaceText);
            
            if (newFileName != fileName)
            {
                string newPath = Path.Combine(SelectedPath, newFileName);
                File.Move(file, newPath);
                renamedCount++;
            }
        }
        StatusMessage = $"已重命名 {renamedCount} 个文件";
        LoadFilesFromPath(SelectedPath);
    }

    private async Task ExecuteRenameSmbAsync()
    {
        if (_smbService == null)
            return;

        try
        {
            var files = await _smbService.GetFilesAsync(SelectedPath);
            int renamedCount = 0;

            foreach (var file in files)
            {
                string newFileName = file.Replace(SearchText, ReplaceText);
                if (newFileName != file)
                {
                    var filePath = SelectedPath.TrimEnd('/') + "/" + file;
                    await _smbService.RenameFileAsync(filePath, newFileName);
                    renamedCount++;
                }
            }

            StatusMessage = $"已重命名 {renamedCount} 个文件";
            await LoadSmbFilesAsync(SelectedPath);
        }
        catch (Exception ex)
        {
            StatusMessage = $"重命名失败: {ex.Message}";
        }
    }

    public void LoadFilesFromPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        if (path.StartsWith("content://", StringComparison.OrdinalIgnoreCase))
        {
            SelectedPath = path;
            FileList.Clear();
            StatusMessage = "Android 文档目录不是本地路径，请使用SMB模式或输入可访问的本地路径";
            return;
        }

        SelectedPath = path;
        FileList.Clear();

        try
        {
            if (!Directory.Exists(path))
            {
                StatusMessage = "文件夹不存在或当前平台无权访问";
                return;
            }

            var files = Directory.GetFiles(path);
            foreach (var file in files)
            {
                FileList.Add(Path.GetFileName(file));
            }
            StatusMessage = $"已加载 {files.Length} 个文件";
        }
        catch (Exception ex)
        {
            StatusMessage = $"加载文件失败: {ex.Message}";
        }
    }
}

