# v0.32.0 fleet inventory — staged implementation

This **local read-only** CLI is a foundation for future centralized
monitoring, not a telemetry service or repository of recovery keys.

    BitKeyBridge.exe --fleet-status --no-elevation

The versioned JSON whitelist contains: machine name, UTC timestamp, version,
runtime ID, OS architecture, Windows Service state, configured audit/RBAC,
update consent, auto-install eligibility and rollback hold.

It contains no BitLocker/LAPS recovery passwords, Graph access tokens,
credential material, private keys, tenant identifiers or raw configuration.
The collector sends nothing to any remote endpoint. If SCM cannot be queried,
state is Unknown and the command returns exit code 2.

Use your approved Intune Remediations or GPO inventory mechanism to collect
the output; secure the inventory centrally under normal institutional data
retention policies. This output is *not* a statement of recovery access,
a verified Audit Broker journal or permission to reveal secrets.

## Centrally restricted auto-updates (v0.32.0 foundation)

The program reads HKLM\\SOFTWARE\\Policies\\BitKeyBridge in the native
64-bit registry view (32-bit on 32-bit Windows), with no registry writes.
GPO or Intune device policy can define the following DWORD values:

- DisableAutomaticUpdates = 1: block all automatic installation.
- DisableUnsignedAutomaticUpdates = 1: block the unsigned channel.
- RequireSignedAutomaticUpdates = 1: require an existing configured pinned signer.

Missing values default to zero. Any invalid or unreadable policy denies
automatic installation (fail closed). A policy can only restrict an already
authorized local update configuration; it **cannot** set consent, pin a
publisher, authorize recovery keys or install updates itself.

Use `BitKeyBridge.exe --managed-update-policy --no-elevation` to inspect
effective restrictions. Manual updating remains a distinct administrator
action. Configure a central policy that reflects your environment; do not
silently bypass AppLocker, WDAC or CyberArk EPM.


## v0.32.0 fleet health report additions

`brokerServiceHealth` distinguishes *NotConfigured*, *PolicyIncomplete*,
*ServiceNotInstalled*, *ServiceNotRunningOrUnknown*, *ServiceIdentityUnexpected*
and *ServiceRunningNotVerified*. **Running is not broker readiness**:
the report deliberately cannot claim a durable protected audit ACK or verified
journal chain from the SCM state alone.

`lastUpdateOutcome` is one of *Success*, *Failed*, *Unclassified* or
*UnknownOrNotRecorded*. The last value is also used when protected update
history is unavailable to the collector; it must not be interpreted as a
healthy endpoint. `lastUpdateUtc` is supplied when a recent history entry
was readable. Update history details, publisher metadata, release URLs and
any credential-like values are never included.

This is a versioned local JSON output for an approved management system
to collect. No new network sender, remote control action, writable policy
channel or implicit unsigned auto-install consent is introduced.
