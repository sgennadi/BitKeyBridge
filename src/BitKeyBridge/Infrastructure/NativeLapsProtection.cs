using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace BitKeyBridge;

public sealed class LapsDecryptionException(int status) : Exception(
    $"Windows LAPS decryption failed (0x{unchecked((uint)status):X8}). " +
    "The AD account needs both attribute read access and permission from the encryption principal; " +
    "the domain key service must be reachable.")
{
    public int NativeStatus { get; } = status;
    public bool IsAccessDenied => unchecked((uint)NativeStatus) is 0x80090010 or 0x80070005;
}

public static class NativeLapsProtection
{
    public static byte[] Unprotect(byte[] payload)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Windows LAPS decryption requires Windows.");

        IntPtr descriptor = IntPtr.Zero;
        IntPtr plaintext = IntPtr.Zero;
        uint length = 0;
        try
        {
            const uint silentFlag = 0x40;
            var status = NCryptUnprotectSecret(out descriptor, silentFlag, payload,
                (uint)payload.Length, IntPtr.Zero, IntPtr.Zero, out plaintext, out length);
            if (status != 0) throw new LapsDecryptionException(status);
            if (plaintext == IntPtr.Zero || length == 0 || length > 1024 * 1024)
                throw new FormatException("Invalid Windows LAPS decrypted buffer length.");
            var bytes = new byte[(int)length];
            Marshal.Copy(plaintext, bytes, 0, bytes.Length);
            return bytes;
        }
        finally
        {
            if (plaintext != IntPtr.Zero)
            {
                // Wipe the native secret before releasing the LocalAlloc buffer.
                if (length is > 0 and <= 1024 * 1024)
                    Marshal.Copy(new byte[(int)length], 0, plaintext, (int)length);
                LocalFree(plaintext);
            }
            if (descriptor != IntPtr.Zero)
                NCryptCloseProtectionDescriptor(descriptor);
        }
    }

    [DllImport("ncrypt.dll", ExactSpelling = true)]
    private static extern int NCryptUnprotectSecret(out IntPtr descriptor, uint flags,
        byte[] protectedBlob, uint protectedBlobLength, IntPtr allocationParameters,
        IntPtr window, out IntPtr data, out uint dataLength);

    [DllImport("ncrypt.dll", ExactSpelling = true)]
    private static extern int NCryptCloseProtectionDescriptor(IntPtr descriptor);

    [DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern IntPtr LocalFree(IntPtr memory);
}
