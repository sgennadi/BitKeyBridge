using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BitKeyBridge;

public static class LapsSelfTest
{
    public static IReadOnlyList<string> Run()
    {
        var failures = new List<string>();
        void Check(string name, Action action)
        {
            try { action(); }
            catch { failures.Add("LAPS: " + name); }
        }
        void Assert(bool condition) { if (!condition) throw new InvalidOperationException(); }

        var time = new DateTime(2026, 10, 6, 12, 0, 0, DateTimeKind.Utc);
        var secret = "  P@ss\\\"word עברית Русский 🔐  ";
        var json = JsonSerializer.Serialize(new { n = "managed-admin", t = time.ToFileTimeUtc().ToString("x"), p = secret });

        Check("plaintext JSON preserves passwords, account and UTC timestamp", () =>
        {
            using var row = new LapsPasswordEntry();
            LapsSecretCodec.ReadPasswordJson(json, row);
            Assert(row.AccountName == "managed-admin" && row.CopyPassword() == secret && row.UpdatedAtUtc == time);
        });
        foreach (var encoding in new[] { Encoding.Unicode, Encoding.UTF8 })
        {
            Check("decrypted JSON decoding and zeroing: " + encoding.WebName, () =>
            {
                using var row = new LapsPasswordEntry();
                var bytes = encoding.GetBytes(json + "\0");
                LapsSecretCodec.ReadDecryptedPassword(bytes, row);
                Assert(row.CopyPassword() == secret && bytes.All(x => x == 0));
            });
        }
        Check("UTF-16 BOM and passphrases", () =>
        {
            using var row = new LapsPasswordEntry();
            var bytes = Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes(json)).ToArray();
            LapsSecretCodec.ReadDecryptedPassword(bytes, row);
            Assert(row.CopyPassword() == secret && bytes.All(x => x == 0));
        });
        Check("secret disposal and safe serialization", () =>
        {
            using var row = new LapsPasswordEntry();
            row.SetPassword(secret);
            Assert(!JsonSerializer.Serialize(row).Contains("P@ss", StringComparison.Ordinal));
            row.Dispose();
            Assert(!row.HasPassword && row.CopyPassword().Length == 0);
        });
        Check("header offsets and little-endian FILETIME", () =>
        {
            var blob = new byte[19];
            BinaryPrimitives.WriteInt64LittleEndian(blob, time.ToFileTimeUtc());
            BinaryPrimitives.WriteUInt32LittleEndian(blob.AsSpan(8), 3);
            blob[16] = 1; blob[17] = 2; blob[18] = 3;
            var unpacked = LapsSecretCodec.UnpackEncryptedBlob(blob);
            Assert(unpacked.TimestampUtc == time && unpacked.Payload.SequenceEqual(new byte[] { 1, 2, 3 }));
        });
        for (var length = 0; length <= 16; length++)
        {
            var size = length;
            Check("truncated header is rejected: " + size, () =>
            {
                try { LapsSecretCodec.UnpackEncryptedBlob(new byte[size]); }
                catch (FormatException) { return; }
                throw new InvalidOperationException();
            });
        }
        foreach (var reserved in new uint[] { 0, 1, uint.MaxValue })
        {
            Check("invalid size or reserved header fields are rejected: " + reserved, () =>
            {
                var blob = new byte[17];
                BinaryPrimitives.WriteUInt32LittleEndian(blob.AsSpan(8), reserved == 0 ? uint.MaxValue : 1);
                BinaryPrimitives.WriteUInt32LittleEndian(blob.AsSpan(12), reserved);
                try { LapsSecretCodec.UnpackEncryptedBlob(blob); }
                catch (FormatException) { return; }
                throw new InvalidOperationException();
            });
        }
        foreach (var invalid in new[] { "{", "[]", "{}", "{\"n\":\"admin\",\"p\":\"PRIVATE-MARKER\",\"t\":\"invalid\"}",
            "{\"n\":\"admin\",\"p\":\"PRIVATE-MARKER\",\"t\":\"ffffffffffffffff\"}" })
        {
            Check("malformed JSON produces secret-free errors", () =>
            {
                using var row = new LapsPasswordEntry();
                row.SetPassword(secret);
                try { LapsSecretCodec.ReadPasswordJson(invalid, row); }
                catch (FormatException ex)
                {
                    Assert(!row.HasPassword && !ex.ToString().Contains("PRIVATE-MARKER", StringComparison.Ordinal));
                    return;
                }
                throw new InvalidOperationException();
            });
        }
        Check("failed Unicode decoding still wipes the plaintext buffer", () =>
        {
            using var row = new LapsPasswordEntry();
            var bytes = new byte[] { 0xff, 0xfe, 0x7b };
            try { LapsSecretCodec.ReadDecryptedPassword(bytes, row); }
            catch (FormatException) { Assert(bytes.All(x => x == 0)); return; }
            throw new InvalidOperationException();
        });
        Check("Entra password uses UTF-16LE", () =>
            Assert(LapsSecretCodec.DecodeEntraPassword(Convert.ToBase64String(Encoding.Unicode.GetBytes(secret))) == secret));
        Check("Entra Graph JSON accepts UTF-8 and UTF-16 BOM", () =>
        {
            var payload =
                "{\"value\":{\"deviceName\":\"PC-TEST\",\"credentials\":[]}}";

            var utf8 =
                Encoding.UTF8.GetPreamble()
                    .Concat(
                        Encoding.UTF8.GetBytes(
                            payload))
                    .ToArray();

            using (var document =
                   LapsGraphResponseCodec.ParseJsonResponse(
                       utf8,
                       "utf-8"))
            {
                Assert(
                    document.RootElement.TryGetProperty(
                        "value",
                        out _));
            }

            var utf16 =
                Encoding.Unicode.GetPreamble()
                    .Concat(
                        Encoding.Unicode.GetBytes(
                            payload))
                    .ToArray();

            using (var document =
                   LapsGraphResponseCodec.ParseJsonResponse(
                       utf16,
                       "utf-16"))
            {
                Assert(
                    document.RootElement.TryGetProperty(
                        "value",
                        out _));
            }
        });
        Check("Entra current-only and unordered history (direct and wrapped responses)", () =>
        {
            var base64 = Convert.ToBase64String(Encoding.Unicode.GetBytes(secret));
            var response = JsonSerializer.Serialize(new { deviceName = "PC-TEST", credentials = new[]
            {
                new { accountName = "old-admin", accountSid = "S-1-5-21-500", backupDateTime = "2026-10-01T12:00:00Z", passwordBase64 = base64 },
                new { accountName = "new-admin", accountSid = "S-1-5-21-501", backupDateTime = "2026-10-06T12:00:00Z", passwordBase64 = base64 },
                new { accountName = "old-admin", accountSid = "S-1-5-21-500", backupDateTime = "2026-10-02T12:00:00Z", passwordBase64 = "invalid" }
            }});
            foreach (var text in new[]
            {
                response,
                "{\"value\":" + response + "}",
                "{\"value\":[" + response + "]}"
            })
            {
                using var document = JsonDocument.Parse(text);
                using var all = LapsCloudCodec.Read(document.RootElement, "device-id", "fallback", true);
                using var current = LapsCloudCodec.Read(document.RootElement, "device-id", "fallback", false);
                Assert(all.Entries.Count == 3 && all.Entries.Count(x => x.IsHistory) == 2);
                Assert(all.OrderedEntries.First().AccountName == "new-admin");
                Assert(all.Entries.Count(x => x.Status == LapsPasswordStatus.InvalidData) == 1);
                Assert(current.Entries.Count == 1 && !current.Entries[0].IsHistory);
                Assert(!JsonSerializer.Serialize(all).Contains(base64, StringComparison.Ordinal));
                all.Dispose();
                Assert(all.Entries.Count == 0);
            }
        });
        Check("missing Entra credentials return a permission-aware explanation", () =>
        {
            using var document = JsonDocument.Parse("{\"deviceName\":\"PC-TEST\"}");
            using var result = LapsCloudCodec.Read(document.RootElement, "device-id", "fallback", true);
            Assert(result.Entries.Count == 0 && result.Note.Contains("ReadBasic.All", StringComparison.Ordinal));
        });
        Check("diagnostic redaction removes LAPS secrets", () =>
        {
            var raw =
                "Password: Super-Secret-LAPS-Value" +
                Environment.NewLine +
                "{\"n\":\"admin\",\"p\":\"AnotherSecretValue\",\"t\":\"01\"}";
            var sanitized =
                DiagnosticRedaction.Sanitize(raw);
            Assert(!sanitized.Contains("Super-Secret-LAPS-Value", StringComparison.Ordinal));
            Assert(!sanitized.Contains("AnotherSecretValue", StringComparison.Ordinal));
            Assert(sanitized.Contains("[REDACTED]", StringComparison.Ordinal));
        });
        Check("AD LAPS empty results distinguish visible backup metadata", () =>
        {
            foreach (var (legacy, windows) in new[]
            {
                (false, false),
                (true, false),
                (false, true),
                (true, true)
            })
            {
                using var result = new LapsReadResult
                {
                    ComputerName = "PC-TEST",
                    DirectoryServer = "dc-test.example",
                    LegacyBackupIndicatorPresent = legacy,
                    WindowsBackupIndicatorPresent = windows
                };

                result.Note = LapsReadDiagnostics.EmptyAdNote(
                    legacy,
                    windows);

                var status = LapsReadDiagnostics.EmptyStatus(
                    result,
                    cloud: false);
                var details = LapsReadDiagnostics.EmptyDetails(
                    result,
                    cloud: false);

                Assert(result.HasAdBackupIndicators == (legacy || windows));
                Assert(result.Entries.Count == 0);
                Assert(status.Contains("0 records", StringComparison.Ordinal));
                Assert(details.Contains("Records returned: 0", StringComparison.Ordinal));
                Assert(details.Contains("not independently verified", StringComparison.Ordinal));
                Assert(details.Contains(result.Note, StringComparison.Ordinal));
                Assert(!details.Contains(secret, StringComparison.Ordinal));
                Assert(!details.Contains("permission denied", StringComparison.OrdinalIgnoreCase));
            }
        });
        Check("Entra LAPS empty responses get a distinct secret-free hint", () =>
        {
            using var result = new LapsReadResult
            {
                ComputerName = "CLOUD-PC",
                DirectoryServer = "Entra"
            };

            var status = LapsReadDiagnostics.EmptyStatus(result, cloud: true);
            var details = LapsReadDiagnostics.EmptyDetails(result, cloud: true);

            Assert(status.Contains("Entra", StringComparison.Ordinal));
            Assert(details.Contains("Graph permissions", StringComparison.Ordinal));
            Assert(!details.Contains(secret, StringComparison.Ordinal));
        });
        Check("schema diagnostics do not invent NotDetected when inspection did not complete", () =>
        {
            Assert(
                LapsAccessDiagnostics.ResolveSchemaState(
                    schemaInspectionCompleted: false,
                    schemaAttributeDetected: false) ==
                LapsAccessState.NotProbed);
        });
        Check("schema diagnostics accept non-secret metadata as positive schema evidence", () =>
        {
            Assert(
                LapsAccessDiagnostics.ResolveSchemaState(
                    schemaInspectionCompleted: false,
                    schemaAttributeDetected: false,
                    metadataEvidence: true) ==
                LapsAccessState.Available);
            Assert(
                LapsAccessDiagnostics.ResolveSchemaState(
                    schemaInspectionCompleted: true,
                    schemaAttributeDetected: false) ==
                LapsAccessState.NotDetected);
        });
        Check("search identifier normalization ignores braces, hyphens and case", () =>
        {
            Assert(
                SearchText.IdentifierContains(
                    "{B1E4-2056-D492}",
                    "b1e42056"));
            Assert(
                SearchText.IdentifierContains(
                    "B1E42056D492442E9996CC6405CD3EDC",
                    "056-d49"));
            Assert(
                !SearchText.IdentifierContains(
                    "B1E42056",
                    "ABCDEF"));
        });
        Check("live search thresholds remain stable", () =>
        {
            Assert(
                SearchText.DebounceMilliseconds >= 300 &&
                SearchText.DebounceMilliseconds <= 1000);
            Assert(
                SearchText.MinimumLiveSearchCharacters == 2);
        });
        Check("multi-valued AD history sorts by time within each source", () =>
        {
            using var result = new LapsReadResult();
            result.Entries.Add(new() { Source = "Windows LAPS (encrypted)", IsHistory = true, UpdatedAtUtc = time.AddDays(-10) });
            result.Entries.Add(new() { Source = "Windows LAPS (encrypted)", UpdatedAtUtc = time });
            result.Entries.Add(new() { Source = "Windows LAPS (encrypted)", IsHistory = true, UpdatedAtUtc = time.AddDays(-1) });
            var ordered = result.OrderedEntries.ToList();
            Assert(!ordered[0].IsHistory && ordered[1].UpdatedAtUtc > ordered[2].UpdatedAtUtc);
        });
        if (OperatingSystem.IsWindows())
            Check("native DPAPI-NG roundtrip and tampered data rejection", () => NativeRoundTrip(json, secret));
        return failures;
    }

    private static void NativeRoundTrip(string json, string secret)
    {
        var status = NCryptCreateProtectionDescriptor("LOCAL=logon", 0, out var descriptor);
        if (status != 0) throw new LapsDecryptionException(status);
        IntPtr encrypted = IntPtr.Zero;
        var plaintext = Encoding.Unicode.GetBytes(json + "\0");
        try
        {
            status = NCryptProtectSecret(descriptor, 0x40, plaintext, (uint)plaintext.Length,
                IntPtr.Zero, IntPtr.Zero, out encrypted, out var length);
            if (status != 0) throw new LapsDecryptionException(status);
            var protectedBytes = new byte[(int)length];
            Marshal.Copy(encrypted, protectedBytes, 0, protectedBytes.Length);
            using var row = new LapsPasswordEntry();
            LapsSecretCodec.ReadDecryptedPassword(NativeLapsProtection.Unprotect(protectedBytes), row);
            if (row.CopyPassword() != secret) throw new InvalidOperationException();
            protectedBytes[^1] ^= 0xff;
            try
            {
                var bytes = NativeLapsProtection.Unprotect(protectedBytes);
                CryptographicOperations.ZeroMemory(bytes);
            }
            catch (LapsDecryptionException) { return; }
            throw new InvalidOperationException();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
            if (encrypted != IntPtr.Zero) LocalFree(encrypted);
            NCryptCloseProtectionDescriptor(descriptor);
        }
    }

    [DllImport("ncrypt.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int NCryptCreateProtectionDescriptor(string descriptor, uint flags, out IntPtr handle);
    [DllImport("ncrypt.dll", ExactSpelling = true)]
    private static extern int NCryptProtectSecret(IntPtr descriptor, uint flags, byte[] data, uint length,
        IntPtr allocationParameters, IntPtr window, out IntPtr protectedBlob, out uint protectedLength);
    [DllImport("ncrypt.dll", ExactSpelling = true)]
    private static extern int NCryptCloseProtectionDescriptor(IntPtr descriptor);
    [DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern IntPtr LocalFree(IntPtr pointer);
}
