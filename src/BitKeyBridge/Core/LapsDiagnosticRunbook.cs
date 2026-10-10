namespace BitKeyBridge;

/// <summary>
/// Produces operator-facing LAPS troubleshooting commands without running them.
/// Scripts are examples only; no permissions or stored passwords are changed.
/// </summary>
public static class LapsDiagnosticRunbook
{
    public static string Build(LapsReadResult result, bool cloud)
    {
        var lines = new List<string>
        {
            "LAPS DIAGNOSTICS - COMMANDS AND WHERE TO RUN THEM",
            "Run these commands manually. BitKeyBridge does not execute PowerShell.",
            "The default checks are read-only and do not request secret attributes.",
            "Never share passwords, access tokens, encrypted password blobs or unredacted support logs.",
            ""
        };

        if (cloud)
        {
            var device = string.IsNullOrWhiteSpace(result.ComputerId)
                ? result.ComputerName : result.ComputerId;
            if (string.IsNullOrWhiteSpace(device))
                device = "ENTRA-DEVICE-ID";

            lines.AddRange([
                "1. ON THE AFFECTED WINDOWS COMPUTER - PowerShell (elevated to see all policies/events)",
                "gpresult /scope computer /r",
                "Get-WinEvent -LogName 'Microsoft-Windows-LAPS/Operational' -MaxEvents 40 | Select-Object TimeCreated, Id, LevelDisplayName, Message | Format-List",
                "Get-ItemProperty 'HKLM:\\Software\\Microsoft\\Windows\\CurrentVersion\\Policies\\LAPS' -ErrorAction SilentlyContinue | Select-Object BackupDirectory",
                "Event 10022: Entra backup policy. Event 10004: success. Event 10005: processing failure.",
                "",
                "2. ON AN ADMINISTRATOR PC WITH THE LAPS AND MICROSOFT GRAPH POWERSHELL MODULES",
                "Connect-MgGraph -Scopes 'DeviceLocalCredential.ReadBasic.All'",
                "Get-LapsAADPassword -DeviceIds " + Quote(device) +
                    " | Select-Object DeviceName, DeviceId, PasswordExpirationTime",
                "The command above reads metadata ONLY (no -IncludePasswords). " +
                    "A device name may additionally require Device.Read.All; prefer the Entra device ID.",
                "To read passwords, a separate DeviceLocalCredential.Read.All permission and " +
                    "an applicable Entra role are required.",
                "",
                "3. IN MICROSOFT ENTRA / INTUNE ADMIN CENTERS",
                "Confirm the exact device ID, its backup policy, last successful LAPS backup, " +
                    "and consent/role assignments for the identity used by BitKeyBridge.",
                "AD OU ACL commands do NOT apply to Entra LAPS."
            ]);

            return string.Join(Environment.NewLine, lines);
        }

        var computer = string.IsNullOrWhiteSpace(result.ComputerDistinguishedName)
            ? result.ComputerName : result.ComputerDistinguishedName;
        if (string.IsNullOrWhiteSpace(computer))
            computer = "COMPUTER-NAME";

        var dc = string.IsNullOrWhiteSpace(result.DirectoryServer)
            ? "YOUR-DOMAIN-CONTROLLER" : result.DirectoryServer;
        var ou = ParentDn(result.ComputerDistinguishedName);

        lines.AddRange([
            "1. ON THE AFFECTED WINDOWS COMPUTER - PowerShell (elevated for full policy/event information)",
            "gpresult /scope computer /r",
            "Get-WinEvent -LogName 'Microsoft-Windows-LAPS/Operational' -MaxEvents 40 | Select-Object TimeCreated, Id, LevelDisplayName, Message | Format-List",
            "Get-ItemProperty 'HKLM:\\Software\\Microsoft\\Windows\\CurrentVersion\\Policies\\LAPS' -ErrorAction SilentlyContinue | Select-Object BackupDirectory, ADPasswordEncryptionEnabled, ADPasswordEncryptionPrincipal, ADEncryptedPasswordHistorySize",
            "Get-ItemProperty 'HKLM:\\Software\\Microsoft\\Policies\\LAPS' -ErrorAction SilentlyContinue | Select-Object BackupDirectory",
            "Windows LAPS: 10021 = AD backup policy, 10023 = legacy policy, 10004 = success, 10005 = failure.",
            "For legacy AdmPwd also check HKLM:\\Software\\Policies\\Microsoft Services\\AdmPwd.",
            "",
            "2. ON A DC OR DOMAIN ADMIN WORKSTATION WITH RSAT - PowerShell (no password-read request)",
            "Import-Module ActiveDirectory",
            "Import-Module LAPS",
            "Get-ADComputer -Identity " + Quote(computer) +
                " -Server " + Quote(dc) +
                " -Properties 'ms-Mcs-AdmPwdExpirationTime','msLAPS-PasswordExpirationTime' | " +
                "Select-Object Name, DistinguishedName, 'ms-Mcs-AdmPwdExpirationTime', 'msLAPS-PasswordExpirationTime'",
            "This retrieves expiration METADATA, not msLAPS-Password, msLAPS-EncryptedPassword or ms-Mcs-AdmPwd.",
            "Visible metadata does not prove that a password is stored or that read/decrypt access is granted."
        ]);

        if (!string.IsNullOrWhiteSpace(ou))
        {
            lines.Add(
                "Find-LapsADExtendedRights -Identity " + Quote(ou) +
                " -DomainController " + Quote(dc));
            lines.Add(
                "Compare ExtendedRightHolders with the operator's delegated read group. " +
                "Review nested membership, inheritance and explicit deny ACLs on this OU/computer.");
            lines.Add(
                "For LEGACY LAPS only, if the AdmPwd.PS module is installed: " +
                "Find-AdmPwdExtendedRights -Identity " + Quote(ou));
        }
        else
        {
            lines.Add(
                "Resolve the computer's OU with Get-ADComputer first; then run " +
                "Find-LapsADExtendedRights -Identity 'OU=Workstations,DC=example,DC=com' " +
                "(replace with the actual OU).");
        }

        lines.AddRange([
            "",
            "3. ON THE OPERATOR'S OWN WINDOWS PC - Command Prompt or PowerShell",
            "whoami /user",
            "whoami /groups",
            "These show the Windows logon token. If BitKeyBridge uses separate supplied AD credentials, " +
                "the group list will NOT represent those LDAP credentials.",
            "After AD group changes, refresh the sign-in/token before rechecking.",
            "",
            "4. OPTIONAL: ON AN AUTHORIZED RSAT WINDOWS PC OR DC - LIVE PASSWORD-READ TEST",
            "Get-LapsADPassword -Identity " + Quote(computer) +
                " -DomainController " + Quote(dc) +
                " -IncludeHistory | Select-Object ComputerName, Source, DecryptionStatus, " +
                "AuthorizedDecryptor, PasswordUpdateTime, ExpirationTimestamp",
            "CAUTION: The command DOES request passwords, although the displayed output " +
                "excludes the Password property and does not use -AsPlainText.",
            "If encrypted attributes are returned but decryption is unauthorized, " +
                "check BOTH AD read delegation and the configured ADPasswordEncryptionPrincipal.",
            "For legacy LAPS, use the legacy AdmPwd.PS tools only if your environment requires them.",
            "Do NOT run permission-grant, password-reset or password-rotation commands during diagnosis."
        ]);

        return string.Join(Environment.NewLine, lines);
    }

    private static string Quote(string value) =>
        "'" + value.Replace("\r", "", StringComparison.Ordinal)
                   .Replace("\n", "", StringComparison.Ordinal)
                   .Replace("'", "''", StringComparison.Ordinal) + "'";

    private static string ParentDn(string distinguishedName)
    {
        for (var index = 0; index < distinguishedName.Length; index++)
        {
            if (distinguishedName[index] == '\\')
            {
                index++;
                continue;
            }

            if (distinguishedName[index] == ',')
            {
                var parent = distinguishedName[(index + 1)..].TrimStart();
                return parent.StartsWith("OU=", StringComparison.OrdinalIgnoreCase)
                    ? parent : string.Empty;
            }
        }

        return string.Empty;
    }
}
