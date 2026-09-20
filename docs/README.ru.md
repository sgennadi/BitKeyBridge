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

Есть два режима:

1. Username + Password (ROPC) — временный ручной вариант; MFA/Conditional Access может его блокировать.
2. App Registration + certificate — вариант для автоматизации.

Поиск в облаке получает metadata. Сам recovery password запрашивается только после **Get Key from Entra**.

Native Auto Setup работает без PowerShell и без Microsoft Graph SDK. Для самого первого OAuth-входа нужен Bootstrap Client ID. После этого приложение самостоятельно создаёт/обновляет рабочий App Registration, permissions, consent и сертификат.

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
