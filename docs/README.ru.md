# BitKeyBridge — кратко

Это native Windows-версия прежнего PowerShell-инструмента, переписанная на **C# / .NET 10 LTS / WinForms**.

Релиз — **self-contained single-file .NET EXE**, а не NativeAOT. Под «native» здесь имеется в виду, что программа напрямую использует Windows/LDAP/Graph API и не запускает PowerShell. Для WinForms/DirectoryServices это практичнее и проще сопровождать.

В рабочем режиме PowerShell вообще не используется: AD читается напрямую по LDAP, репликация — через .NET DirectoryServices, Microsoft Graph — напрямую по HTTPS/OAuth, сертификаты — через Windows Certificate Store.

## Один EXE

Release собирается как self-contained single-file отдельно для **win-x64**, **win-x86** и **win-arm64**.
Для обычных Windows Server/DC основной вариант — **win-x64**; **win-arm64** предназначен для Windows on ARM, а x86 оставлен для 32-bit Windows client-сценариев.


```text
BitKeyBridge.exe
```

Устанавливать .NET Runtime на сервер не требуется.

Один EXE работает и как GUI, и как CLI для Task Scheduler:

```text
BitKeyBridge.exe --cli
BitKeyBridge.exe --dry-run
BitKeyBridge.exe --dc-test
BitKeyBridge.exe --self-test
BitKeyBridge.exe --cli --force-publish
```

## OU

В публичном GitHub-коде реальные OU организации не зашиты. В GUI выбери **Add OU...**, отметь OU и нажми **Save defaults**. Они сохранятся в:

```text
C:\ProgramData\BitKeyBridge\appsettings.json
```

Если нужно временно выбрать OU только для одного CLI-запуска:

```text
BitKeyBridge.exe --dry-run --search-base "OU=Computers,DC=example,DC=com"
```

Параметр можно повторить несколько раз.

## AD / BitLocker

Приложение:

- динамически обнаруживает актуальные DC;
- не содержит списка dc01/dc02/dc03;
- показывает Site, RODC, GC, IP, OS;
- проверяет состояние входящей AD replication;
- сравнивает количество recovery objects между DC;
- читает `msFVE-RecoveryInformation` напрямую;
- ничего не удаляет и не изменяет в AD;
- проверяет и атомарно заменяет CSV;
- защищает предыдущий CSV от partial/empty export, резкого падения количества строк и случайного изменения OU scope;
- хранит отдельный last-success status.

## Entra / Intune

Есть три режима:

1. **Device Code** — основной интерактивный режим; поддерживает MFA и Conditional Access.
2. **Username + Password (ROPC)** — legacy-вариант; MFA/Conditional Access его часто блокирует.
3. **App Registration + certificate** — режим для unattended/автоматизации.

Поиск в облаке получает metadata. Сам recovery password запрашивается только после **Get Key from Entra**.

### Первый запуск Entra с нуля

Заранее создавать App Registration **не нужно**.

1. Открой вкладку **Entra / Intune Cloud**.
2. Поле **Client ID** можно оставить пустым.
3. Нажми **First-Run / Repair Setup**.
4. Для временного bootstrap BitKeyBridge использует официальный Microsoft public client **Microsoft Graph Command Line Tools**.
5. Откроется Device Code login. Войди администратором Entra и одобри запрошенные management permissions.
6. BitKeyBridge сам создаст/обновит:
   - App Registration **BitKeyBridge**;
   - Enterprise Application;
   - delegated/application Graph permissions;
   - admin-consent grant;
   - локальный certificate с private key.
7. Tenant ID, Client ID и thumbprint сохранятся автоматически. Пароль администратора и access token не сохраняются.

Временные bootstrap permissions `Application.ReadWrite.All`, `AppRoleAssignment.ReadWrite.All` и `DelegatedPermissionGrant.ReadWrite.All` используются только в интерактивной setup-сессии и **не выдаются** рабочему BitKeyBridge App Registration.

Если Conditional Access запрещает Microsoft first-party bootstrap, кнопка **Bootstrap...** позволяет указать tenant-approved public-client Application ID. В обычном случае ничего вручную вводить не надо.

## Важно по безопасности

Recovery CSV содержит секреты. Не клади его в GitHub. Проверь NTFS/share ACL. Если recovery passwords лежат в SYSVOL/NETLOGON, доступ к каталогу должен быть жёстко ограничен; лучше отдельный защищённый share.

