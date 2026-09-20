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
