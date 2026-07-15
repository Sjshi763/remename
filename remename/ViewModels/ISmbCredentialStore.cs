using System.Collections.Generic;
using System.Threading.Tasks;

namespace remename.ViewModels;

public sealed record SmbCredential(string Server, string Username, string Password);
public sealed record SmbCredentialInfo(string Id, string Server, string Username);

public interface ISmbCredentialStore
{
    bool IsAvailable { get; }
    Task<IReadOnlyList<SmbCredentialInfo>> ListAsync();
    Task<SmbCredential?> LoadAsync(string id);
    Task SaveAsync(SmbCredential credential);
    Task DeleteAsync(string id);
}

internal sealed class UnavailableSmbCredentialStore : ISmbCredentialStore
{
    public bool IsAvailable => false;
    public Task<IReadOnlyList<SmbCredentialInfo>> ListAsync() =>
        Task.FromResult<IReadOnlyList<SmbCredentialInfo>>([]);
    public Task<SmbCredential?> LoadAsync(string id) => Task.FromResult<SmbCredential?>(null);
    public Task SaveAsync(SmbCredential credential) => Task.CompletedTask;
    public Task DeleteAsync(string id) => Task.CompletedTask;
}
