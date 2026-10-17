# Access Health 2.0 - source-qualified diagnosis and context handoff

The v0.31.0 Access Health results now include **read-only, source-specific**
suggested next checks. These use the provenance (AD, AD LAPS, Entra, Intune,
Entra LAPS, Windows token RBAC or protected Audit) and explicit HTTP status
on each existing metadata-only check.

Key distinctions:
- A Graph **401** requires a new intended tenant/user/app authentication,
  **403** needs source-specific consent and effective roles,
  **429** needs rate-limiting/backoff, and Graph **5xx** is not a permission
  problem. No automatic consent/roles are created.
- LAPS **metadata read** never proves password decryption rights.
  Encrypted LAPS decrypt failures are differentiated from missing backups.
- An AD BitLocker metadata failure cannot prove there are no recovery objects.
- A healthy Windows service alone cannot prove that the protected audit
  broker returned a durable ACK; ACL/hash chain must remain protected.

When opening Access Health from a selected **non-ambiguous AD Start row**, the
computer name is copied into its *test computer* textbox (only an identifier,
no recovery secret). The diagnostic is not started until explicitly requested.
An ambiguous/cloud selection clears stale AD input and explains that the
device identity must be checked before querying a name in AD. Manually
entered queries remain untouched when there is no selected Start device.

The suggestions are hypotheses/next checks, NOT a claim that a cause was
confirmed; neither this layer nor the guided Recovery Steps changes AD/GPO,
Graph roles, Intune RBAC, Windows ACL, services, tokens or audit policy.
The full recovery authorization and protected broker ACK remain authoritative.
Offline self-test fixtures cover HTTP 401/403/429/503, source distinctions,
NotProbed/OK behavior and safe selected-AD handoff.

## Fast non-secret keyboard navigation

Technicians can use **Ctrl+Shift+H** from any workspace to open metadata-only
Access Health diagnostics and **Ctrl+Shift+R** to open Recovery Steps.
The Tools menu also displays both shortcuts. Neither keyboard shortcut
retrieves, copies or rotates a recovery secret; the existing source and
protected audit gates still apply. Ctrl+1..7 and Ctrl+F remain unchanged.

The global Tools menu captions for **Recovery Steps** and **Access Health**
are translated in EN/RU/HE, retaining the same safe Ctrl+Shift+R/H keyboard
gestures. Switching language does not reconstruct workspace tabs, invalidate
search context, or change effective RBAC/audit policies.
