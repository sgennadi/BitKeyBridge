# Runtime update signer pin

`TrustedUpdateSignerThumbprint` is an optional exact SHA-1 certificate thumbprint in the administrator-owned `appsettings.json`. Provision it from the independently verified organizational signing certificate, not from an untrusted downloaded EXE. Whitespace, colons and letter case are normalized; malformed or different pins are rejected.

When set, every existing `AuthenticodeVerificationService.EnforcePolicy` call requires successful Windows `WinVerifyTrust` verification and the matching leaf signer from that same verification state. `X509CertificateLoader` loads only that certificate's DER data; it does not open the PE file or decide signature trust. The native trust state is closed in `finally`. File SHA-256 checks remain separate.

Existing publisher-name requirements continue to apply. Empty pins preserve the previous signature/unsigned opt-in behavior. A pin never grants unsigned consent, and unsigned unattended mode is disabled when a pin is present. For unattended signed updates retain `RequireTrustedUpdateSignature=true` and the existing configured publisher. Coordinate certificate rotation before changing the pin. This change does not provision a certificate or enable updates.

The verifier preserves the existing Windows revocation-check policy; this change does not introduce online revocation checks.

Regression coverage links the production verifier into `tests/AuthenticodeRegression`. Offline cases validate pin syntax. The ephemeral Windows signing integration verifies signed x64/x86/ARM64 PE payloads, wrong pins, unsigned files and modified signed files. Native API execution and interactive WinForms/RDP checks require Windows.