Файл с реальными внутренними OU тоже не надо коммитить в публичный репозиторий.


## Новое в 0.2.0

- **Device Code** — основной интерактивный вход в Microsoft Entra; поддерживает MFA и Conditional Access.
- ROPC оставлен как legacy-режим.
- **Unified Devices** объединяет данные компьютера из локального AD, Entra BitLocker metadata и Intune managedDevice.
- Поиск работает по имени ПК, serial number, UPN/пользователю и device ID.
- **Rotate BitLocker Key** отправляет подтверждённый запрос ротации ключа через Intune.
- Вкладка **Audit** пишет локальный JSONL-журнал действий в `%ProgramData%\BitKeyBridge\audit.jsonl`.
- Recovery password никогда не записывается в audit; строки формата 48-digit BitLocker key автоматически заменяются на `[REDACTED-BITLOCKER-KEY]`.
- Native Auto Setup теперь добавляет `DeviceManagementManagedDevices.ReadWrite.All` для delegated и application mode.


## Новое в 0.2.1

- Первый запуск Entra теперь работает без заранее созданного App Registration.
- Встроенный bootstrap использует Microsoft first-party **Microsoft Graph Command Line Tools** через Device Code.
- Ручной Bootstrap Client ID перенесён в advanced fallback **Bootstrap...**.
- Graph permission IDs больше не зашиты жёстко — они определяются по именам из Microsoft Graph service principal.
- Добавлены retry/backoff для Graph throttling/5xx и задержка после создания Enterprise Application.
- После setup приложение автоматически переключается на Device Code и сохраняет созданный Client ID/certificate.


## Новое в 0.3.0

- Добавлена вкладка **Dashboard** с общим состоянием export/service/replication/certificate.
- Добавлен настоящий **Windows Service** без PowerShell и без `sc.exe`.
- Сервис устанавливается в `%ProgramFiles%\BitKeyBridge\BitKeyBridge.exe`, работает как LocalSystem и запускает export по заданному интервалу.
- Добавлен localhost-only health endpoint:
  - `http://127.0.0.1:8750/health`
  - `http://127.0.0.1:8750/health/live`
- Endpoint не содержит recovery keys, паролей, access tokens или private-key данных.
- Добавлены CLI-команды `--health`, `--install-service`, `--uninstall-service`, `--start-service`, `--stop-service`, `--service-status`.
- Добавлен **Secure Output Wizard**: отключает NTFS inheritance, оставляет Full Control SYSTEM/Administrators/current admin, выдаёт указанным группам только Read & Execute и при желании создаёт ограниченный SMB share через Win32 API.
- При закрытии GUI recovery key/password/token очищаются из состояния приложения; clipboard очищается, если в нём всё ещё находится показанный recovery key.


## Новое в 0.4.0

- Добавлена вкладка **Operations**.
- Добавлена проверяемая установка обновлений из GitHub Releases:
  - автоматически выбирается `win-x64` / `win-x86` / `win-arm64`;
  - проверяется `SHA256SUMS.txt`;
  - дополнительно сверяется GitHub asset digest, если он присутствует;
  - распакованный новый EXE обязан пройти `--self-test`;
  - временный elevated updater заменяет GUI/service EXE;
  - при ошибке выполняется rollback из `.bak`.
- Добавлены CLI `--check-update` и `--update`.
- Добавлен Windows Event Log source **BitKeyBridge** в журнал Application.
- Добавлен opt-in **Remote API**:
  - по умолчанию выключен;
  - TLS 1.2/1.3;
  - случайный bearer token показывается один раз;
  - в конфиг сохраняется только SHA-256 токена;
  - firewall rule создаётся только для Domain/Private profiles;
  - recovery passwords через Remote API не выдаются.
- Read-only API: `/api/v1/health`, `/api/v1/service`, `/api/v1/version`.
- Remote management включается отдельно и в 0.4 разрешает только `POST /api/v1/export`.
- Windows Service получает failure-recovery policy: автоматический restart после transient crash.
- Updater cleanup остаётся shell-free и использует Win32 `MoveFileEx`.


## Новое в 0.5.0

- Опционально можно требовать **номер тикета / Reference** перед показом или копированием recovery key.
- Добавлено поле **Reason** — причина доступа к секрету.
- Reference и Reason пишутся отдельными структурированными полями в audit JSONL; сам recovery password туда не попадает.
- Один recovery access context переиспользуется в текущей GUI-сессии для Show/Copy/Get/Rotate и очищается при закрытии программы.
- После получения Entra recovery password BitKeyBridge может напомнить выполнить **Rotate Key in Intune** после завершения восстановления.
- Автоматической ротации нет — действие всегда требует отдельного подтверждения.
- Добавлены CodeQL, Dependabot, CycloneDX SBOM и GitHub provenance/SBOM attestations для релизов.


