#requires -Version 5.1
<#
.SYNOPSIS
Verifies the *published* Win32 executable's embedded manifest.
.DESCRIPTION
Read-only resource inspection. Does not execute the application, change any
Windows compatibility policy, or read user/device/account/recovery data.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $Executable
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$fullPath = [System.IO.Path]::GetFullPath($Executable)
if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
    throw 'Manifest inspection requires an existing executable file.'
}

Add-Type -TypeDefinition @'
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
public static class BitKeyBridgeManifestProbe
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadLibraryEx(string path, IntPtr file, uint flags);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr FindResource(IntPtr module, IntPtr name, IntPtr type);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LoadResource(IntPtr module, IntPtr info);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LockResource(IntPtr data);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint SizeofResource(IntPtr module, IntPtr info);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FreeLibrary(IntPtr module);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate bool ResourceNameCallback(IntPtr module, IntPtr type, IntPtr name, IntPtr param);
    [DllImport("kernel32.dll", EntryPoint = "EnumResourceNamesW", SetLastError = true)]
    private static extern bool EnumResourceNames(
        IntPtr module, IntPtr type, ResourceNameCallback callback, IntPtr param);

    // Inspect published apphost resource data without executing the EXE.
    // RT_GROUP_ICON=14 references RT_ICON=3 resource frames.
    public static int[] ReadIconSizes(string path)
    {
        IntPtr module = LoadLibraryEx(path, IntPtr.Zero, 0x2);
        if (module == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot inspect icon resources.");
        try
        {
            IntPtr first = IntPtr.Zero;
            ResourceNameCallback callback = (h, type, name, param) =>
            {
                first = name;
                return false;
            };
            EnumResourceNames(module, new IntPtr(14), callback, IntPtr.Zero);
            GC.KeepAlive(callback);
            if (first == IntPtr.Zero)
                throw new InvalidOperationException("Published EXE has no RT_GROUP_ICON resource.");
            IntPtr info = FindResource(module, first, new IntPtr(14));
            if (info == IntPtr.Zero)
                throw new InvalidOperationException("Published EXE has no valid icon group.");
            uint length = SizeofResource(module, info);
            IntPtr resource = LoadResource(module, info);
            IntPtr ptr = LockResource(resource);
            if (length < 20 || length > 4096 || ptr == IntPtr.Zero)
                throw new InvalidOperationException("Published EXE icon group size is invalid.");
            byte[] data = new byte[(int)length];
            Marshal.Copy(ptr, data, 0, data.Length);
            int count = BitConverter.ToUInt16(data, 4);
            if (count < 1 || count > 32 || 6 + count * 14 > data.Length)
                throw new InvalidOperationException("Published EXE icon frames are invalid.");
            var sizes = new int[count];
            for (int index = 0; index < count; index++)
            {
                int offset = 6 + index * 14;
                int width = data[offset] == 0 ? 256 : data[offset];
                int height = data[offset + 1] == 0 ? 256 : data[offset + 1];
                int imageId = BitConverter.ToUInt16(data, offset + 12);
                if (width != height || FindResource(
                        module, new IntPtr(imageId), new IntPtr(3)) == IntPtr.Zero)
                    throw new InvalidOperationException("Published EXE icon frame is missing.");
                sizes[index] = width;
            }
            return sizes;
        }
        finally { FreeLibrary(module); }
    }

    public static byte[] Read(string path)
    {
        // LOAD_LIBRARY_AS_DATAFILE: maps PE resources, does not run app code.
        IntPtr module = LoadLibraryEx(path, IntPtr.Zero, 0x2);
        if (module == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot inspect the executable as data.");
        try
        {
            // RT_MANIFEST = 24; CREATEPROCESS embedded application manifest = 1.
            IntPtr info = FindResource(module, new IntPtr(1), new IntPtr(24));
            if (info == IntPtr.Zero)
                throw new InvalidOperationException("No application manifest (#1) in the published executable.");
            uint size = SizeofResource(module, info);
            IntPtr resource = LoadResource(module, info);
            IntPtr ptr = LockResource(resource);
            if (size == 0 || size > 1024 * 1024 || ptr == IntPtr.Zero)
                throw new InvalidOperationException("Embedded application manifest is missing or invalid.");
            byte[] bytes = new byte[(int)size];
            Marshal.Copy(ptr, bytes, 0, bytes.Length);
            return bytes;
        }
        finally { FreeLibrary(module); }
    }
}
'@

$bytes = [BitKeyBridgeManifestProbe]::Read($fullPath)
$stream = [System.IO.MemoryStream]::new($bytes, $false)
$settings = [System.Xml.XmlReaderSettings]::new()
$settings.DtdProcessing = [System.Xml.DtdProcessing]::Prohibit
$settings.XmlResolver = $null
$reader = [System.Xml.XmlReader]::Create($stream, $settings)
try {
    $xml = [System.Xml.XmlDocument]::new()
    $xml.XmlResolver = $null
    $xml.Load($reader)
}
finally {
    $reader.Dispose()
    $stream.Dispose()
}

$manager = [System.Xml.XmlNamespaceManager]::new($xml.NameTable)
$manager.AddNamespace('compat', 'urn:schemas-microsoft-com:compatibility.v1')
$manager.AddNamespace('v3', 'urn:schemas-microsoft-com:asm.v3')
$manager.AddNamespace('dpi', 'http://schemas.microsoft.com/SMI/2016/WindowsSettings')
$manager.AddNamespace('security', 'urn:schemas-microsoft-com:asm.v3')

$os = $xml.SelectSingleNode('//compat:application/compat:supportedOS', $manager)
$expectedOs = '{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}'
if ($null -eq $os -or [string]$os.GetAttribute('Id') -ine $expectedOs) {
    throw 'Published manifest does not declare Windows 10/11.'
}
$dpi = $xml.SelectSingleNode('//v3:application/v3:windowsSettings/dpi:dpiAwareness', $manager)
if ($null -eq $dpi -or $dpi.InnerText.Trim() -cne 'PerMonitorV2,PerMonitor') {
    throw 'Published manifest is missing PerMonitorV2/PerMonitor awareness.'
}
$requested = $xml.SelectSingleNode('//*[local-name()="requestedExecutionLevel"]', $manager)
if ($null -eq $requested -or
    $requested.GetAttribute('level') -ne 'asInvoker' -or
    $requested.GetAttribute('uiAccess') -ne 'false') {
    throw 'Published manifest changed the least-privilege execution policy.'
}

$iconSizes = [BitKeyBridgeManifestProbe]::ReadIconSizes($fullPath)
foreach ($size in @(16, 24, 32, 48, 64, 128, 256)) {
    if ($iconSizes -notcontains $size) {
        throw "Published EXE is missing the branded $size-pixel shell icon."
    }
}

Write-Output 'Embedded Windows manifest and branded 16-256px PE icon: PASS.'
