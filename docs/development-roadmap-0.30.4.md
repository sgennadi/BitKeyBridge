# BitKeyBridge v0.30.4 release and future roadmap

**Released baseline:** v0.30.3. This v0.30.4 branch publishes a stable unsigned
Release only after successful GitHub CI and Audit Broker integration, without
Authenticode signing. Real AD/Entra/EPM endpoint acceptance remains required
before wide institutional rollout.

## Implemented for v0.30.4

- Primary tabs: Start, BitLocker, LAPS, Devices (including Microsoft 365), Helpdesk, Diagnostics, and optional Settings; Start is selected on launch
- Background updater with fixed Program Files path and daily privileged Task Scheduler job; accepts explicit machine-level unsigned consent only for sgennadi/BitKeyBridge
- Existing updater verifies official GitHub release metadata, SHA-256, PE architecture, ProductVersion, staged self-test, policy at apply time, and maintains rollback hold
- Protected audit journal metadata capacity preflight; no automatic deletion or overwrite of SYSTEM evidence
- Configuration example and Intune/GPO deployment documentation updated for current schema, security defaults and architecture

## Additional managed pilot acceptance before wide deployment

- GitHub Windows x64/x86/ARM64 compilation, offline regressions and UI scaling
- Real privileged Task Scheduler creation/removal, rejection from portable EXEs, task invocation from LocalSystem and unattended update on a managed pilot
- Service shutdown/restart and rollback, machine security policy revocation between stage and apply, EPM/AppLocker/WDAC behaviour
- Audit Broker two-token real Windows integration; real AD and Entra delegated rights, RDP 100%/150%/175%/200%
- Package integrity and source provenance; ensure no recovery secret is included in logs

## v0.31.0 — usability and search

- Consolidate existing AD, Entra and Intune search into a single metadata-first source-aware search coordinator with cancellation, dedupe and explicit source/error reporting
- Reuse existing RBAC and secret reveal controls; do not cache or read recovery secrets during metadata search
- Unified recovery flow: search -> check permission -> enter ticket -> retrieve -> committed audit -> optional rotation
- Compact keyboard navigable UI, persistent search context, accessibility/HiDPI and English/Russian/Hebrew RTL resource localization
- Consolidated Access Health explanations, without granting permissions or lowering security

## v0.32.0 — enterprise administration

- Signed-or-policy-verified central read-only deployment configuration with strict precedence and schema migrations; no remote arbitrary command execution
- GPO/Intune package and detection improvements, controlled per-machine update rollout rings with explicit admin approval for unsigned updates
- Central health reporting for versions, broker readiness, updates and SIEM queue; **never centralize secrets or plaintext recovery keys**
- Optional incident reference integration with ticket systems via least-privilege APIs (do not transmit actual LAPS/BitLocker secrets)

**Rule:** full functionality requires real domain/tenant acceptance. Windows CI
and these development commits do not prove production success.
