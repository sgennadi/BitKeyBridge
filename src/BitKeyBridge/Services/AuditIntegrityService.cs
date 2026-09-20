using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BitKeyBridge;

public static class AuditIntegrityService
{
    public const int CurrentChainVersion = 1;

    public static string ComputeHash(AuditEntry entry)
    {
        var builder = new StringBuilder(1024);

        Append(builder, entry.ChainVersion.ToString());
        Append(builder, entry.PreviousHash);
        Append(
            builder,
            entry.TimestampUtc
                .ToUniversalTime()
                .ToString("O"));
        Append(builder, entry.User);
        Append(builder, entry.Host);
        Append(builder, entry.Action);
        Append(builder, entry.Result);
        Append(builder, entry.ComputerName);
        Append(builder, entry.RecoveryId);
        Append(builder, entry.Source);
        Append(builder, entry.AuthMode);
        Append(builder, entry.Reference);
        Append(builder, entry.Reason);
        Append(builder, entry.Details);

        return Convert.ToHexString(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(builder.ToString())));
    }

    public static string GetLastHash(string path)
    {
        try
        {
            if (!File.Exists(path))
                return string.Empty;

            foreach (var line in File.ReadLines(path).Reverse())
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                try
                {
                    var entry =
                        JsonSerializer.Deserialize<AuditEntry>(line);
                    if (entry is not null &&
                        entry.ChainVersion > 0 &&
                        !string.IsNullOrWhiteSpace(entry.EntryHash))
                    {
                        return entry.EntryHash;
                    }
                }
                catch
                {
                    return string.Empty;
                }
            }
        }
        catch
        {
        }

        return string.Empty;
    }

    public static AuditIntegrityResult Verify(string path)
    {
        using var processLock = AcquireInterprocessLock(path);
        if (processLock is null)
        {
            return Fail(
                new AuditIntegrityResult(),
                "Could not acquire the audit interprocess lock for verification.");
        }

        var result = new AuditIntegrityResult();

        var paths = new[]
        {
            path + ".old",
            path
        }
        .Where(File.Exists)
        .ToArray();

        string? previousHash = null;
        var chainStarted = false;

        foreach (var file in paths)
        {
            result.FilesChecked++;

            var lineNumber = 0;
            foreach (var line in File.ReadLines(file))
            {
                lineNumber++;
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                result.TotalEntries++;

                AuditEntry? entry;
                try
                {
                    entry = JsonSerializer.Deserialize<AuditEntry>(line);
                }
                catch (Exception ex)
                {
                    return Fail(
                        result,
                        $"{Path.GetFileName(file)}:{lineNumber}: invalid JSON: {ex.Message}");
                }

                if (entry is null)
                {
                    return Fail(
                        result,
                        $"{Path.GetFileName(file)}:{lineNumber}: empty audit entry.");
                }

                if (entry.ChainVersion <= 0 ||
                    string.IsNullOrWhiteSpace(entry.EntryHash))
                {
                    if (chainStarted)
                    {
                        return Fail(
                            result,
                            $"{Path.GetFileName(file)}:{lineNumber}: legacy/unhashed entry appears after the hash chain started.");
                    }

                    result.LegacyEntries++;
                    continue;
                }

                if (entry.ChainVersion != CurrentChainVersion)
                {
                    return Fail(
                        result,
                        $"{Path.GetFileName(file)}:{lineNumber}: unsupported chain version {entry.ChainVersion}.");
                }

                string calculated;
                byte[] expectedHash;
                byte[] actualHash;
                try
                {
                    calculated = ComputeHash(entry);
                    expectedHash = Convert.FromHexString(calculated);
                    actualHash = Convert.FromHexString(entry.EntryHash);
                }
                catch (Exception ex)
                {
                    return Fail(
                        result,
                        $"{Path.GetFileName(file)}:{lineNumber}: invalid hash encoding: {ex.Message}");
                }

                if (!CryptographicOperations.FixedTimeEquals(
                        expectedHash,
                        actualHash))
                {
                    return Fail(
                        result,
                        $"{Path.GetFileName(file)}:{lineNumber}: entry hash mismatch.");
                }

                if (chainStarted &&
                    !string.Equals(
                        entry.PreviousHash,
                        previousHash,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return Fail(
                        result,
                        $"{Path.GetFileName(file)}:{lineNumber}: previous-hash link mismatch.");
                }

                chainStarted = true;
                previousHash = entry.EntryHash;
                result.LastHash = entry.EntryHash;
                result.ChainedEntries++;
            }
        }

        return result;
    }

    public static AuditIntegrityStatus VerifyAndPersist(
        string path,
        string? statusPath = null)
    {
        var result = Verify(path);
        var status = new AuditIntegrityStatus
        {
            VerifiedAtUtc = DateTime.UtcNow,
            Valid = result.Valid,
            FilesChecked = result.FilesChecked,
            TotalEntries = result.TotalEntries,
            LegacyEntries = result.LegacyEntries,
            ChainedEntries = result.ChainedEntries,
            LastHash = result.LastHash,
            FirstError = result.FirstError
        };

        TryPersistStatus(
            statusPath ?? AppPaths.AuditIntegrityStatusFile,
            status);

        return status;
    }

    public static AuditIntegrityStatus? ReadLastStatus(
        string? statusPath = null)
    {
        try
        {
            return JsonStore.Read<AuditIntegrityStatus>(
                statusPath ?? AppPaths.AuditIntegrityStatusFile);
        }
        catch
        {
            return null;
        }
    }

    public static DateTime? GetLastAuditWriteUtc(string path)
    {
        DateTime? latest = null;

        foreach (var candidate in new[]
                 {
                     path + ".old",
                     path
                 })
        {
            try
            {
                if (!File.Exists(candidate))
                    continue;

                var value = File.GetLastWriteTimeUtc(candidate);
                if (latest is null || value > latest)
                    latest = value;
            }
            catch
            {
            }
        }

        return latest;
    }

    private static void TryPersistStatus(
        string path,
        AuditIntegrityStatus status)
    {
        try
        {
            JsonStore.WriteAtomic(path, status);
        }
        catch
        {
            // Verification result should still be returned even if the
            // current identity cannot persist the machine-level cache.
        }
    }

    private static FileStream? AcquireInterprocessLock(
        string path)
    {
        var lockPath = path + ".lock";

        for (var attempt = 0; attempt < 250; attempt++)
        {
            try
            {
                return new FileStream(
                    lockPath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None);
            }
            catch (IOException)
            {
                Thread.Sleep(20);
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }

        return null;
    }

    private static AuditIntegrityResult Fail(
        AuditIntegrityResult result,
        string error)
    {
        result.Valid = false;
        result.FirstError = error;
        return result;
    }

    private static void Append(
        StringBuilder builder,
        string? value)
    {
        value ??= string.Empty;
        builder.Append(value.Length);
        builder.Append(':');
        builder.Append(value);
        builder.Append('|');
    }
}
