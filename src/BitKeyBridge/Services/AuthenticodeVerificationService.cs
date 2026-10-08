using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;

namespace BitKeyBridge;

public sealed class AuthenticodeVerificationResult
{
    public bool Signed { get; set; }
    public bool Trusted { get; set; }
    public string Publisher { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public string Status { get; set; } = string.Empty;
}

public static class AuthenticodeVerificationService
{
    private static readonly Guid GenericVerifyV2 =
        new("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");

    private const uint WtdUiNone = 2;
    private const uint WtdRevokeNone = 0;
    private const uint WtdChoiceFile = 1;
    private const uint WtdStateActionIgnore = 0;
    private const uint WtdRevocationCheckNone = 0x00000010;

    public static AuthenticodeVerificationResult Verify(
        string path)
    {
        if (string.IsNullOrWhiteSpace(
                path) ||
            !File.Exists(
                path))
        {
            throw new FileNotFoundException(
                "Authenticode target file was not found.",
                path);
        }

        var publisher =
            GetPublisher(
                path);

        if (!OperatingSystem.IsWindows())
        {
            return new AuthenticodeVerificationResult
            {
                Signed =
                    !string.IsNullOrWhiteSpace(
                        publisher),
                Trusted =
                    false,
                Publisher =
                    publisher,
                Status =
                    "WinVerifyTrust is available only on Windows."
            };
        }

        var filePath =
            Marshal.StringToCoTaskMemUni(
                Path.GetFullPath(
                    path));
        var fileInfoPointer =
            IntPtr.Zero;
        var trustDataPointer =
            IntPtr.Zero;

        try
        {
            var fileInfo =
                new WinTrustFileInfo
                {
                    cbStruct =
                        (uint)Marshal.SizeOf<WinTrustFileInfo>(),
                    pcwszFilePath =
                        filePath,
                    hFile =
                        IntPtr.Zero,
                    pgKnownSubject =
                        IntPtr.Zero
                };

            fileInfoPointer =
                Marshal.AllocCoTaskMem(
                    Marshal.SizeOf<WinTrustFileInfo>());
            Marshal.StructureToPtr(
                fileInfo,
                fileInfoPointer,
                fDeleteOld:
                    false);

            var trustData =
                new WinTrustData
                {
                    cbStruct =
                        (uint)Marshal.SizeOf<WinTrustData>(),
                    pPolicyCallbackData =
                        IntPtr.Zero,
                    pSIPClientData =
                        IntPtr.Zero,
                    dwUIChoice =
                        WtdUiNone,
                    fdwRevocationChecks =
                        WtdRevokeNone,
                    dwUnionChoice =
                        WtdChoiceFile,
                    pFile =
                        fileInfoPointer,
                    dwStateAction =
                        WtdStateActionIgnore,
                    hWVTStateData =
                        IntPtr.Zero,
                    pwszURLReference =
                        IntPtr.Zero,
                    dwProvFlags =
                        WtdRevocationCheckNone,
                    dwUIContext =
                        0,
                    pSignatureSettings =
                        IntPtr.Zero
                };

            trustDataPointer =
                Marshal.AllocCoTaskMem(
                    Marshal.SizeOf<WinTrustData>());
            Marshal.StructureToPtr(
                trustData,
                trustDataPointer,
                fDeleteOld:
                    false);

            var status =
                WinVerifyTrust(
                    new IntPtr(
                        -1),
                    GenericVerifyV2,
                    trustDataPointer);

            var code =
                unchecked(
                    (int)status);

            return new AuthenticodeVerificationResult
            {
                Signed =
                    !string.IsNullOrWhiteSpace(
                        publisher),
                Trusted =
                    code == 0,
                Publisher =
                    publisher,
                StatusCode =
                    code,
                Status =
                    code == 0
                        ? "Trusted"
                        : $"WinVerifyTrust returned 0x{status:X8}."
            };
        }
        finally
        {
            if (trustDataPointer !=
                IntPtr.Zero)
            {
                Marshal.DestroyStructure<WinTrustData>(
                    trustDataPointer);
                Marshal.FreeCoTaskMem(
                    trustDataPointer);
            }

            if (fileInfoPointer !=
                IntPtr.Zero)
            {
                Marshal.DestroyStructure<WinTrustFileInfo>(
                    fileInfoPointer);
                Marshal.FreeCoTaskMem(
                    fileInfoPointer);
            }

            Marshal.FreeCoTaskMem(
                filePath);
        }
    }

    public static string GetPublisher(
        string path)
    {
        try
        {
            using var certificate =
                new X509Certificate2(
                    X509Certificate.CreateFromSignedFile(
                        path));

            var subject =
                certificate.GetNameInfo(
                    X509NameType.SimpleName,
                    forIssuer:
                        false);

            return string.IsNullOrWhiteSpace(
                    subject)
                ? "Signed"
                : subject;
        }
        catch (CryptographicException)
        {
            return string.Empty;
        }
    }

    public static void EnforcePolicy(
        AppConfig config,
        string path)
    {
        var result =
            Verify(
                path);

        if (config.RequireTrustedUpdateSignature &&
            !result.Trusted)
        {
            throw new InvalidOperationException(
                "The staged BitKeyBridge executable does not have a trusted Authenticode signature. " +
                result.Status);
        }

        var expectedPublisher =
            config.TrustedUpdatePublisher?.Trim() ??
            string.Empty;

        if (string.IsNullOrWhiteSpace(
                expectedPublisher))
        {
            return;
        }

        if (!result.Trusted)
        {
            throw new InvalidOperationException(
                "TrustedUpdatePublisher is configured, but the staged executable does not have a trusted Authenticode signature.");
        }

        if (!result.Publisher.Contains(
                expectedPublisher,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"The staged executable publisher '{result.Publisher}' does not match trusted publisher '{expectedPublisher}'.");
        }
    }

    [DllImport(
        "wintrust.dll",
        ExactSpelling = true,
        SetLastError = true,
        CharSet = CharSet.Unicode)]
    private static extern uint WinVerifyTrust(
        IntPtr hwnd,
        [MarshalAs(UnmanagedType.LPStruct)]
        Guid pgActionId,
        IntPtr pWvtData);

    [StructLayout(
        LayoutKind.Sequential,
        CharSet = CharSet.Unicode)]
    private struct WinTrustFileInfo
    {
        public uint cbStruct;
        public IntPtr pcwszFilePath;
        public IntPtr hFile;
        public IntPtr pgKnownSubject;
    }

    [StructLayout(
        LayoutKind.Sequential,
        CharSet = CharSet.Unicode)]
    private struct WinTrustData
    {
        public uint cbStruct;
        public IntPtr pPolicyCallbackData;
        public IntPtr pSIPClientData;
        public uint dwUIChoice;
        public uint fdwRevocationChecks;
        public uint dwUnionChoice;
        public IntPtr pFile;
        public uint dwStateAction;
        public IntPtr hWVTStateData;
        public IntPtr pwszURLReference;
        public uint dwProvFlags;
        public uint dwUIContext;
        public IntPtr pSignatureSettings;
    }
}
