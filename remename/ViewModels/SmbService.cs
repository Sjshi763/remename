using SharpCifs.Smb;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace remename;

public interface ISmbService
{
    Task ConnectAsync(string server, string username, string password);
    Task DisconnectAsync();
    Task<List<string>> GetFoldersAsync(string path);
    Task<List<string>> GetFilesAsync(string path);
    Task<bool> IsConnectedAsync();
    Task RenameFileAsync(string filePath, string newFileName);
}

public class SmbService : ISmbService
{
    private SmbFile? _rootShare;
    private bool _isConnected;

    public Task<bool> IsConnectedAsync()
    {
        return Task.FromResult(_isConnected);
    }

    public async Task ConnectAsync(string server, string username, string password)
    {
        try
        {
            var auth = new NtlmPasswordAuthentication(null, username, password);
            var smbUrl = $"smb://{server}/";
            _rootShare = new SmbFile(smbUrl, auth);
            
            // Test connection by listing shares
            var shares = _rootShare.ListFiles();
            _isConnected = true;
        }
        catch (Exception ex)
        {
            _isConnected = false;
            throw new Exception($"Failed to connect to SMB server: {ex.Message}", ex);
        }
    }

    public Task DisconnectAsync()
    {
        try
        {
            _rootShare = null;
            _isConnected = false;
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to disconnect: {ex.Message}", ex);
        }
    }

    public async Task<List<string>> GetFoldersAsync(string path)
    {
        var folders = new List<string>();

        try
        {
            if (_rootShare == null)
                throw new InvalidOperationException("Not connected to SMB server");

            var share = new SmbFile(_rootShare, path.EndsWith("/") ? path : path + "/");
            var files = share.ListFiles();

            foreach (var file in files)
            {
                if (file.IsDirectory() && !file.GetName().EndsWith("$/"))
                {
                    folders.Add(file.GetName().TrimEnd('/'));
                }
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to get folders: {ex.Message}", ex);
        }

        return folders;
    }

    public async Task<List<string>> GetFilesAsync(string path)
    {
        var files = new List<string>();

        try
        {
            if (_rootShare == null)
                throw new InvalidOperationException("Not connected to SMB server");

            var share = new SmbFile(_rootShare, path.EndsWith("/") ? path : path + "/");
            var smbFiles = share.ListFiles();

            foreach (var file in smbFiles)
            {
                if (!file.IsDirectory())
                {
                    files.Add(file.GetName());
                }
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to get files: {ex.Message}", ex);
        }

        return files;
    }

    public async Task RenameFileAsync(string filePath, string newFileName)
    {
        try
        {
            if (_rootShare == null)
                throw new InvalidOperationException("Not connected to SMB server");

            var file = new SmbFile(_rootShare, filePath);
            var newPath = Path.Combine(Path.GetDirectoryName(filePath) ?? "", newFileName)
                .Replace("\\", "/");
            var newFile = new SmbFile(_rootShare, newPath);

            file.Renameto(newFile);
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to rename file: {ex.Message}", ex);
        }
    }
}
