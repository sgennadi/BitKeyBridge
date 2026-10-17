# v0.31.0 Recovery Steps — metadata-only preflight

The **Recovery Steps** button on the Helpdesk tab and the Tools menu
shows seven read-only stages: selected identity, directory connection,
recovery metadata, current Windows RBAC, ticket/reference policy,
protected audit requirement, and explicit recovery action.

No key/password, privileged LDAP attributes, credential, Graph secret
request, audit-broker write, elevation, ticket creation, or rotation is
performed. This preview NEVER authorizes recovery: existing Reveal and
Copy handlers still enforce identity, delegation, optional JIT/approval
and a committed protected audit event before reading any secret.

Cloud entries must retain a source-qualified ENTRA:/INTUNE: identity.
Ambiguous AD/cloud identities block readiness; failed metadata requests
remain Unverified, not proof that no backup exists. The dialog provides
navigation to Start or Access Health, not an automatic recovery action.

Offline self-tests exercise negative identity, source, RBAC, incomplete
metadata and strict broker configurations. The GUI DPI smoke suite now
includes this dialog at compact, 150%, 175% and 200% scaling.
Real AD/Entra/CyberArk, Windows-token and RDP acceptance is still required.

## Context-aware navigation (phase 2)

The selected **Start** device now offers a compact *Recovery Steps* action.
The guide contains explicit **Open BitLocker** and **Open LAPS** choices
for AD objects, or **Open Devices** for source-qualified Entra/Intune objects.
These choices NEVER call Reveal, Copy, LAPS read or Intune rotation.
Open Start and Access Health are always available for diagnostics.

Every route re-checks the exact original Start-row object, the currently
connected AD/Graph source and the current Windows RecoveryRead decision
AFTER closing the dialog. A same-named replacement row is not the same
device and cannot use its prior preflight. Ambiguous AD/cloud identities,
name-only cloud objects, disconnected sources and denied rights cannot
navigate through a guided recovery shortcut. The real individual recovery
handlers remain responsible for ticket validation, approval, durable audit
ACK and secret retrieval. Windows offline tests exercise all negative routes;
the existing UI DPI test covers the buttons.

### Confirmed cloud device identity
Guided Devices navigation additionally rejects Intune/Entra `UNRESOLVED`
search rows and Entra recovery-ID-only metadata (`ENTRA:KEY:`). Only
nonempty, source-qualified `INTUNE:MANAGED:`, `INTUNE:DEVICE:` or
`ENTRA:DEVICE:` device identifiers are eligible. The operator still
needs to verify the exact managedDeviceId before any later rotation.
