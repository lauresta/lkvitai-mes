using System.Collections.Concurrent;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using LKvitai.MES.Domain.Entities;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace LKvitai.MES.Infrastructure.Persistence;

public static class PiiEncryption
{
    private static readonly ConcurrentDictionary<string, byte[]> RuntimeKeys = new(StringComparer.Ordinal);
    private static readonly object Sync = new();
    private static string? _activeKeyId;

    public static ValueConverter<string, string> StringConverter { get; } = new(
        value => Encrypt(value),
        value => Decrypt(value));

    public static string ActiveKeyId
    {
        get
        {
            EnsureInitialized();
            return _activeKeyId!;
        }
    }

    public static string Encrypt(string? plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
        {
            return plaintext ?? string.Empty;
        }

        if (plaintext.StartsWith("enc:", StringComparison.Ordinal))
        {
            return plaintext;
        }

        EnsureInitialized();

        var keyId = _activeKeyId!;
        var key = RuntimeKeys[keyId];

        var nonce = RandomNumberGenerator.GetBytes(12);
        var input = Encoding.UTF8.GetBytes(plaintext);
        var cipher = new byte[input.Length];
        var tag = new byte[16];

        using var aes = new AesGcm(key, tagSizeInBytes: 16);
        aes.Encrypt(nonce, input, cipher, tag);

        return string.Create(
            keyId.Length + 7 + Convert.ToBase64String(nonce).Length + Convert.ToBase64String(tag).Length + Convert.ToBase64String(cipher).Length,
            (keyId, nonce, tag, cipher),
            static (span, state) =>
            {
                var nonceBase64 = Convert.ToBase64String(state.nonce);
                var tagBase64 = Convert.ToBase64String(state.tag);
                var cipherBase64 = Convert.ToBase64String(state.cipher);
                var payload = $"enc:{state.keyId}:{nonceBase64}:{tagBase64}:{cipherBase64}";
                payload.AsSpan().CopyTo(span);
            });
    }

    public static string Decrypt(string? storedValue)
    {
        if (string.IsNullOrEmpty(storedValue) || !storedValue.StartsWith("enc:", StringComparison.Ordinal))
        {
            return storedValue ?? string.Empty;
        }

        EnsureInitialized();

        var parts = storedValue.Split(':', 5, StringSplitOptions.None);
        if (parts.Length != 5)
        {
            return storedValue;
        }

        var keyId = parts[1];
        var nonce = Convert.FromBase64String(parts[2]);
        var tag = Convert.FromBase64String(parts[3]);
        var cipher = Convert.FromBase64String(parts[4]);

        if (!RuntimeKeys.TryGetValue(keyId, out var key))
        {
            key = RuntimeKeys.Values.First();
        }

        var plaintext = new byte[cipher.Length];

        using var aes = new AesGcm(key, tagSizeInBytes: 16);
        aes.Decrypt(nonce, cipher, tag, plaintext);

        return Encoding.UTF8.GetString(plaintext);
    }

    public static string RotateToNewRuntimeKey()
    {
        EnsureInitialized();

        var newKeyId = $"v{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        RuntimeKeys[newKeyId] = RandomNumberGenerator.GetBytes(32);
        _activeKeyId = newKeyId;
        return newKeyId;
    }

    public static string EnsureCurrentKeyRecord(ICollection<PiiEncryptionKeyRecord> records)
    {
        EnsureInitialized();
        var keyId = _activeKeyId!;

        if (!records.Any(x => x.KeyId == keyId))
        {
            records.Add(new PiiEncryptionKeyRecord
            {
                KeyId = keyId,
                Active = true,
                ActivatedAt = DateTimeOffset.UtcNow,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        return keyId;
    }

    private static void EnsureInitialized()
    {
        if (_activeKeyId is not null)
        {
            return;
        }

        lock (Sync)
        {
            if (_activeKeyId is not null)
            {
                return;
            }

            var configured = Environment.GetEnvironmentVariable("PII_ENCRYPTION_KEYS");
            if (!string.IsNullOrWhiteSpace(configured))
            {
                var chunks = configured.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (var chunk in chunks)
                {
                    var tuple = chunk.Split(':', 2, StringSplitOptions.TrimEntries);
                    if (tuple.Length != 2)
                    {
                        continue;
                    }

                    try
                    {
                        RuntimeKeys[tuple[0]] = Convert.FromBase64String(tuple[1]);
                    }
                    catch
                    {
                        // ignore malformed key entries
                    }
                }
            }

            if (RuntimeKeys.IsEmpty)
            {
                RuntimeKeys["v1"] = SHA256.HashData(Encoding.UTF8.GetBytes("LKvitai-MES-PII-DEV-KEY"));
            }

            _activeKeyId = Environment.GetEnvironmentVariable("PII_ENCRYPTION_ACTIVE_KEY");
            if (string.IsNullOrWhiteSpace(_activeKeyId) || !RuntimeKeys.ContainsKey(_activeKeyId))
            {
                _activeKeyId = RuntimeKeys.Keys.OrderBy(x => x, StringComparer.Ordinal).Last();
            }
        }
    }
}
