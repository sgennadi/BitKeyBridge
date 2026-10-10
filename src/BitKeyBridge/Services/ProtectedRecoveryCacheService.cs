using System.Runtime.InteropServices;
using System.ComponentModel;
using System.Security.Cryptography;
using System.Text;

namespace BitKeyBridge;

/// <summary>
/// DPAPI CurrentUser recovery cache. Plaintext never goes to a temporary file.
/// This is an opt-in sidecar and never changes the existing export/CSV file.
/// </summary>
public static class ProtectedRecoveryCacheService
{
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("BKBRC01");
    private const int UiForbidden = 0x1;

    public static string DefaultFile =>
        Path.Combine(AppPaths.LocalConfigDirectory, "bitlocker_recovery_keys.bkb");

    public static string EncryptExistingCsv(string sourceCsv, string? destination = null)
    {
        EnsureWindows();
        if (!File.Exists(sourceCsv))
            throw new FileNotFoundException("Existing recovery CSV not found.", sourceCsv);

        var target = Path.GetFullPath(destination ?? DefaultFile);
        if (!target.EndsWith(".bkb", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Protected recovery caches use .bkb extension.");
        if (Path.GetFullPath(sourceCsv).Equals(target, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Encrypted cache must not overwrite a plaintext CSV.");

        var plain = File.ReadAllBytes(sourceCsv);
        byte[]? cipher = null;
        try
        {
            if (plain.Length > 64 * 1024 * 1024)
                throw new InvalidDataException("Recovery CSV is larger than the 64 MB protected-cache limit.");
            if (plain.Length < 10 || !Encoding.UTF8.GetString(plain.AsSpan(0, Math.Min(300, plain.Length)))
                    .Contains("RecoveryKey", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The input is not a BitKeyBridge recovery export CSV.");

            cipher = TransformDpapi(plain, protect: true);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            var temp = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var file = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    file.Write(Magic);
                    file.Write(cipher);
                    file.Flush(flushToDisk: true);
                }

                // Validate decryption and row parser before modifying the active sidecar.
                var check = LoadEncryptedBytes(temp);
                try
                {
                    if (check.Length != plain.Length ||
                        !CryptographicOperations.FixedTimeEquals(
                            SHA256.HashData(check), SHA256.HashData(plain)))
                        throw new InvalidDataException("Protected cache could not be verified.");
                }
                finally { CryptographicOperations.ZeroMemory(check); }

                if (File.Exists(target))
                {
                    File.Copy(target, target + ".previous.bkb", overwrite: true);
                    File.Replace(temp, target, null);
                }
                else File.Move(temp, target);
            }
            finally
            {
                if (File.Exists(temp)) File.Delete(temp);
            }
            return target;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plain);
            if (cipher is not null) CryptographicOperations.ZeroMemory(cipher);
        }
    }

    public static List<RecoverySearchResult> ReadMetadata(string path, string query, int maximumItems)
    {
        var result = new List<RecoverySearchResult>();
        WithReader(path, reader =>
        {
            _ = reader.ReadLine();
            while (reader.ReadLine() is { } line)
            {
                var fields = CsvUtility.ParseLine(line);
                if (fields.Count < 4 ||
                    !(fields[0].Contains(query ?? "", StringComparison.OrdinalIgnoreCase) ||
                    SearchText.IdentifierContains(fields[1], query))) continue;
                DateTime.TryParse(fields[3], out var checkedAt);
                result.Add(new RecoverySearchResult
                {
                    ComputerName = fields[0],
                    RecoveryId = fields[1],
                    LastChecked = checkedAt == default ? null : checkedAt,
                    Source = "Protected cache"
                });
                if (result.Count >= Math.Clamp(maximumItems, 1, 10000)) break;
            }
            return true;
        });
        return result;
    }

    public static string GetRecoveryPassword(string path, string computerName, string recoveryId)
    {
        return WithReader(path, reader =>
        {
            _ = reader.ReadLine();
            while (reader.ReadLine() is { } line)
            {
                var fields = CsvUtility.ParseLine(line);
                if (fields.Count < 4 ||
                    !fields[0].Equals(computerName, StringComparison.OrdinalIgnoreCase) ||
                    !fields[1].Equals(recoveryId, StringComparison.OrdinalIgnoreCase)) continue;

                var password = fields[2];
                if (!System.Text.RegularExpressions.Regex.IsMatch(
                        password, @"^\d{6}(?:-\d{6}){7}$"))
                    throw new InvalidDataException("Recovery password in protected cache has invalid format.");
                return password;
            }
            throw new KeyNotFoundException("Recovery ID was not found in the protected cache.");
        });
    }

    private static T WithReader<T>(string path, Func<StreamReader, T> action)
    {
        var data = LoadEncryptedBytes(path);
        try
        {
            using var stream = new MemoryStream(data, writable: false);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            return action(reader);
        }
        finally { CryptographicOperations.ZeroMemory(data); }
    }

    private static byte[] LoadEncryptedBytes(string path)
    {
        EnsureWindows();
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (file.Length < Magic.Length + 8 || file.Length > 80 * 1024 * 1024)
            throw new InvalidDataException("Invalid protected cache size.");
        var full = new byte[(int)file.Length];
        file.ReadExactly(full);
        try
        {
            if (!full.AsSpan(0, Magic.Length).SequenceEqual(Magic))
                throw new InvalidDataException("Unsupported protected-cache format. Plaintext CSV is not accepted.");
            return TransformDpapi(full.AsSpan(Magic.Length).ToArray(), protect: false);
        }
        finally { CryptographicOperations.ZeroMemory(full); }
    }

    private static byte[] TransformDpapi(byte[] value, bool protect)
    {
        var pin = GCHandle.Alloc(value, GCHandleType.Pinned);
        var input = new DataBlob { Length = value.Length, Data = pin.AddrOfPinnedObject() };
        var output = default(DataBlob);
        IntPtr description = IntPtr.Zero;
        try
        {
            bool ok = protect
                ? CryptProtectData(ref input, "BitKeyBridge user-protected recovery cache",
                    IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, UiForbidden, out output)
                : CryptUnprotectData(ref input, out description, IntPtr.Zero,
                    IntPtr.Zero, IntPtr.Zero, UiForbidden, out output);

            if (!ok) throw new Win32Exception(Marshal.GetLastWin32Error(),
                protect ? "Could not protect cache with current-user DPAPI." :
                          "Could not unlock cache using the current Windows user.");
            var result = new byte[output.Length];
            Marshal.Copy(output.Data, result, 0, result.Length);
            return result;
        }
        finally
        {
            if (output.Data != IntPtr.Zero) LocalFree(output.Data);
            if (description != IntPtr.Zero) LocalFree(description);
            pin.Free();
        }
    }

    private static void EnsureWindows()
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("DPAPI CurrentUser cache requires Windows.");
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DataBlob { public int Length; public IntPtr Data; }

    [DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptProtectData(ref DataBlob input, string? description,
        IntPtr entropy, IntPtr reserved, IntPtr prompt,
        int flags, out DataBlob output);

    [DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptUnprotectData(ref DataBlob input, out IntPtr description,
        IntPtr entropy, IntPtr reserved, IntPtr prompt,
        int flags, out DataBlob output);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LocalFree(IntPtr memory);
}
