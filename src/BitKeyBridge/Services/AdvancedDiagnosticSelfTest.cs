using System.IO.Compression;
using System.Text.Json;

namespace BitKeyBridge;

public static class AdvancedDiagnosticSelfTest
{
    public static IReadOnlyList<string> Run(string tempDirectory)
    {
        var failures = new List<string>();
        failures.AddRange(AdvancedDiagnosticRulesSelfTest.Run());

        void Check(string label, Action action)
        {
            try { action(); }
            catch (Exception ex)
            {
                failures.Add("Advanced diagnostics / " + label + ": " + ex.Message);
            }
        }
        static void Require(bool ok, string message)
        {
            if (!ok) throw new InvalidOperationException(message);
        }

        Check("DPAPI CurrentUser protected recovery CSV metadata and one-time secret read", () =>
        {
            var path = Path.Combine(tempDirectory, "protected-cache-original.csv");
            var encrypted = Path.Combine(tempDirectory, "protected-cache-test.bkb");
            const string secret = "111111-222222-333333-444444-555555-666666-777777-888888";
            const string recoveryId = "33333333-4444-5555-6666-777777777777";
            CsvUtility.WriteRecoveryCsvAtomic(path, [
                new RecoveryRecord("PC-TEST", recoveryId, secret, DateTime.UtcNow)
            ]);
            var output = ProtectedRecoveryCacheService.EncryptExistingCsv(path, encrypted);
            Require(output == encrypted, "Incorrect encrypted cache destination.");
            Require(File.Exists(path), "Migration deleted the plaintext original without consent.");
            var bytes = File.ReadAllBytes(encrypted);
            Require(!System.Text.Encoding.ASCII.GetString(bytes).Contains(secret, StringComparison.Ordinal),
                "Protected file contains cleartext recovery secret.");
            var metadata = ProtectedRecoveryCacheService.ReadMetadata(encrypted, "PC-TEST", 10);
            Require(metadata.Count == 1 && metadata[0].Source == "Protected cache",
                "Encrypted metadata search failed.");
            Require(ProtectedRecoveryCacheService.GetRecoveryPassword(
                encrypted, "PC-TEST", recoveryId) == secret, "Encrypted recovery read failed.");

            // Re-running explicitly replaces the sidecar but retains an encrypted rollback copy.
            ProtectedRecoveryCacheService.EncryptExistingCsv(path, encrypted);
            Require(File.Exists(encrypted + ".previous.bkb"), "Encrypted rollback copy missing.");
            var tampered = Path.Combine(tempDirectory, "tampered-cache.bkb");
            bytes = File.ReadAllBytes(encrypted);
            bytes[^1] ^= 0xFF;
            File.WriteAllBytes(tampered, bytes);
            var refused = false;
            try { ProtectedRecoveryCacheService.ReadMetadata(tampered, "", 10); }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception
                                        or InvalidDataException
                                        or System.Security.Cryptography.CryptographicException)
            { refused = true; }
            Require(refused, "Tampered DPAPI cache was accepted.");
        });

        Check("Read-only endpoint ZIP parser and no secret-containing event messages", () =>
        {
            var name = Path.Combine(tempDirectory, "endpoint-fixture.zip");
            var data = new EndpointDiagnosticBundle { ComputerName = "PC-TEST" };
            data.WhitelistedPolicyMetadata["LAPS/BackupDirectory"] = "2";
            data.EventSummaries.Add(new EndpointEventSummary(
                "Microsoft-Windows-LAPS/Operational", 10021, "Information", DateTimeOffset.UtcNow));
            using (var file = File.Create(name))
            using (var zip = new ZipArchive(file, ZipArchiveMode.Create))
            {
                using var writer = new StreamWriter(zip.CreateEntry("endpoint-summary.json").Open());
                writer.Write(JsonSerializer.Serialize(data));
            }
            var report = EndpointDiagnosticCollector.Read(name);
            Require(report.ComputerName == "PC-TEST", "Endpoint ZIP did not preserve target.");
            Require(report.EventSummaries.Count == 1 && report.EventSummaries[0].Id == 10021,
                "Endpoint ZIP events not parsed.");
            Require(!EndpointDiagnosticCollector.SafeSummary(report).Contains("RecoveryKey"),
                "Endpoint ZIP unexpectedly exposed a recovery-key field.");

            var rejected = false;
            try
            {
                using (var file = File.Create(Path.Combine(tempDirectory, "bad-endpoint.zip")))
                using (var zip = new ZipArchive(file, ZipArchiveMode.Create))
                using (var writer = new StreamWriter(zip.CreateEntry("unexpected.txt").Open()))
                    writer.Write("untrusted");
                EndpointDiagnosticCollector.Read(Path.Combine(tempDirectory, "bad-endpoint.zip"));
            }
            catch (InvalidDataException) { rejected = true; }
            Require(rejected, "Unexpected ZIP entries were accepted.");
        });

        return failures;
    }
}
