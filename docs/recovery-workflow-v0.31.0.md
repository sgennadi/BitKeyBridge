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
