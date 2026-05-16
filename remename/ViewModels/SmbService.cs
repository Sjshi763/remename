using SMBLibrary;
using SMBLibrary.Client;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using FileAttributes = SMBLibrary.FileAttributes;

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
    private SMB2Client? _client;
    private bool _isConnected;
    private string _server = string.Empty;
    private string _domain = string.Empty;
    private string _username = string.Empty;
    private string _password = string.Empty;

    public Task<bool> IsConnectedAsync()
    {
        return Task.FromResult(_isConnected);
    }

    public Task ConnectAsync(string server, string username, string password)
    {
        return Task.Run(() =>
        {
            try
            {
                Disconnect();

                _server = ExtractServerName(server);
                (_domain, _username) = SplitDomainAndUsername(username);
                _password = password;

                _client = new SMB2Client();
                if (!ConnectClient(_client, _server))
                {
                    throw new InvalidOperationException($"Unable to connect to SMB server '{_server}' on TCP port 445.");
                }

                var status = _client.Login(_domain, _username, _password);
                EnsureSuccess(status, "authenticate to SMB server");

                // Test the authenticated session by enumerating shares.
                _client.ListShares(out status);
                EnsureSuccess(status, "list SMB shares");

                _isConnected = true;
            }
            catch (Exception ex)
            {
                Disconnect();
                throw new Exception($"Failed to connect to SMB server: {ex.Message}", ex);
            }
        });
    }

    public Task DisconnectAsync()
    {
        Disconnect();
        return Task.CompletedTask;
    }

    public Task<List<string>> GetFoldersAsync(string path)
    {
        return Task.Run(() =>
        {
            try
            {
                EnsureConnected();
                var smbPath = ParsePath(path);

                if (string.IsNullOrEmpty(smbPath.ShareName))
                {
                    return ListShares();
                }

                return ListDirectory(smbPath)
                    .Where(entry => entry.IsDirectory && !entry.Name.EndsWith('$'))
                    .Select(entry => entry.Name)
                    .ToList();
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get folders: {ex.Message}", ex);
            }
        });
    }

    public Task<List<string>> GetFilesAsync(string path)
    {
        return Task.Run(() =>
        {
            try
            {
                EnsureConnected();
                var smbPath = ParsePath(path);

                if (string.IsNullOrEmpty(smbPath.ShareName))
                {
                    return new List<string>();
                }

                return ListDirectory(smbPath)
                    .Where(entry => !entry.IsDirectory)
                    .Select(entry => entry.Name)
                    .ToList();
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get files: {ex.Message}", ex);
            }
        });
    }

    public Task RenameFileAsync(string filePath, string newFileName)
    {
        return Task.Run(() =>
        {
            try
            {
                EnsureConnected();

                if (newFileName.IndexOfAny(new[] { '/', '\\' }) >= 0)
                {
                    throw new ArgumentException("The new file name must not contain path separators.", nameof(newFileName));
                }

                var smbPath = ParsePath(filePath);
                if (string.IsNullOrEmpty(smbPath.ShareName) || string.IsNullOrEmpty(smbPath.RelativePath))
                {
                    throw new ArgumentException("A file inside an SMB share must be selected before renaming.", nameof(filePath));
                }

                var newRelativePath = CombineRemotePath(Path.GetDirectoryName(smbPath.RelativePath), newFileName);
                WithFileStore(smbPath.ShareName, fileStore =>
                {
                    var status = fileStore.CreateFile(
                        out var fileHandle,
                        out _,
                        ToRemotePath(smbPath.RelativePath),
                        AccessMask.GENERIC_READ | AccessMask.DELETE | AccessMask.SYNCHRONIZE,
                        FileAttributes.Normal,
                        ShareAccess.Read,
                        CreateDisposition.FILE_OPEN,
                        CreateOptions.FILE_NON_DIRECTORY_FILE | CreateOptions.FILE_SYNCHRONOUS_IO_ALERT,
                        null);
                    EnsureSuccess(status, $"open SMB file '{smbPath.RelativePath}'");

                    try
                    {
                        status = fileStore.SetFileInformation(fileHandle, new FileRenameInformationType2
                        {
                            FileName = ToRemotePath(newRelativePath),
                            ReplaceIfExists = false,
                        });
                        EnsureSuccess(status, $"rename SMB file '{smbPath.RelativePath}'");
                    }
                    finally
                    {
                        fileStore.CloseFile(fileHandle);
                    }
                });
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to rename file: {ex.Message}", ex);
            }
        });
    }

    private static string ExtractServerName(string server)
    {
        if (string.IsNullOrWhiteSpace(server))
        {
            throw new ArgumentException("SMB server address is required.", nameof(server));
        }

        var trimmed = server.Trim();
        if (trimmed.StartsWith("smb://", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[6..];
        }

        var serverAndPath = trimmed.Replace('\\', '/').Trim('/');
        var slashIndex = serverAndPath.IndexOf('/');
        return slashIndex >= 0 ? serverAndPath[..slashIndex] : serverAndPath;
    }

    private static (string Domain, string Username) SplitDomainAndUsername(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return (string.Empty, string.Empty);
        }

        var slashIndex = username.IndexOf('\\');
        if (slashIndex > 0)
        {
            return (username[..slashIndex], username[(slashIndex + 1)..]);
        }

        var atIndex = username.IndexOf('@');
        if (atIndex > 0)
        {
            return (username[(atIndex + 1)..], username[..atIndex]);
        }

        return (string.Empty, username);
    }

    private static bool ConnectClient(SMB2Client client, string server)
    {
        if (IPAddress.TryParse(server, out var address))
        {
            return client.Connect(address, SMBTransportType.DirectTCPTransport);
        }

        foreach (var hostAddress in Dns.GetHostAddresses(server))
        {
            if (client.Connect(hostAddress, SMBTransportType.DirectTCPTransport))
            {
                return true;
            }
        }

        return false;
    }

    private List<string> ListShares()
    {
        EnsureConnected();
        var shares = _client!.ListShares(out var status);
        EnsureSuccess(status, "list SMB shares");
        return shares.Where(share => !share.EndsWith('$')).ToList();
    }

    private List<SmbDirectoryEntry> ListDirectory(SmbPath smbPath)
    {
        return WithFileStore(smbPath.ShareName, fileStore =>
        {
            var remotePath = ToRemotePath(smbPath.RelativePath);
            var status = fileStore.CreateFile(
                out var directoryHandle,
                out _,
                remotePath,
                AccessMask.GENERIC_READ,
                FileAttributes.Directory,
                ShareAccess.Read | ShareAccess.Write,
                CreateDisposition.FILE_OPEN,
                CreateOptions.FILE_DIRECTORY_FILE,
                null);
            EnsureSuccess(status, $"open SMB directory '{remotePath}'");

            try
            {
                status = fileStore.QueryDirectory(
                    out var entries,
                    directoryHandle,
                    "*",
                    FileInformationClass.FileDirectoryInformation);
                if (status != NTStatus.STATUS_SUCCESS && status != NTStatus.STATUS_NO_MORE_FILES)
                {
                    EnsureSuccess(status, $"list SMB directory '{remotePath}'");
                }

                return (entries ?? new List<QueryDirectoryFileInformation>())
                    .OfType<FileDirectoryInformation>()
                    .Where(entry => entry.FileName is not "." and not "..")
                    .Select(entry => new SmbDirectoryEntry(
                        entry.FileName,
                        entry.FileAttributes.HasFlag(FileAttributes.Directory)))
                    .ToList();
            }
            finally
            {
                fileStore.CloseFile(directoryHandle);
            }
        });
    }

    private void WithFileStore(string shareName, Action<ISMBFileStore> action)
    {
        WithFileStore<object?>(shareName, fileStore =>
        {
            action(fileStore);
            return null;
        });
    }

    private T WithFileStore<T>(string shareName, Func<ISMBFileStore, T> action)
    {
        EnsureConnected();
        var fileStore = _client!.TreeConnect(shareName, out var status);
        EnsureSuccess(status, $"connect to SMB share '{shareName}'");

        try
        {
            return action(fileStore);
        }
        finally
        {
            fileStore.Disconnect();
        }
    }

    private SmbPath ParsePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return new SmbPath(string.Empty, string.Empty);
        }

        var normalized = path.Trim();
        if (normalized.StartsWith("smb://", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[6..];
        }

        normalized = normalized.Replace('\\', '/').Trim('/');
        if (string.IsNullOrEmpty(normalized))
        {
            return new SmbPath(string.Empty, string.Empty);
        }

        var parts = normalized.Split('/', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1)
        {
            if (string.Equals(parts[0], _server, StringComparison.OrdinalIgnoreCase))
            {
                return new SmbPath(string.Empty, string.Empty);
            }

            return new SmbPath(parts[0], string.Empty);
        }

        if (string.Equals(parts[0], _server, StringComparison.OrdinalIgnoreCase))
        {
            var shareAndPath = parts[1].Split('/', 2, StringSplitOptions.RemoveEmptyEntries);
            return new SmbPath(shareAndPath[0], shareAndPath.Length > 1 ? shareAndPath[1] : string.Empty);
        }

        return new SmbPath(parts[0], parts[1]);
    }

    private static string ToRemotePath(string relativePath)
    {
        var path = (relativePath ?? string.Empty).Replace('/', '\\').Trim('\\');
        return path;
    }

    private static string CombineRemotePath(string? directoryPath, string fileName)
    {
        if (string.IsNullOrEmpty(directoryPath))
        {
            return fileName;
        }

        return directoryPath.Replace('\\', '/').Trim('/') + "/" + fileName;
    }

    private void EnsureConnected()
    {
        if (!_isConnected || _client == null)
        {
            throw new InvalidOperationException("Not connected to SMB server");
        }
    }

    private static void EnsureSuccess(NTStatus status, string operation)
    {
        if (status != NTStatus.STATUS_SUCCESS)
        {
            throw new InvalidOperationException($"Failed to {operation}. SMB status: {status}.");
        }
    }

    private void Disconnect()
    {
        if (_client != null)
        {
            try
            {
                if (_isConnected)
                {
                    _client.Logoff();
                }
            }
            finally
            {
                _client.Disconnect();
                _client = null;
                _isConnected = false;
            }
        }
        else
        {
            _isConnected = false;
        }
    }

    private sealed record SmbPath(string ShareName, string RelativePath);

    private sealed record SmbDirectoryEntry(string Name, bool IsDirectory);
}