## Новое в 0.6.0

- BitKeyBridge теперь может запускаться не только на DC, но и на обычной domain workstation или standalone/workgroup Windows-машине.
- Вкладка **Directory Connection** позволяет выбрать:
  - **Auto** — автообнаружение домена/DC и текущие Windows credentials;
  - **Explicit DC** — ручной DC/FQDN для standalone/workstation.
- Для Explicit DC можно задать AD domain, user и пароль только на текущую сессию. Пароль не сохраняется в JSON, audit или Event Log.
- Поддерживаются:
  - LDAP 389 с signing/sealing;
  - LDAPS/TLS, обычно порт 636.
- Добавлена кнопка **Test DC Connection**.
- Export больше не требует, чтобы сама программа работала на DC: output root может быть локальной папкой или UNC path.
- Если новый `OutputRoot` пустой, сохраняется старое поведение с `SysvolScriptsRoot`, поэтому WinPE/NETLOGON-сценарий не ломается.
- CLI получил `--ad-auto`, `--ad-server`, `--ad-domain`, `--ad-user`, `--ad-password-prompt`, `--ad-integrated`, `--ad-port`, `--ad-ldaps`, `--ad-ldap` и `--ad-test`.
- Microsoft 365 / Entra / Intune по-прежнему работает независимо от членства Windows-компьютера в домене.
- CI теперь проверяет, что каждая версия из `.csproj` обязательно имеет секцию в `CHANGELOG.md`.


## Новое в 0.7.0

- Добавлены три режима AD credentials:
  - **Session only** — пароль только в памяти текущего процесса;
  - **Current User / Credential Manager** — Windows Credential Manager текущего пользователя;
  - **Machine / Service / DPAPI** — машинный encrypted vault для unattended сценария.
- Machine Vault хранится в `%ProgramData%\BitKeyBridge\Secrets\ad-machine.cred`, шифруется Windows DPAPI LocalMachine и закрывается ACL только для SYSTEM/Administrators.
- Пароли AD не попадают в `appsettings.json`, audit, Event Log и command line.
- В GUI добавлены Save Credential / Delete Stored и отображение только metadata.
- Windows Service теперь можно переключать между:
  - LocalSystem;
  - gMSA / managed service account;
  - обычным domain service account.
- Для gMSA пароль не нужен и BitKeyBridge его никогда не получает — пароль управляется Active Directory.
- Для обычного service account пароль передается напрямую Windows SCM только при смене identity и BitKeyBridge его не сохраняет.
- Current User vault заблокирован для unattended Windows Service: для службы используй Machine Vault либо integrated credentials + gMSA/domain account.
- CLI: `--vault-status`, `--vault-save-user`, `--vault-save-machine`, `--vault-delete-user`, `--vault-delete-machine`, `--service-identity-local-system`, `--service-identity-gmsa`, `--service-identity-user`.
- Self-test проверяет DPAPI LocalMachine protect/unprotect round-trip.


## Новое в 0.8.0

- Добавлена вкладка **Coverage** для сравнения BitLocker metadata из AD, Entra ID и Intune.
- Coverage не читает `msFVE-RecoveryPassword` и не вызывает Graph endpoint получения самого recovery password.
- Показываются состояния:
  - AD + Entra;
  - только AD;
  - только Entra;
  - recovery key не найден.
- Дополнительно отмечаются:
  - несколько recovery objects;
  - Intune device без encryption;
  - stale Intune sync;
  - старый Entra recovery-key metadata.
- В отчёт входят Intune compliance, last sync, user/UPN, serial, manufacturer/model, OS и encryption state.
- Есть фильтры и экспорт видимых строк в CSV.
- CSV Coverage содержит только metadata — 48-значного recovery password в нём нет.
- Порог stale Intune и старого cloud key настраивается через `CoverageStaleIntuneDays` и `CoverageOldCloudKeyDays`.

## Новое в 0.9.0

