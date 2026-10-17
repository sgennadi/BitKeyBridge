# BitKeyBridge v0.30.5 - updater ZIP compatibility hotfix

v0.30.4 accidentally included a UI self-test log in public architecture ZIPs.
Strict legacy updater correctly rejected the unexpected extra file. The fixed
packages contain only the tested BitKeyBridge.exe in the ZIP root, validated
for exact name, size, absence of symbolic links, and matching SHA-256.

Use only your correct Windows architecture (x64, x86 or ARM64), and verify
SHA256SUMS.txt from the official published GitHub Release. Automatic unsigned
installation still requires explicit existing consent. No Authenticode signing,
trusted publisher bypass, security-policy change or silent fleet deployment is
introduced. Test on managed AD/Entra/CyberArk endpoints before rollout.
