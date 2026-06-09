using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.IO;
using System.Collections.ObjectModel;
using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace remename.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private ISmbService? _smbService;

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

    private IRelayCommand? _selectFolderCommand;
    public IRelayCommand SelectFolderCommand 
    { 
        get => _selectFolderCommand ??= new RelayCommand(ExecuteSelectFolder);
        set => _selectFolderCommand = value;
    }

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

    private void ExecuteSelectFolder()
    {
        // 这个方法需要在View中调用，因为需要访问TopLevel
        // 我们会在MainView.axaml.cs中实现具体的文件夹选择逻辑
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
            
            // 连接成功后，设置路径为服务器根目录
            SelectedPath = $"smb://{SmbServer}/";
            await LoadSmbFilesAsync(SelectedPath);
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