- Добавлен CLI-режим `--coverage` для автоматического metadata-only отчёта AD + Entra + Intune.
- За один запуск создаются CSV и JSON; recovery password не запрашивается ни из AD, ни через Graph key-value endpoint.
- Для интерактивного запуска можно использовать Device Code, для unattended/Task Scheduler — certificate authentication.
- Добавлены overrides: `--tenant-id`, `--client-id`, `--cert-thumbprint`, `--cloud-user`, `--cloud-auth`.
- Пути отчётов задаются через `--coverage-output`, `--coverage-csv`, `--coverage-json`; `--coverage-json-stdout` печатает JSON в stdout.
- Для мониторинга добавлены exit codes:
  - 20 — есть устройства без recovery metadata;
  - 21 — Intune сообщает хотя бы одно managed device как not encrypted;
  - 22 — есть stale Intune devices.
- Для этих проверок добавлен offline self-test.

Пример unattended запуска:

```text
BitKeyBridge.exe --coverage --cloud-auth Certificate --tenant-id <tenant-guid> --client-id <app-guid> --cert-thumbprint <thumbprint> --coverage-fail-no-key
```

### Scheduled Coverage / Windows Service

Для unattended режима используется отдельный machine-level cloud config:

```text
BitKeyBridge.exe --cloud-machine-save --tenant-id <tenant-guid> --client-id <app-guid> --cert-thumbprint <thumbprint>
BitKeyBridge.exe --cloud-machine-status
```

Файл:

```text
%ProgramData%\BitKeyBridge\cloud_auth_machine.json
```

содержит только Tenant ID, Client ID, thumbprint и режим `Certificate`. Пароль, access/refresh token и private key туда не записываются; certificate с private key должен находиться в `LocalMachine\My`.

Coverage можно запускать с этим конфигом:

```text
BitKeyBridge.exe --coverage --coverage-machine-config
```

Или включить его прямо в native Windows Service:

```text
BitKeyBridge.exe --service-coverage-enable --service-coverage-interval 1440 --service-coverage-run-on-start
```

У Coverage отдельный interval, поэтому частота обычного AD recovery export не меняется. В Dashboard добавлены соответствующие переключатели и кнопки **Save Cloud for Service / Delete Machine Cloud**. Последний результат Coverage попадает в health snapshot и в `%ProgramData%\BitKeyBridge\coverage_status.json`.

Coverage CSV теперь использует UTC timestamps и нейтрализует значения, похожие на Excel/CSV formula injection.

## Новое в 0.10.0

- Добавлено автоматическое управление ACL private key для certificate из `LocalMachine\My`.
- Поддерживаются CNG keys в `%ProgramData%\Microsoft\Crypto\Keys` и legacy CAPI keys в `RSA\MachineKeys`.
- Для gMSA/domain service account BitKeyBridge добавляет только точечный Read ACE и не заменяет существующий ACL ключа.
- При смене identity службы доступ к certificate проверяется/подготавливается до изменения SCM, если scheduled Coverage зависит от certificate.
- В Dashboard появилась кнопка **Repair Cert Access**.
- CLI:
  - `--cert-key-status`
  - `--cert-key-grant`
  - `--cert-key-revoke`
  - `--cert-account <DOMAIN\\account>`
- Health теперь показывает состояние доступа service identity к private key.
- Добавлен **Coverage Policy Engine** с порогами для No Key, Intune Not Encrypted, Intune Stale и Old Cloud Key.
- Для каждого правила можно выбрать Error / Warning / Info.
- В Coverage появилась кнопка **Policy...**.
- CLI policy поддерживает status, enable/disable, max thresholds и severity.
- `--coverage-fail-policy` возвращает exit code 23 при нарушении policy.
- Policy сохраняется в `coverage_status.json` как структурированный список нарушений.
- Scheduled Coverage пишет severity policy в Windows Event Log.
- Добавлен общий unattended Coverage runner с защитой от одновременного запуска нескольких экземпляров.
- Remote API получил:
  - `GET /api/v1/coverage`
  - `GET /api/v1/coverage/policy`
  - `POST /api/v1/coverage/run` при включённом management.
- Эти API не возвращают recovery password и не отдают device-level recovery secrets.

Пример строгой policy:

```text
BitKeyBridge.exe --coverage-policy-enable --coverage-policy-max-no-key 0 --coverage-policy-severity-no-key Error
```

## Новое в 0.11.0

- Добавлен опциональный **RBAC по Windows users/groups/SID**. По умолчанию он выключен, поэтому upgrade не меняет существующее поведение.
- Права разделены:
  - **RecoveryRead** — поиск локального recovery CSV, Show/Copy AD key, получение/Show/Copy Entra recovery password;
  - **Rotate** — запрос ротации BitLocker key через Intune.
