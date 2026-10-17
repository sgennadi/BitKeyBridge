# Protected Audit Journal - append-only verified archival (v0.30.4)

The SYSTEM-owned protected journal is an evidence log, never a cache.
When `audit.jsonl` reaches the configured rotation threshold, it first
becomes `audit.jsonl.old`. When a second rotation is required, the broker
moves the previously rotated `.old` file into its own inherited-ACL folder:

    %ProgramData%\\BitKeyBridge\\ProtectedAudit\\Archive\\audit-00000001.jsonl
    %ProgramData%\\BitKeyBridge\\ProtectedAudit\\Archive\\audit-00000002.jsonl

Every segment is append-only in normal operation; no retention limit deletes
or overwrites evidence. The broker requires SYSTEM/Administrators NTFS writer
ownership throughout the path, rejects reparse points, unexpected files and
segment numbering gaps, and verifies the entire SHA-256 linked sequence while
holding the journal's existing cross-process lock. A failed move or bad chain
blocks privileged secret reads (fail closed). Non-protected/user logs retain
their prior rotation behavior.

A crash after moving `.old` is recoverable because the numbering and
hash chain include all archived files; the next append inherits the last
record hash from the current journal, rotated journal or final segment.

`--audit-journal-capacity --no-elevation` reports the segment count and
total archive bytes. It only reports VerifiedArchivingReady when NTFS and
chain verification succeed for the querying Windows identity. Ordinary
accounts without protected log access may receive Access Denied; that does
not grant them read access or make auditing optional.

No archive is automatically deleted. Administrators must separately set an
approved long-term retention/export and off-host integrity checkpoint before
removing any evidence. Capacity monitoring should alert on low free disk space,
unexpected growth, hash corruption and broker availability. CI includes
offline fixtures for multi-segment integrity, previous-hash continuity and
missing segment rejection.

No Authenticode signature is added. Technicians should validate this on a
managed disposable Windows endpoint before activating non-admin Helpdesk
recovery broadly.
