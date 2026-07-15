using Android.Content;
using Android.Security.Keystore;
using Java.Security;
using Javax.Crypto;
using Javax.Crypto.Spec;
using remename.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace remename.Android;

internal sealed class AndroidSmbCredentialStore : ISmbCredentialStore
{
    private const string KeyAlias = "remename.smb.credentials.key";
    private const string PreferenceName = "secure_credentials";
    private const string MetadataKey = "smb_metadata_v2";
    private const string PasswordPrefix = "smb_password_v2_";
    private readonly Context _context;

    public AndroidSmbCredentialStore(Context context) => _context = context.ApplicationContext!;

    public bool IsAvailable => true;

    public Task<IReadOnlyList<SmbCredentialInfo>> ListAsync() =>
        Task.FromResult<IReadOnlyList<SmbCredentialInfo>>(ReadMetadata());

    public Task<SmbCredential?> LoadAsync(string id)
    {
        var info = ReadMetadata().FirstOrDefault(item => item.Id == id);
        var encodedPassword = Preferences.GetString(PasswordPrefix + id, null);
        if (info is null || string.IsNullOrEmpty(encodedPassword))
            return Task.FromResult<SmbCredential?>(null);

        try
        {
            var password = Encoding.UTF8.GetString(Decrypt(encodedPassword, info));
            return Task.FromResult<SmbCredential?>(new(info.Server, info.Username, password));
        }
        catch
        {
            RemoveRecord(id);
            throw;
        }
    }

    public Task SaveAsync(SmbCredential credential)
    {
        var metadata = ReadMetadata();
        var existing = metadata.FirstOrDefault(item =>
            string.Equals(item.Server, credential.Server, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(item.Username, credential.Username, StringComparison.Ordinal));
        var info = existing ?? new SmbCredentialInfo(Guid.NewGuid().ToString("N"), credential.Server, credential.Username);
        if (existing is null)
            metadata.Add(info);

        var editor = Preferences.Edit()!;
        editor.PutString(MetadataKey, JsonSerializer.Serialize(metadata));
        editor.PutString(PasswordPrefix + info.Id, Encrypt(Encoding.UTF8.GetBytes(credential.Password), info));
        if (!editor.Commit())
            throw new InvalidOperationException("Unable to persist SMB credentials.");
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string id)
    {
        RemoveRecord(id);
        return Task.CompletedTask;
    }

    private List<SmbCredentialInfo> ReadMetadata()
    {
        var json = Preferences.GetString(MetadataKey, null);
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<SmbCredentialInfo>>(json) ?? [];
        }
        catch (JsonException)
        {
            Preferences.Edit()?.Remove(MetadataKey)?.Apply();
            return [];
        }
    }

    private void RemoveRecord(string id)
    {
        var metadata = ReadMetadata();
        metadata.RemoveAll(item => item.Id == id);
        Preferences.Edit()?
            .PutString(MetadataKey, JsonSerializer.Serialize(metadata))?
            .Remove(PasswordPrefix + id)?
            .Apply();
    }

    private static string Encrypt(byte[] plaintext, SmbCredentialInfo info)
    {
        using var cipher = Cipher.GetInstance("AES/GCM/NoPadding")!;
        cipher.Init(CipherMode.EncryptMode, GetOrCreateKey());
        cipher.UpdateAAD(BuildAdditionalData(info));
        var ciphertext = cipher.DoFinal(plaintext)
            ?? throw CryptoError("Credential encryption returned no data.");
        var iv = cipher.GetIV() ?? throw CryptoError("Credential encryption returned no IV.");
        var payload = new byte[1 + iv.Length + ciphertext.Length];
        payload[0] = checked((byte)iv.Length);
        Buffer.BlockCopy(iv, 0, payload, 1, iv.Length);
        Buffer.BlockCopy(ciphertext, 0, payload, 1 + iv.Length, ciphertext.Length);
        return Convert.ToBase64String(payload);
    }

    private static byte[] Decrypt(string encoded, SmbCredentialInfo info)
    {
        var payload = Convert.FromBase64String(encoded);
        if (payload.Length < 13)
            throw CryptoError("Invalid encrypted credential payload.");
        var ivLength = payload[0];
        if (ivLength == 0 || payload.Length <= ivLength + 1)
            throw CryptoError("Invalid encrypted credential IV.");

        using var cipher = Cipher.GetInstance("AES/GCM/NoPadding")!;
        using var parameters = new GCMParameterSpec(128, payload.AsSpan(1, ivLength).ToArray());
        cipher.Init(CipherMode.DecryptMode, GetOrCreateKey(), parameters);
        cipher.UpdateAAD(BuildAdditionalData(info));
        return cipher.DoFinal(payload.AsSpan(1 + ivLength).ToArray())
            ?? throw CryptoError("Credential decryption returned no data.");
    }

    private static byte[] BuildAdditionalData(SmbCredentialInfo info) =>
        JsonSerializer.SerializeToUtf8Bytes(info);

    private static System.Security.Cryptography.CryptographicException CryptoError(string message) => new(message);

    private ISharedPreferences Preferences =>
        _context.GetSharedPreferences(PreferenceName, FileCreationMode.Private)!;

    private static IKey GetOrCreateKey()
    {
        using var keyStore = KeyStore.GetInstance("AndroidKeyStore")!;
        keyStore.Load(null);
        var existing = keyStore.GetKey(KeyAlias, null);
        if (existing is not null)
            return existing;

        using var generator = KeyGenerator.GetInstance(KeyProperties.KeyAlgorithmAes, "AndroidKeyStore")!;
        using var spec = new KeyGenParameterSpec.Builder(KeyAlias, KeyStorePurpose.Encrypt | KeyStorePurpose.Decrypt)
            .SetBlockModes(KeyProperties.BlockModeGcm)
            .SetEncryptionPaddings(KeyProperties.EncryptionPaddingNone)
            .Build();
        generator.Init(spec);
        return generator.GenerateKey()!;
    }
}
