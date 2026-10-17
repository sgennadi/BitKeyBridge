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
