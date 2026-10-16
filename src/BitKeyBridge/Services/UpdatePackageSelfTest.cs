using System.Buffers.Binary;
using System.IO.Compression;

namespace BitKeyBridge;

/// <summary>
/// Offline negative tests: corrupted archives and cross-architecture PE
/// binaries must fail BEFORE an unsigned staged EXE is executed.
/// </summary>
internal static class UpdatePackageSelfTest
{
    internal static IEnumerable<string> Run()
    {
        var failures = new List<string>();

        TestArchive(["BitKeyBridge.exe"], accepted: true, "root-only");
        TestArchive(["./", "./BitKeyBridge.exe"], accepted: true, "tar-root-marker");
        TestArchive(["subfolder/BitKeyBridge.exe"], accepted: false, "nested-exe");
        TestArchive(["../BitKeyBridge.exe"], accepted: false, "path-traversal");
        TestArchive(["BitKeyBridge.exe", "unwanted.dll"], accepted: false, "sidecar-dll");
        TestArchive(["BitKeyBridge.exe", "BitKeyBridge.exe"], accepted: false, "duplicate-exe");
        TestArchive(["BitKeyBridge.exe"], accepted: false, "oversize", maximumSize: 8);

        foreach (var (rid, machine) in new[]
                 {
                     ("win-x64", (ushort)0x8664),
                     ("win-x86", (ushort)0x014C),
                     ("win-arm64", (ushort)0xAA64)
                 })
        {
            TestMachine(rid, machine, accepted: true, rid + "-matching");
        }

        TestMachine("win-x64", 0x014C, accepted: false, "x64-wrong-architecture");
        TestMachine("win-arm64", 0x8664, accepted: false, "arm64-wrong-architecture");
        TestMachine("win-x64", 0x8664, accepted: false, "bad-dos-header", badDos: true);
        TestMachine("win-x64", 0x8664, accepted: false, "bad-pe-header", badPe: true);

        return failures;

        void TestArchive(
            string[] names, bool accepted, string label, long maximumSize = 512L * 1024L * 1024L)
        {
            try
            {
                using var buffer = new MemoryStream();
                using (var writer = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
                {
                    foreach (var name in names)
                    {
                        var entry = writer.CreateEntry(name);
                        if (name.EndsWith("/", StringComparison.Ordinal))
                            continue;

                        using var output = entry.Open();
                        output.Write(new byte[32]);
                    }
                }

                buffer.Position = 0;
                using var archive = new ZipArchive(buffer, ZipArchiveMode.Read, leaveOpen: true);
                UpdatePackageValidator.ValidateArchiveEntries(archive.Entries, maximumSize);
                if (!accepted)
                    failures.Add("Update ZIP safety test accepted " + label + ".");
            }
            catch (InvalidDataException)
            {
                if (accepted)
                    failures.Add("Update ZIP safety test rejected " + label + ".");
            }
            catch (Exception ex)
            {
                failures.Add("Update ZIP safety test " + label + ": " +
                    DiagnosticRedaction.Sanitize(ex.Message));
            }
        }

        void TestMachine(
            string rid, ushort machine, bool accepted, string label,
            bool badDos = false, bool badPe = false)
        {
            try
            {
                var bytes = new byte[256];
                if (!badDos)
                {
                    bytes[0] = (byte)'M';
                    bytes[1] = (byte)'Z';
                }
                BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0x3C), 0x80);
                if (!badPe)
                {
                    bytes[0x80] = (byte)'P';
                    bytes[0x81] = (byte)'E';
                }
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x84), machine);
                using var stream = new MemoryStream(bytes, writable: false);
                UpdatePackageValidator.ValidateExecutableMachine(stream, rid);
                if (!accepted)
                    failures.Add("Update PE safety test accepted " + label + ".");
            }
            catch (InvalidDataException)
            {
                if (accepted)
                    failures.Add("Update PE safety test rejected " + label + ".");
            }
            catch (Exception ex)
            {
                failures.Add("Update PE safety test " + label + ": " +
                    DiagnosticRedaction.Sanitize(ex.Message));
            }
        }
    }
}
