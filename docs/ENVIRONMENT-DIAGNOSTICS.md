# Workstation readiness and BitLocker diagnostics

Version 0.25.4 adds **Start > Environment Check...**, accessible to a normal
helpdesk operator even when the Administration tab is hidden, and
**Recovery > Diagnostic steps and commands...** when BitLocker recovery
lookup/password retrieval fails or finds no metadata. Start's selected-device
actions also include **BitLocker diagnostics...** when the selected computer
has **zero** recovery keys.

All checks run locally in the portable .NET/WinForms EXE and are read-only.
No PowerShell, RSAT, WinRM, WMI remote service control, firewall changes,
ACL changes, password reads, password rotations or Key Distribution Service
secret/decryption operations are made by the automated readiness probe.
The app performs a read-only native RPC Endpoint Mapper inquiry to determine
which TCP port the DC currently advertises for MS-GKDI v1.0, then attempts
a TCP handshake against that advertised port. It does not scan the RPC range.

## Readiness checks from any Windows workstation

- Identifies Windows OS/architecture and whether an AD joined domain can be confirmed.
  Workgroup PCs can use a configured DC with separate explicit AD credentials.
- Discovers/selects a writable DC, checks its DNS A/AAAA resolution, checks the
  configured LDAP or LDAPS TCP port, and checks TCP 88, 135 and 445 as secondary
  network indicators. Also checks **TCP 1344**, which was observed to be needed
  for encrypted Windows LAPS decryption in one deployment. TCP 1344 is **not**
  the documented fixed/default GKDI port and may be unused on another DC.
- When TCP 135 is reachable, asks Windows RPC Endpoint Mapper for interface
  **MS-GKDI v1.0**, UUID `b9785960-524f-11df-8b6d-83dcded72085`.
  Reports exactly which `ncacn_ip_tcp` port(s) are advertised, then probes
  those specific ports. No dynamic port-range scan is performed. TCP 135 success
  is **not** proof of access to the returned endpoint.
- Probes distinguish **Connected**, **Connection refused** (no listener or
  explicit rejection), **Timed out** (possible dropped/filtered traffic, no
  definitive firewall verdict), and **Unreachable/DNS failure**. A closed
  historical 1344 port is informational unless the mapper currently
  advertises it for GKDI; an unreachable currently advertised GKDI
  port is a warning. Successful TCP connect is still not a GKDI bind,
  permission check or password-decryption test.
- Performs a separate authenticated LDAPv3 **Negotiate** bind using the
  application's connection settings. Standard LDAP uses signing/sealing;
  selected LDAPS uses TLS with the normal certificate/identity validation.
  If the configured AD credentials are unavailable, the bind is reported
  as failed, never silently authenticated with the wrong account.
- For an optional *exact* AD computer name, fetches only BitLocker recovery
  object GUID/creation metadata, and LAPS non-secret expiry/version indicators.
- If Entra/Intune is configured, checks outbound TCP 443 to
  login.microsoftonline.com and graph.microsoft.com. This does not test
  OAuth scopes, app roles, consent, conditional access or Graph API authorization.
- Marks KDS/DPAPI-NG decrypt access, remote DC services, confidential BitLocker
  recovery attribute rights, LAPS password reads and LAPS decrypt rights as
  **NotVerified**, never as successful merely because a port responded.
- Shows colored statuses using shared UiStyle, supports compact/high DPI and
  200% UI layouts, allows cancellation, and can copy a sanitized, secret-free
  report for helpdesk diagnosis.

### Domain controller and network dependencies

| Path | Port | Interpretation |
| --- | --- | --- |
| Client -> AD DNS | TCP/UDP 53 | DNS resolution is tested, but this is not necessarily the DC itself |
| Client -> DC LDAP | TCP 389 | Default signed/sealed LDAP bind |
| Client -> DC LDAPS | TCP 636 | Optional TLS LDAP, requires valid trusted DC certificate |
| Client -> DC KDC | TCP/UDP 88 | Needed in ordinary Kerberos scenarios; readiness probes TCP only |
| Client -> DC RPC mapper | TCP 135 | Endpoint-mapper inquiry for GKDI; a successful TCP handshake is insufficient |
| Client -> DC observed GKDI candidate | TCP 1344 | Previously observed working in this environment; informational if not advertised for GKDI |
| Client -> DC discovered GKDI RPC endpoint | TCP returned by Endpoint Mapper | Probe only port(s) advertised for MS-GKDI; refused/timeout shown separately |
| Client -> DC dynamic RPC | TCP 49152-65535 by default, potentially different | General Windows RPC range; **not** scanned by the application |
| Client -> DC SMB/SYSVOL | TCP 445 | Normal domain functions and policy, not required for plain LDAP recovery reads |
| Client -> cloud (optional) | TCP 443 | Entra/Intune sign-in/Graph, not required for on-prem recovery |

KDS service status on the DC may be trigger-started, not permanently Running.
To diagnose KDS, check the client/DC event logs, Get-Service KdsSvc
on the DC, and a separately authorized decrypt of a real encrypted LAPS blob.
To inspect port ownership on the DC (PowerShell as administrator), use
`Get-NetTCPConnection -State Listen -LocalPort 1344 -ErrorAction SilentlyContinue`.
For an operator workstation, `Test-NetConnection -ComputerName <DC> -Port 1344`
tests only the TCP handshake. A successful test cannot identify the remote
service. On the DC, check the RPC registrations and the endpoint returned
for UUID `b9785960-524f-11df-8b6d-83dcded72085` before creating narrowly
scoped firewall rules. The IANA service-name assignment for TCP 1344 (ICAP)
does not identify which actual RPC/service listener owns that port.
Do not widen domain-controller RPC firewall rules to every workstation;
restrict RPC allowances to approved helpdesk/administrative networks.

## BitLocker troubleshooting

The Recovery screen keeps its yellow status **above** the results grid when:

1. AD connection/OU scope is missing;
2. a local-cache path does not exist;
3. an AD/cache query returns zero recovery records;
4. metadata search fails; or
5. a visible recovery object cannot return the selected password.

The **Diagnostic steps and commands...** dialog is manual and copyable:

- Client workstation: DNS and TCP checks, whoami identity/groups.
- Affected device: Get-BitLockerVolume status (excluding KeyProtector secret
  details), gpresult and relevant BitLocker event-log names.
- DC/RSAT machine: Get-ADComputer and a **one-level, metadata-only**
  Get-ADObject search for msFVE-RecoveryInformation child objects. When a
  recovery-child DN is known, dsacls.exe inspects its ACL.
- Optional, authorized: a narrowly scoped confidential attribute read
  msFVE-RecoveryPassword that prints **True/False only**. The attribute
  really is retrieved into memory; never run it without authorization or
  share raw command/session logs.
- Offline/local CSV: check path, size and timestamp **without printing CSV
  content** (which can contain secrets).
- Entra-only: BitlockerKey.ReadBasic.All metadata query, separate
  BitlockerKey.Read.All authorization/role guidance, network TCP 443 checks.
  AD ACLs do not apply to cloud recovery keys.

A missing recovery object or a filtered password attribute is **not** in itself
proof that AD denied access. Verify computer/OU scope, client backup policy,
DC replication, exact Recovery ID and user/group delegation before remediation.
BitLocker AD plaintext recovery reads **do not require KDS/DPAPI-NG**.
Windows LAPS encrypted password decryption does require that separate mechanism.

The application never executes the displayed PowerShell commands.
RBAC, JIT, two-person approval, mandatory tickets and SIEM remain
opt-in/disabled by default.
