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
