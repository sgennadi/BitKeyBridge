namespace BitKeyBridge;

public sealed class BitLockerDiagnosticContext
{
    public string SearchQuery { get; init; } = string.Empty;
    public string ComputerDistinguishedName { get; init; } = string.Empty;
    public string RecoveryDistinguishedName { get; init; } = string.Empty;
    public string RecoveryId { get; init; } = string.Empty;
    public string DirectoryServer { get; init; } = string.Empty;
    public string Domain { get; init; } = string.Empty;
    public string ScopeDistinguishedName { get; init; } = string.Empty;
    public string LocalCachePath { get; init; } = string.Empty;
    public bool LocalCache { get; init; }
    public bool ProtectedCache { get; init; }
    public bool Entra { get; init; }
    public int? RecordsReturned { get; init; }
}

/// <summary>
/// Copyable troubleshooting instructions, never executed by BitKeyBridge.
/// The default commands are metadata-only; optional secret-attribute probes
/// are explicitly labeled and display only booleans.
/// </summary>
public static class BitLockerDiagnosticRunbook
{
    public static string Build(BitLockerDiagnosticContext context)
    {
        var lines = new List<string>
        {
            "BITLOCKER DIAGNOSTIC COMMANDS - MANUAL, READ-ONLY CHECKS",
            "Nothing is executed by BitKeyBridge. Default steps do not read recovery passwords.",
            "Do not share actual 48-digit recovery passwords, authorization tokens, or unredacted logs.",
            "Recovery ID: " + (string.IsNullOrWhiteSpace(context.RecoveryId) ? "(not selected)" : context.RecoveryId),
            "Selected search scope: " + (string.IsNullOrWhiteSpace(context.ScopeDistinguishedName) ? "(none)" : context.ScopeDistinguishedName),
            "Returned recovery records: " + (context.RecordsReturned?.ToString() ?? "not tested"),
            ""
        };

        if (context.LocalCache)
        {
            var cache = string.IsNullOrWhiteSpace(context.LocalCachePath)
                ? @"C:\ProgramData\BitKeyBridge\RecoveryExport\recovery.csv"
                : context.LocalCachePath;
            if (context.ProtectedCache)
            {
                lines.AddRange([
                    "WHERE: workstation running BitKeyBridge",
                    "Test-Path -LiteralPath " + Quote(cache),
                    "Get-Item -LiteralPath " + Quote(cache) + " -ErrorAction SilentlyContinue | Select-Object FullName, Length, LastWriteTime",
                    "This is a .bkb DPAPI CurrentUser-encrypted sidecar, not a readable CSV.",
                    "Only the Windows user who created it can decrypt it. Do NOT use Import-Csv or Get-Content to try to expose it.",
                    "Original plaintext export CSV was not removed by the migration; handle it separately."
                ]);
                return string.Join(Environment.NewLine, lines);
            }

            lines.AddRange([
                "WHERE: workstation running BitKeyBridge - PowerShell (no AD/DC connection required)",
                "Test-Path -LiteralPath " + Quote(cache),
                "Get-Item -LiteralPath " + Quote(cache) + " -ErrorAction SilentlyContinue | Select-Object FullName, Length, LastWriteTime",
                "Confirm the configured cache exists, is recent, and is readable by this Windows identity.",
                "Do NOT use Import-Csv/Get-Content to copy a recovery-export CSV into support logs: it can contain secret recovery passwords.",
                "A zero-result cache search does not indicate missing AD rights. Switch to Live AD and search the exact computer/Recovery ID.",
                "If AD lookup also fails, open Environment Check from Start to test DC DNS, LDAP port and authenticated bind."
            ]);
            return string.Join(Environment.NewLine, lines);
        }

        if (context.Entra)
        {
            lines.AddRange([
                "WHERE: authorized Entra administrator PC - PowerShell with Microsoft Graph SDK",
                "Connect-MgGraph -Scopes 'BitlockerKey.ReadBasic.All'",
                "Invoke-MgGraphRequest -Method GET -Uri 'https://graph.microsoft.com/v1.0/informationProtection/bitlocker/recoveryKeys?$top=10'",
                "The GET above lists metadata only. It does NOT request the confidential key property.",
                "To read recovery keys the operator/app needs BitlockerKey.Read.All and applicable Entra role/consent.",
                "Verify the correct Entra device ID and recovery-key ID. Intune enrollment and recovery-key upload are separate.",
                "WHERE: workstation running BitKeyBridge",
                "Test-NetConnection graph.microsoft.com -Port 443",
                "Test-NetConnection login.microsoftonline.com -Port 443",
                "Cloud 443 reachability does not prove Graph permissions or token claims.",
                "AD OU delegation and DC LDAP ports do not control Entra BitLocker recovery access."
            ]);
            return string.Join(Environment.NewLine, lines);
        }

        var dc = string.IsNullOrWhiteSpace(context.DirectoryServer)
            ? string.IsNullOrWhiteSpace(context.Domain) ? "YOUR-WRITABLE-DC" : context.Domain
            : context.DirectoryServer;

        lines.AddRange([
            "WHERE: workstation running BitKeyBridge - PowerShell",
            "Resolve-DnsName " + Quote(dc),
            "Test-NetConnection " + Quote(dc) + " -Port 389",
            "Test-NetConnection " + Quote(dc) + " -Port 636",
            "whoami /user",
            "whoami /groups",
            "The authenticated LDAP bind is a separate check; a reachable TCP port does not prove access.",
            "If BitKeyBridge uses another AD account, whoami describes the WINDOWS identity, not the supplied LDAP credentials.",
            "BitLocker AD recovery keys use LDAP/LDAPS; KDS/DPAPI-NG is NOT required to read the ordinary msFVE-RecoveryPassword attribute.",
            "",
            "WHERE: affected Windows computer - PowerShell (elevated for complete policy/event visibility)",
            "Get-BitLockerVolume -MountPoint $env:SystemDrive | Select-Object MountPoint, VolumeStatus, ProtectionStatus, EncryptionMethod",
            "gpresult /scope computer /r",
            "Get-WinEvent -ListLog '*BitLocker*' | Select-Object LogName, RecordCount",
            "Get-WinEvent -LogName 'Microsoft-Windows-BitLocker-API/Management' -MaxEvents 30 -ErrorAction SilentlyContinue | Select-Object TimeCreated, Id, LevelDisplayName, Message | Format-List",
            @"Get-ItemProperty 'HKLM:\SOFTWARE\Policies\Microsoft\FVE' -ErrorAction SilentlyContinue | Format-List",
            "Inspect the BitLocker-API/Management events and BitLocker GPO backup settings for AD DS backup/escrow failures.",
            "A locally encrypted drive does not establish that its recovery key was successfully backed up.",
            "",
            "WHERE: DC or domain management workstation WITH RSAT ActiveDirectory module - PowerShell",
            "Import-Module ActiveDirectory",
            "$dc = " + Quote(dc)
        ]);
        if (!string.IsNullOrWhiteSpace(context.ScopeDistinguishedName))
        {
            lines.Add("$selectedScopeDN = " + Quote(context.ScopeDistinguishedName));
            lines.Add("Get-ADObject -Identity $selectedScopeDN -Server $dc | Select-Object Name, DistinguishedName");
            lines.Add("Confirm the target computer is inside this selected search scope; otherwise broaden/adjust the scope before concluding no backup.");
        }

        var computerDn = context.ComputerDistinguishedName;
        if (!string.IsNullOrWhiteSpace(computerDn))
        {
            lines.Add("$computerDN = " + Quote(computerDn));
            lines.Add("Get-ADObject -Identity $computerDN -Server $dc | Select-Object Name, DistinguishedName");
        }
        else if (!string.IsNullOrWhiteSpace(context.SearchQuery) &&
                 !Guid.TryParse(context.SearchQuery, out _))
        {
            lines.Add("$computer = Get-ADComputer -Identity " + Quote(context.SearchQuery) +
                      " -Server $dc");
            lines.Add("$computerDN = $computer.DistinguishedName");
        }
        else
        {
            lines.Add("$computerDN = 'CN=EXACT-PC,OU=Workstations,DC=example,DC=com' # REPLACE with the actual computer DN");
            lines.Add("A Recovery ID alone does not identify a computer reliably here. Resolve the exact computer DN before this query.");
        }

        lines.AddRange([
            "$recoveryObjects = @(Get-ADObject -SearchBase $computerDN -SearchScope OneLevel " +
                "-LDAPFilter '(objectClass=msFVE-RecoveryInformation)' -Server $dc -Properties whenCreated)",
            "$recoveryObjects | Select-Object Name, DistinguishedName, whenCreated",
            "'Visible recovery objects: ' + $recoveryObjects.Count",
            "This queries recovery object METADATA only (not msFVE-RecoveryPassword).",
            "0 visible objects can mean: no AD backup, wrong computer/OU, restricted object visibility, or replication lag.",
            "Check the real computer OU, GPO requiring AD DS escrow and the writable DC that holds the backup.",
            ""
        ]);

        if (!string.IsNullOrWhiteSpace(context.RecoveryDistinguishedName))
        {
            lines.Add(
                "WHERE: authorized AD administrator - inspect recovery-object ACL (no key read)");
            lines.Add("dsacls.exe " + Quote(context.RecoveryDistinguishedName));
            lines.Add("Compare configured rights for the actual operator's AD principal and group membership.");
            lines.Add("");
            lines.Add("OPTIONAL: authorized read-right verification (this DOES retrieve the password attribute into memory)");
            lines.Add(
                "$probe = Get-ADObject -Identity " + Quote(context.RecoveryDistinguishedName) +
                " -Server $dc -Properties 'msFVE-RecoveryPassword'");
            lines.Add(
                "[bool](-not [string]::IsNullOrWhiteSpace($probe.'msFVE-RecoveryPassword'))");
            lines.Add("Remove-Variable probe -ErrorAction SilentlyContinue");
            lines.Add("This prints only True/False, but the secret WAS requested; do not log raw objects or values.");
        }
        else if (!string.IsNullOrWhiteSpace(computerDn))
        {
            lines.Add("WHERE: authorized AD administrator - inspect computer ACL and inherited delegation");
            lines.Add("dsacls.exe " + Quote(computerDn));
            lines.Add(
                "For the exact recovery child object, use dsacls.exe on its DN once metadata search locates it.");
        }

        lines.AddRange([
            "",
            "For a missing recovery password, distinguish three conditions:",
            "1. No recovery child object visible (backup/scope/replication or object-read issue).",
            "2. Recovery child metadata visible, but msFVE-RecoveryPassword not returned (possibly missing confidential-attribute read rights).",
            "3. Recovery ID mismatch or recovery password format invalid (wrong object or stale/invalid backup).",
            "Do not interpret a metadata-only query as an effective permission test.",
            "Do not grant All Extended Rights or rotate/re-backup a key automatically during diagnosis."
        ]);
        return string.Join(Environment.NewLine, lines);
    }

    private static string Quote(string value) =>
        "'" + value.Replace("\r", "", StringComparison.Ordinal)
                   .Replace("\n", "", StringComparison.Ordinal)
                   .Replace("'", "''", StringComparison.Ordinal) + "'";
}
