using System.IO.Compression;
using System.Text;

namespace BitKeyBridge;

/// <summary>
/// Checks the exact single-file Windows release package before extraction
/// and repeats the PE architecture check before any staged EXE can run.
/// This does not replace SHA-256 or Authenticode publisher authentication.
/// </summary>
internal static class UpdatePackageValidator
{
    private const long MaximumUncompressedExecutableBytes = 512L * 1024L * 1024L;
    private const uint PeSignature = 0x00004550;
    private const ushort DosSignature = 0x5A4D;

    internal static void ValidateSingleExecutableArchive(string zipPath)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        ValidateArchiveEntries(archive.Entries);
    }

    internal static void ValidateArchiveEntries(
        IReadOnlyList<ZipArchiveEntry> entries,
        long maximumUncompressedBytes = MaximumUncompressedExecutableBytes)
    {
        if (maximumUncompressedBytes <= 0 || entries.Count is < 1 or > 4)
            throw new InvalidDataException("Unexpected BitKeyBridge release ZIP structure.");

        var executableCount = 0;
        long totalUncompressedBytes = 0;
        foreach (var entry in entries)
        {
            // Windows tar-created ZIP files may include a harmless "./"
            // root directory marker. No other directories are permitted.
            if (entry.FullName == "./")
                continue;

            if (entry.FullName != "BitKeyBridge.exe" &&
                entry.FullName != "./BitKeyBridge.exe")
            {
                throw new InvalidDataException(
                    "The BitKeyBridge update ZIP contains an unexpected path or extra file.");
            }

            // Reject symbolic-link entries, even when named BitKeyBridge.exe.
            var unixFileType = (entry.ExternalAttributes >> 16) & 0xF000;
            if (unixFileType == 0xA000)
                throw new InvalidDataException("The update ZIP contains a symbolic link.");

            executableCount++;
            if (entry.Length <= 0 ||
                entry.Length > maximumUncompressedBytes - totalUncompressedBytes)
            {
                throw new InvalidDataException(
                    "The extracted update size exceeds the safe release limit.");
            }

            totalUncompressedBytes += entry.Length;
        }

        if (executableCount != 1)
            throw new InvalidDataException(
                "The update ZIP must contain exactly one BitKeyBridge.exe at its root.");
    }

    internal static void ValidateExecutableMachine(string executablePath, string rid)
    {
        using var stream = new FileStream(executablePath,
            FileMode.Open, FileAccess.Read, FileShare.Read);
        ValidateExecutableMachine(stream, rid);
    }

    internal static void ValidateExecutableMachine(Stream stream, string rid)
    {
        var expected = rid switch
        {
            "win-x86" => (ushort)0x014C,
            "win-x64" => (ushort)0x8664,
            "win-arm64" => (ushort)0xAA64,
            _ => throw new InvalidDataException(
                "The updater received an unsupported Windows architecture.")
        };

        if (!stream.CanSeek || !stream.CanRead || stream.Length < 0x86)
            throw new InvalidDataException("The update executable is not a readable PE image.");

        using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);
        stream.Position = 0;
        if (reader.ReadUInt16() != DosSignature)
            throw new InvalidDataException("The update executable is missing the MZ header.");

        stream.Position = 0x3C;
        var peOffset = reader.ReadInt32();
        if (peOffset < 0x40 || peOffset > stream.Length - 6)
            throw new InvalidDataException("The update executable has an invalid PE header offset.");

        stream.Position = peOffset;
        if (reader.ReadUInt32() != PeSignature)
            throw new InvalidDataException("The update executable is missing a valid PE signature.");

        var machine = reader.ReadUInt16();
        if (machine != expected)
            throw new InvalidDataException(
                $"The downloaded executable architecture does not match {rid}.");

        stream.Position = 0;
    }
}