- Local Administrators bypass можно отдельно включить или выключить.
- В **Operations → Helpdesk Recovery Workflow → RBAC...** можно задать группы, проверить их разрешение в SID и сразу увидеть права текущей Windows identity.
- CLI:
  `--rbac-status`, `--rbac-enable`, `--rbac-disable`, `--rbac-admin-bypass`,
  `--rbac-reader-add/remove`, `--rbac-rotator-add/remove`.
- Отказы RBAC пишутся в `audit.jsonl` и Windows Application Event Log без recovery password.
- Health endpoint показывает только состояние RBAC и количество configured principals/errors, но не раскрывает названия групп.
- Новые audit-записи получили **SHA-256 hash chain**. GUI и Windows Service используют cross-process lock, чтобы параллельные записи не ломали цепочку.
- Старые audit lines остаются читаемыми как legacy/unhashed.
- Проверка integrity доступна кнопкой **Verify Chain** во вкладке Audit и командой:

```text
BitKeyBridge.exe --audit-verify
```

- Native Windows Service автоматически проверяет audit chain при старте и затем каждые 24 часа. Результат кэшируется в `%ProgramData%\BitKeyBridge\audit_integrity_status.json`, `/health` показывает `Valid / Invalid / Stale / NeverVerified`, а ошибка integrity попадает в Windows Event Log.

- Self-test теперь проверяет RBAC backward compatibility, валидную audit chain и специально изменённую запись, которая обязана определиться как tampered.
- Hash chain является tamper-evident, но не заменяет внешний immutable/SIEM archive; для высокой гарантии audit лучше пересылать на центральное защищённое хранилище.
## Новое в 0.12.0

- Поверх SHA-256 hash chain добавлены **криптографически подписанные audit checkpoints**.
- Используется отдельный RSA-3072 certificate в `LocalMachine\My`; он не зависит от Entra certificate и Remote API certificate.
- Private key сохраняется как machine key без флага Exportable.
- Signed checkpoint содержит hash текущей головы audit chain, количество записей, версию chain, имя компьютера, thumbprint signing certificate, timestamp и RSA-SHA256-PKCS1 signature.
- Перед заменой существующего signed checkpoint BitKeyBridge сначала проверяет старую подпись и убеждается, что подписанный hash всё ещё присутствует в текущей валидной audit chain. Если anchor пропал или изменён, новый checkpoint не создаётся.
- Это не позволяет Windows Service автоматически «узаконить» уже переписанный audit при следующей суточной проверке.
- CLI:

```text
BitKeyBridge.exe --audit-signing-setup
BitKeyBridge.exe --audit-signing-status
BitKeyBridge.exe --audit-signing-sign
BitKeyBridge.exe --audit-signing-verify
BitKeyBridge.exe --audit-signing-disable
```

- Для setup можно задать срок certificate через `--audit-signing-years 1-10`.
- В Audit tab добавлены **Setup Signing / Sign Now / Verify Signature / Disable Signing**.
- При смене LocalSystem/gMSA/domain service account автоматически выполняется preflight доступа к signing private key.
- Native Windows Service подписывает проверенную audit chain в своём ежедневном integrity cycle.
- `/health` показывает status подписи, expiry certificate, checkpoint time/count и подписана ли текущая голова audit.
- Добавлен warning horizon `AuditSigningCertificateWarningDays`.
- Self-test проверяет RSA signature round-trip и обнаружение изменения подписанного payload.
## Новое в 0.13.0

- Каждый recovery workflow получил `SessionId` / `CorrelationId`. Один ID связывает Get/Show/Copy/Rotate в audit.
- Audit chain обновлена до **version 2**: `CorrelationId` входит в SHA-256 hash новой записи. Старые v1 audit-записи продолжают проходить verification без миграции.
- Для recovery session автоматически создаётся metadata-only incident JSON:

```text
%ProgramData%\BitKeyBridge\Incidents\<SessionId>.json
```

- Incident bundle содержит operator, host, computer, Recovery ID, ticket/reference, reason, timestamps, actions, audit EntryHash и rotation state. 48-digit recovery password туда не записывается.
- В Audit tab добавлена колонка Session.
- Local health разделён на дополнительные безопасные endpoints:

```text
/health/ready
/health/security
/health/coverage
```

