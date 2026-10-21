using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;

namespace BitKeyBridge;

public sealed class AuthenticodeVerificationResult
{
    public bool Signed { get; set; }
    public bool Trusted { get; set; }
    public string SignerThumbprint { get; set; } = string.Empty;
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
    private const uint WtdStateActionVerify = 1;
    private const uint WtdStateActionClose = 2;
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

        var publisher = string.Empty;
        var thumbprint = string.Empty;

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
                        WtdStateActionVerify,
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

            // Read the leaf signer from this exact verification state, never
            // an unrelated certificate or timestamp countersigner in the file.
            var verifiedData = Marshal.PtrToStructure<WinTrustData>(trustDataPointer);
            using var certificate = ReadSignerCertificate(verifiedData.hWVTStateData);
            if (certificate is not null)
            {
                publisher = certificate.GetNameInfo(X509NameType.SimpleName, forIssuer: false);
                if (string.IsNullOrWhiteSpace(publisher)) publisher = "Signed";
                thumbprint = certificate.Thumbprint;
            }

            return new AuthenticodeVerificationResult
            {
                SignerThumbprint = thumbprint,
                Signed = certificate is not null,
                Trusted =
                    code == 0 && certificate is not null,
                Publisher =
                    publisher,
                StatusCode =
                    code,
                Status =
                    code != 0
                        ? $"WinVerifyTrust returned 0x{status:X8}."
                        : certificate is null ? "Verified signer certificate unavailable." : "Trusted"
            };
        }
        finally
        {
            if (trustDataPointer !=
                IntPtr.Zero)
            {
                var closingData = Marshal.PtrToStructure<WinTrustData>(trustDataPointer);
                if (closingData.hWVTStateData != IntPtr.Zero)
                {
                    closingData.dwStateAction = WtdStateActionClose;
                    Marshal.StructureToPtr(closingData, trustDataPointer, false);
                    _ = WinVerifyTrust(new IntPtr(-1), GenericVerifyV2, trustDataPointer);
                }
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

    public static string GetPublisher(string path) => Verify(path).Publisher;

    private static X509Certificate2? ReadSignerCertificate(IntPtr state)
    {
        if (state == IntPtr.Zero) return null;
        var provider = WTHelperProvDataFromStateData(state);
        if (provider == IntPtr.Zero) return null;
        var signer = WTHelperGetProvSignerFromChain(provider, 0, false, 0);
        if (signer == IntPtr.Zero) return null;
        var providerCert = WTHelperGetProvCertFromChain(signer, 0);
        if (providerCert == IntPtr.Zero) return null;
        var cert = Marshal.PtrToStructure<ProviderCertificateHeader>(providerCert);
        if (cert.Certificate == IntPtr.Zero) return null;
        var context = Marshal.PtrToStructure<CertificateContext>(cert.Certificate);
        if (context.Encoded == IntPtr.Zero || context.EncodedLength == 0 ||
            context.EncodedLength > 1024 * 1024)
            throw new CryptographicException("Invalid Authenticode signer certificate.");
        var der = new byte[checked((int)context.EncodedLength)];
        Marshal.Copy(context.Encoded, der, 0, der.Length);
        return X509CertificateLoader.LoadCertificate(der);
    }

    internal static bool ThumbprintMatches(string? actual, string? expected)
    {
        // Windows thumbprints are SHA-1 certificate identifiers, not file hashes.
        static string Normalize(string? value) => new((value ?? string.Empty)
            .Where(c => !char.IsWhiteSpace(c) && c != ':').ToArray());
        var pin = Normalize(expected);
        var signer = Normalize(actual);
        return pin.Length == 40 && pin.All(Uri.IsHexDigit) &&
            signer.Length == 40 && signer.All(Uri.IsHexDigit) &&
            string.Equals(signer, pin, StringComparison.OrdinalIgnoreCase);
    }

    public static void EnforcePolicy(
        AppConfig config,
        string path)
    {
        var result =
            Verify(
                path);

        // Honor an explicitly required signature. Unsigned auto-installation is
        // separately gated by official GitHub source, exact release hashes,
        // explicit opt-in and verified update/rollback staging.
        // Strict audit and RBAC checks remain mandatory for recovery.
        var managedAudit =
            config.AuditBrokerEnabled &&
            config.RequireProtectedAuditForRecovery;

        if (config.RequireTrustedUpdateSignature &&
            !result.Trusted)
        {
            throw new InvalidOperationException(
                "The staged BitKeyBridge executable does not have a trusted Authenticode signature. " +
                result.Status);
        }

        // A configured pin always requires Windows trust, even when unsigned
        // updates were previously enabled. Empty pins preserve existing policy.
        if (!string.IsNullOrWhiteSpace(config.TrustedUpdateSignerThumbprint) &&
            (!result.Trusted || !ThumbprintMatches(result.SignerThumbprint,
                config.TrustedUpdateSignerThumbprint)))
            throw new InvalidOperationException(
                "The staged executable does not have the trusted, pinned signer certificate.");

        var expectedPublisher =
            config.TrustedUpdatePublisher?.Trim() ??
            string.Empty;

        // Existing configured publisher pins are never bypassed by the
        // unsigned update opt-in, including for a managed audit broker.
        if (string.IsNullOrWhiteSpace(expectedPublisher))
            return;

        if (!result.Trusted)
        {
            throw new InvalidOperationException(
                "TrustedUpdatePublisher is configured, but the staged executable does not have a trusted Authenticode signature.");
        }

        if (!PublisherMatches(result.Publisher, expectedPublisher,
                exact: managedAudit))
        {
            throw new InvalidOperationException(
                $"The staged executable publisher '{result.Publisher}' does not match trusted publisher '{expectedPublisher}'.");
        }
    }

    /// <summary>
    /// Managed broker updates demand a full publisher-name match. Prefix/
    /// substring matching can accept a different trusted signer's name
    /// containing the expected publisher as attacker-controlled text.
    /// Legacy publisher matching is preserved outside managed audit mode.
    /// </summary>
    internal static bool PublisherMatches(
        string? actual, string? expected, bool exact)
    {
        var publisher = actual?.Trim() ?? string.Empty;
        var pin = expected?.Trim() ?? string.Empty;
        if (publisher.Length == 0 || pin.Length == 0 ||
            string.Equals(publisher, "Signed", StringComparison.OrdinalIgnoreCase))
            return false;

        return exact
            ? string.Equals(publisher, pin, StringComparison.OrdinalIgnoreCase)
            : publisher.Contains(pin, StringComparison.OrdinalIgnoreCase);
    }

    [DllImport("wintrust.dll", ExactSpelling = true)]
    private static extern IntPtr WTHelperProvDataFromStateData(IntPtr state);

    [DllImport("wintrust.dll", ExactSpelling = true)]
    private static extern IntPtr WTHelperGetProvSignerFromChain(IntPtr provider,
        uint signerIndex, [MarshalAs(UnmanagedType.Bool)] bool counterSigner, uint counterSignerIndex);

    [DllImport("wintrust.dll", ExactSpelling = true)]
    private static extern IntPtr WTHelperGetProvCertFromChain(IntPtr signer, uint certificateIndex);

    // Only the native CRYPT_PROVIDER_CERT prefix is read; pointer alignment
    // is supplied by sequential layout on x86, x64 and ARM64.
    [StructLayout(LayoutKind.Sequential)]
    private struct ProviderCertificateHeader
    {
        public uint Size;
        public IntPtr Certificate;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CertificateContext
    {
        public uint Encoding;
        public IntPtr Encoded;
        public uint EncodedLength;
        public IntPtr Info;
        public IntPtr Store;
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
