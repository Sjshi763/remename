using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.IO;
using System.Collections.ObjectModel;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using remename.Helpers;

namespace remename.ViewModels;

public class FileItemInfo
{
    public string Name { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTime ModifiedTime { get; set; }
    public bool IsSelected { get; set; }
    public string SizeFormatted => FormatFileSize(Size);
    public string ModifiedFormatted => ModifiedTime.ToString("yyyy-MM-dd HH:mm");

    private static string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:0.##} {sizes[order]}";
    }
}

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
    private ObservableCollection<FileItemInfo> _fileList = new();

    [ObservableProperty]
    private bool _isSmbOptionAvailable = false;

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

    [ObservableProperty]
    private string _filterText = string.Empty;

    [ObservableProperty]
    private bool _selectAll = false;

    [ObservableProperty]
    private int _selectedCount = 0;

    [ObservableProperty]
    private string _sortBy = "Name";

    [ObservableProperty]
    private bool _sortAscending = true;

    public bool IsDesktop => PlatformHelper.IsDesktop;
    public bool IsMobile => PlatformHelper.IsMobile;

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

    private AsyncRelayCommand? _refreshCommand;
    public IAsyncRelayCommand RefreshCommand =>
        _refreshCommand ??= new AsyncRelayCommand(ExecuteRefreshAsync);

    private AsyncRelayCommand? _toggleSelectAllCommand;
    public IAsyncRelayCommand ToggleSelectAllCommand =>
        _toggleSelectAllCommand ??= new AsyncRelayCommand(ExecuteToggleSelectAll);

    public MainViewModel()
    {
        _smbService = new SmbService();
        // SMB功能主要用于移动端，桌面端可以直接访问本地文件
        IsSmbOptionAvailable = Helpers.PlatformHelper.IsMobile;
    }

    partial void OnFilterTextChanged(string value)
    {
        ApplyFilter();
    }

    partial void OnSelectAllChanged(bool value)
    {
        foreach (var file in FileList)
        {
            file.IsSelected = value;
        }
        UpdateSelectedCount();
    }

    partial void OnSortByChanged(string value)
    {
        ApplySorting();
    }

    partial void OnSortAscendingChanged(bool value)
    {
        ApplySorting();
    }

    private void ApplyFilter()
    {
        // 过滤逻辑在加载文件时处理
        if (!string.IsNullOrEmpty(SelectedPath))
        {
            if (IsSmbMode && _smbService != null)
            {
                _ = LoadSmbFilesAsync(SelectedPath);
            }
            else if (Directory.Exists(SelectedPath))
            {
                LoadFilesFromPath(SelectedPath);
            }
        }
    }

    private void ApplySorting()
    {
        var sorted = SortBy switch
        {
            "Name" => SortAscending
                ? FileList.OrderBy(f => f.Name).ToList()
                : FileList.OrderByDescending(f => f.Name).ToList(),
            "Size" => SortAscending
                ? FileList.OrderBy(f => f.Size).ToList()
                : FileList.OrderByDescending(f => f.Size).ToList(),
            "Modified" => SortAscending
                ? FileList.OrderBy(f => f.ModifiedTime).ToList()
                : FileList.OrderByDescending(f => f.ModifiedTime).ToList(),
            "Extension" => SortAscending
                ? FileList.OrderBy(f => f.Extension).ToList()
                : FileList.OrderByDescending(f => f.Extension).ToList(),
            _ => FileList.ToList()
        };

        FileList.Clear();
        foreach (var item in sorted)
        {
            FileList.Add(item);
        }
    }

    private void UpdateSelectedCount()
    {
        SelectedCount = FileList.Count(f => f.IsSelected);
    }

    public void OnFileSelectionChanged()
    {
        UpdateSelectedCount();
        SelectAll = FileList.Count > 0 && FileList.All(f => f.IsSelected);
    }

    private async Task ExecuteRefreshAsync()
    {
        if (!string.IsNullOrEmpty(SelectedPath))
        {
            if (IsSmbMode)
            {
                await LoadSmbFilesAsync(SelectedPath);
            }
            else
            {
                await Task.Run(() => LoadFilesFromPath(SelectedPath));
            }
        }
    }

    private Task ExecuteToggleSelectAll()
    {
        SelectAll = !SelectAll;
        return Task.CompletedTask;
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

            // 应用过滤
            var filteredFiles = string.IsNullOrEmpty(FilterText)
                ? files
                : files.Where(f => f.Contains(FilterText, StringComparison.OrdinalIgnoreCase)).ToList();

            foreach (var file in filteredFiles)
            {
                var fileInfo = new FileItemInfo
                {
                    Name = file,
                    Extension = Path.GetExtension(file),
                    Size = 0, // SMB暂时不获取大小
                    ModifiedTime = DateTime.Now
                };
                FileList.Add(fileInfo);
            }

            ApplySorting();
            StatusMessage = $"已加载 {filteredFiles.Count} 个文件";
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

        var filesToRename = SelectedCount > 0
            ? FileList.Where(f => f.IsSelected).ToList()
            : FileList.ToList();

        int renamedCount = 0;
        foreach (var fileItem in filesToRename)
        {
            string file = Path.Combine(SelectedPath, fileItem.Name);
            string newFileName = fileItem.Name.Replace(SearchText, ReplaceText);

            if (newFileName != fileItem.Name)
            {
                string newPath = Path.Combine(SelectedPath, newFileName);
                try
                {
                    File.Move(file, newPath);
                    renamedCount++;
                }
                catch (Exception ex)
                {
                    StatusMessage = $"重命名 {fileItem.Name} 失败: {ex.Message}";
                }
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
            var filesToRename = SelectedCount > 0
                ? FileList.Where(f => f.IsSelected).ToList()
                : FileList.ToList();

            int renamedCount = 0;

            foreach (var fileItem in filesToRename)
            {
                string newFileName = fileItem.Name.Replace(SearchText, ReplaceText);
                if (newFileName != fileItem.Name)
                {
                    var filePath = SelectedPath.TrimEnd('/') + "/" + fileItem.Name;
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

            // 应用过滤
            var filteredFiles = string.IsNullOrEmpty(FilterText)
                ? files.ToList()
                : files.Where(f => Path.GetFileName(f).Contains(FilterText, StringComparison.OrdinalIgnoreCase)).ToList();

            foreach (var file in filteredFiles)
            {
                var fileInfo = new FileInfo(file);
                var fileItem = new FileItemInfo
                {
                    Name = fileInfo.Name,
                    Extension = fileInfo.Extension,
                    Size = fileInfo.Length,
                    ModifiedTime = fileInfo.LastWriteTime
                };
                FileList.Add(fileItem);
            }

            ApplySorting();
            StatusMessage = $"已加载 {filteredFiles.Count} 个文件";
        }
        catch (Exception ex)
        {
            StatusMessage = $"加载文件失败: {ex.Message}";
        }
    }
}