- `/health/security` показывает RBAC/audit/certificate/Remote API security posture без token hashes.
- Remote API получил least-privilege bearer tokens: `read`, `coverage-run`, `export`. Старый token остаётся Admin token для backward compatibility.
- GET разрешён любому валидному scope. `POST /coverage/run` требует Admin или CoverageRun; `POST /export` — Admin или Export. Global remote-management switch всё равно обязателен.
- Scoped tokens создаются/отзываются через **Operations → Scoped Tokens...** или CLI:

```text
BitKeyBridge.exe --remote-token-status
BitKeyBridge.exe --remote-token-generate read
BitKeyBridge.exe --remote-token-generate coverage-run
BitKeyBridge.exe --remote-token-generate export
BitKeyBridge.exe --remote-token-revoke read
```

- Добавлены безопасные config backup/restore и diagnostics ZIP:

```text
BitKeyBridge.exe --config-backup C:\Backup\BitKeyBridge.json
BitKeyBridge.exe --config-restore C:\Backup\BitKeyBridge.json
BitKeyBridge.exe --diagnostics-bundle C:\Temp\BitKeyBridge-Diagnostics.zip
```

- Перед restore автоматически создаётся rollback backup. Credential Manager/DPAPI password, Graph tokens, certificate private keys и recovery passwords в backup не включаются.
- Diagnostics ZIP дополнительно исключает recovery CSV, audit contents, bearer tokens и даже их hashes. В нём только sanitized health/service/config/status/log/certificate metadata.
- Добавлен staged **Entra certificate rollover**: новый certificate добавляется через Graph без удаления старого, проверяется app-only authentication, готовится service private-key ACL и только после этого обновляется user/machine config.
- Старый Entra Graph credential и local certificate автоматически не удаляются — они остаются для rollback/grace.
- Добавлен explicit **audit-signing certificate rollover**. Transition подписывается одновременно старым и новым RSA private key; история хранится в `%ProgramData%\BitKeyBridge\AuditSigningTransitions`.
- При обычной ошибке audit rollover BitKeyBridge пытается вернуть предыдущий thumbprint/checkpoint. Старый certificate остаётся для исторической проверки.
- Вся история dual-signed transitions теперь проверяется автоматически: обе подписи, наличие старого/нового certificates, machine identity, chain version и непрерывность old→new thumbprints. Ошибка видна в GUI/CLI и `/health/security`.
- Новые Remote API TLS private keys больше не создаются Exportable.
- При Remote API setup и смене LocalSystem/gMSA/domain service identity выполняется private-key ACL preflight.
- `/health/security` показывает Remote API scopes, TLS expiry/status и key ACL без раскрытия hashes.
- Self-test расширен проверками mixed audit v1→v2, CorrelationId, metadata-only incident bundle, dual-sign transition tamper detection, Remote API scope normalization и config validation.

## Новое в 0.14.0

- Добавлена проверка recovery incident bundle по retained tamper-evident audit:

```text
BitKeyBridge.exe --incident-verify <session-id>
```

- Проверяются audit chain, стабильность snapshot, точные `AuditEntryHash`, `CorrelationId`, action/result/source/auth, operator/host/device/Recovery ID, ticket/reference, reason и rotation state.
- В Audit tab появилась кнопка **Incident...**: можно выбрать recent Session ID, выполнить verification, открыть JSON bundle или папку Incidents.
- Статус `NotFullyRetained` отделён от tamper/mismatch: он означает, что incident старше текущего окна хранения `audit.jsonl` + `.old`.
- Отдельно обнаруживаются missing anchor в retained window, metadata mismatch, invalid audit chain и случайное появление 48-digit recovery password в incident JSON.
- На существующем loopback health listener добавлен Prometheus endpoint:

```text
http://127.0.0.1:8750/metrics
```

- Metrics содержат только числовые operational/security gauges. В них нет computer/user/OU/ticket/Recovery ID, recovery password, bearer token или token hash.
- `appsettings.json` получил явный `SchemaVersion`; текущая schema — v1.
- Старый pre-versioned config перед atomic migration сначала копируется в machine Backups directory.
- Config с более новой неизвестной schema не перезаписывается и не интерпретируется через старые defaults.
- Если read-only/non-admin process не может сохранить migration, effective config продолжает работать в памяти, а migration будет повторена позже; состояние видно в health.
- Migration status включён в sanitized diagnostics bundle.
- Self-test дополнен incident verification/tamper detection, metrics secret-leak check, legacy config migration/backup и future-schema refusal.

