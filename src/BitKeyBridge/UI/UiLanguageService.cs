using System.Globalization;

namespace BitKeyBridge;

/// <summary>
/// Per-user, non-security UI language preference. This first increment
/// localizes the primary shell/navigation only; detailed workflows can
/// migrate to these same resource keys without rewriting authorization.
/// </summary>
internal static class UiLanguageService
{
    internal sealed record Language(string Code, string DisplayName);
    internal sealed class Preferences
    {
        public string Code { get; set; } = "en";
    }

    internal static IReadOnlyList<Language> SupportedLanguages { get; } =
    [
        new("en", "English"),
        new("ru", "Русский"),
        new("he", "עברית")
    ];

    private static readonly IReadOnlyDictionary<string, string[]> Messages =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["Start"] = ["Start", "Главная", "התחלה"],
            ["BitLocker"] = ["BitLocker", "BitLocker", "BitLocker"],
            ["LAPS"] = ["LAPS", "LAPS", "LAPS"],
            ["Devices"] = ["Devices", "Устройства", "מכשירים"],
            ["Helpdesk"] = ["Helpdesk", "Поддержка", "תמיכה"],
            ["Diagnostics"] = ["Diagnostics", "Диагностика", "אבחון"],
            ["Settings"] = ["Settings", "Настройки", "הגדרות"],
            ["Tools"] = ["Tools", "Инструменты", "כלים"],
            ["Language"] = ["Language", "Язык", "שפה"],
            ["DevicesConfigureCloud"] = ["Configure Cloud...", "Настроить облако...", "הגדרת ענן..."],
            ["HelpdeskDeploymentProgressTemplate"] =
                ["{0} of {1} steps marked complete",
                 "Отмечено {0} из {1} этапов",
                 "סומנו {0} מתוך {1} שלבים"],
            ["HelpdeskStatusNotInstalled"] = ["not installed", "не установлена", "לא מותקן"],
            ["HelpdeskStatusUnavailableQuery"] = ["unavailable (query failed)", "недоступно (ошибка запроса)", "לא זמין (השאילתה נכשלה)"],
            ["HelpdeskStatusDisabled"] = ["disabled in configuration", "отключено в настройках", "מושבת בתצורה"],
            ["HelpdeskStatusAuditOptional"] = ["enabled; strict audit not required", "включено; строгий аудит необязателен", "מופעל; ביקורת מחמירה אינה נדרשת"],
            ["HelpdeskStatusAuditStrict"] = ["enabled; strict audit required (runtime unverified)", "включено; требуется строгий аудит (работа не проверена)", "מופעל; נדרשת ביקורת מחמירה (לא אומתה בזמן ריצה)"],
            ["HelpdeskStatusAclVerified"] = ["protected ACL verified", "защищённый ACL проверен", "הרשאות ACL מוגנות אומתו"],
            ["HelpdeskStatusAclUntrusted"] = ["not verified / untrusted", "не проверено / нет доверия", "לא אומת / אינו מהימן"],
            ["HelpdeskStatusVerifyUnavailable"] = ["verification unavailable", "проверка недоступна", "האימות אינו זמין"],
            ["HelpdeskStatusNotChecked"] = ["not checked", "не проверено", "לא נבדק"],
            ["HelpdeskStatusChainValid"] = ["hash chain valid", "цепочка хешей корректна", "שרשרת הגיבובים תקינה"],
            ["HelpdeskStatusChainInvalid"] = ["hash chain invalid or unavailable", "цепочка хешей некорректна или недоступна", "שרשרת הגיבובים שגויה או אינה זמינה"],
            ["HelpdeskStatusNoReadAccess"] = ["unavailable (insufficient access or I/O error)", "недоступно (нет прав или ошибка ввода-вывода)", "לא זמין (אין הרשאה או שגיאת קלט/פלט)"],
            ["HelpdeskStatusConnected"] = ["Connected", "Подключено", "מחובר"],
            ["HelpdeskStatusDisconnected"] = ["Not connected", "Не подключено", "לא מחובר"],
            ["HelpdeskStatusAllowed"] = ["Allowed", "Разрешено", "מותר"],
            ["HelpdeskStatusDenied"] = ["Denied", "Запрещено", "נדחה"],
            ["Search"] = ["Search", "Поиск", "חיפוש"],
            ["Cancel"] = ["Cancel", "Отмена", "ביטול"],
            ["UpdateCenterTitle"] = ["Update Center 2.0", "Центр обновлений 2.0", "מרכז עדכונים 2.0"],
            ["UpdateCenterSubtitle"] =
                ["Versions, update policy, last result and scheduled task, without connecting to GitHub.",
                 "Версии, политика, история и задание планировщика без подключения к GitHub.",
                 "גרסאות, מדיניות, היסטוריה ומשימה מתוזמנת ללא חיבור ל-GitHub."],
            ["UpdateCenterRefresh"] = ["Refresh status", "Обновить статус", "רענון מצב"],
            ["UpdateCenterRetry"] = ["Retry check", "Проверить снова", "בדיקה חוזרת"],
            ["UpdateCenterPause"] = ["Pause auto-install", "Приостановить", "השהיית התקנה"],
            ["UpdateCenterEnable"] = ["Enable auto-install", "Включить установку", "הפעלת התקנה"],
            ["UpdateCenterCompact"] = ["Compact layout", "Компактный вид", "תצוגה מצומצמת"],
            ["UpdateCenterVersion"] = ["Installed", "Установлено", "מותקן"],
            ["UpdateCenterLatest"] = ["Last checked", "Последняя проверенная", "גרסה שנבדקה"],
            ["UpdateCenterChannel"] = ["Channel", "Канал", "ערוץ"],
            ["UpdateCenterLastCheck"] = ["Checked", "Проверено", "נבדק"],
            ["UpdateCenterLastResult"] = ["Last update", "Последнее обновление", "עדכון אחרון"],
            ["UpdateCenterTask"] = ["Scheduled task", "Задание", "משימה מתוזמנת"],
            ["UpdateCenterNextRun"] = ["Next run", "Следующий запуск", "הרצה הבאה"],
            ["UpdateCenterNever"] = ["Never / unknown", "Нет данных", "לא ידוע"],
            ["UpdateCenterStable"] = ["Stable", "Стабильный", "יציב"],
            ["UpdateCenterPreview"] = ["Prerelease (opt-in)", "Предварительный", "גרסת קדם"],
            ["UpdateCenterChecksOff"] =
                ["Automatic checks disabled", "Автопроверка отключена", "בדיקה אוטומטית כבויה"],
            ["UpdateCenterInstallOff"] =
                ["Auto-install paused", "Автоустановка приостановлена", "התקנה אוטומטית מושהית"],
            ["UpdateCenterHeld"] =
                ["Blocked: update/rollback hold; review and explicitly resume",
                 "Блокировка после ошибки/отката — проверьте причину перед возобновлением",
                 "חסימה בעקבות כשל או שחזור — יש לבדוק לפני חידוש"],
            ["UpdateCenterPolicyBlocked"] =
                ["Blocked: signature, unsigned consent or GPO restriction",
                 "Заблокировано: подпись, разрешение неподписанных версий или GPO",
                 "חסימה: חתימה, הסכמה לעדכון לא חתום או מדיניות GPO"],
            ["UpdateCenterInstallReady"] =
                ["Auto-install enabled under active trust/GPO policy",
                 "Автоустановка разрешена действующей политикой доверия/GPO",
                 "התקנה אוטומטית מותרת לפי מדיניות האמון וה-GPO"],
            ["UpdateCenterTaskSafe"] =
                ["Installed: verified SYSTEM task", "Установлено: SYSTEM-задание проверено",
                 "מותקן: משימת SYSTEM אומתה"],
            ["UpdateCenterTaskMissing"] =
                ["Not installed", "Не установлено", "לא מותקן"],
            ["UpdateCenterTaskUnsafe"] =
                ["WARNING: unexpected task action", "ВНИМАНИЕ: задание изменено",
                 "אזהרה: המשימה שונתה"],
            ["UpdateCenterLastTask"] =
                ["Last task run", "Последний запуск задания", "הפעלת משימה אחרונה"],
            ["UpdateCenterTaskResult"] =
                ["Task result", "Результат задания", "תוצאת המשימה"],
            ["UpdateCenterTrust"] = ["Trust", "Доверие", "אמון"],
            ["UpdateCenterNoHistory"] =
                ["None recorded", "История отсутствует", "אין היסטוריה"],
            ["UpdateCenterInstallAction"] = ["Install", "Установка", "התקנה"],
            ["UpdateCenterRollbackAction"] = ["Rollback", "Откат", "שחזור"],
            ["UpdateCenterSuccess"] = ["Success", "Успешно", "הצלחה"],
            ["UpdateCenterFailed"] = ["Failed", "Ошибка", "כשל"],
            ["UpdateCenterLastSuccess"] =
                ["Last successful update", "Последнее успешное обновление", "עדכון מוצלח אחרון"],
            ["UpdateCenterLastFailure"] =
                ["Last failed update", "Последняя ошибка обновления", "כשל עדכון אחרון"],
            ["ConnectionSettings"] =
                ["Open Start / connection settings",
                 "Открыть главную / настройки подключения",
                 "פתיחת התחלה / הגדרות חיבור"],
            // New recovery guide: first translate navigation and step titles.
            // Long directory-provider diagnostic descriptions remain technical
            // English until the complete localized resource rollout.
            ["RecoveryStepsShortcut"] =
                ["Recovery Steps (Ctrl+Shift+R)",
                 "Этапы восстановления (Ctrl+Shift+R)",
                 "שלבי שחזור (Ctrl+Shift+R)"],
            ["AccessHealthShortcut"] =
                ["Access Health (Ctrl+Shift+H)",
                 "Проверка доступа (Ctrl+Shift+H)",
                 "בדיקת הרשאות (Ctrl+Shift+H)"],
            ["RecoveryDialogTitle"] =
                ["Recovery Steps - Read-only Preflight", "Восстановление — проверка",
                 "שחזור — בדיקה מקדימה"],
            ["RecoveryDialogHeading"] =
                ["Recovery steps — no passwords or keys requested",
                 "Этапы восстановления — без запроса паролей и ключей",
                 "שלבי שחזור — ללא בקשה לסיסמאות או למפתחות"],
            ["RecoveryDeviceLabel"] = ["Device:", "Устройство:", "מכשיר:"],
            ["RecoverySourceLabel"] = ["Source:", "Источник:", "מקור:"],
            ["RecoveryOpenStart"] = ["Open Start", "Открыть главную", "פתח התחלה"],
            ["RecoveryAccessHealth"] = ["Access Health", "Проверка доступа", "בדיקת הרשאות"],
            ["RecoveryOpenBitLocker"] = ["Open BitLocker", "Открыть BitLocker", "פתח BitLocker"],
            ["RecoveryOpenLaps"] = ["Open LAPS", "Открыть LAPS", "פתח LAPS"],
            ["RecoveryOpenDevices"] = ["Open Devices", "Открыть устройства", "פתח מכשירים"],
            ["RecoveryClose"] = ["Close", "Закрыть", "סגור"],
            ["RecoveryStep1"] = ["1. Exact device identity", "1. Точная идентификация устройства",
                                 "1. זיהוי מדויק של המכשיר"],
            ["RecoveryStep2"] = ["2. Directory source", "2. Источник каталога",
                                 "2. מקור ספרייה"],
            ["RecoveryStep3"] = ["3. BitLocker/LAPS metadata", "3. Метаданные BitLocker/LAPS",
                                 "3. נתוני BitLocker/LAPS"],
            ["RecoveryStep4"] = ["4. Windows RecoveryRead RBAC", "4. Права Windows RecoveryRead",
                                 "4. הרשאות Windows RecoveryRead"],
            ["RecoveryStep5"] = ["5. Ticket / reference", "5. Обращение / номер заявки",
                                 "5. פנייה / מספר קריאה"],
            ["RecoveryStep6"] = ["6. Durable recovery audit", "6. Защищённый аудит восстановления",
                                 "6. רישום ביקורת מוגן"],
            ["RecoveryStep7"] = ["7. Explicit reveal / copy / rotation", "7. Просмотр / копирование / ротация",
                                 "7. הצגה / העתקה / החלפה"],
            ["RecoveryStatusOk"] = ["OK", "OK", "תקין"],
            ["RecoveryStatusAction"] = ["Action required", "Требуется действие", "נדרשת פעולה"],
            ["RecoveryStatusUnverified"] = ["Unverified", "Не проверено", "לא אומת"],
            ["RecoveryStatusNotTested"] = ["Not tested", "Не проверялось", "לא נבדק"],
            ["RecoveryStatusInfo"] = ["Info", "Информация", "מידע"],
            ["RecoveryStatusRequired"] = ["Required at retrieval", "Обязательно при чтении", "נדרש בעת קריאה"],
            ["RecoveryStatusOptional"] = ["Optional", "Необязательно", "אופציונלי"],
            ["RecoveryStatusPendingAck"] = ["Pending protected ACK", "Ожидается подтверждение аудита",
                                           "ממתין לאישור ביקורת מוגן"],
            ["RecoveryStatusPolicy"] = ["Policy-dependent", "По политике", "בהתאם למדיניות"],
            ["RecoveryStatusExplicit"] = ["Explicit action only", "Только по команде", "פעולה מפורשת בלבד"],
            // Technician-first metadata errors. No secrets, device names or
            // server exception details are sent through localization.
            ["MetadataPartial"] = ["Partial search", "Частичный поиск", "חיפוש חלקי"],
            ["MetadataVisible"] = ["visible result(s).", "видимых результатов.", "רשומות גלויות."],
            ["MetadataSeeDiagnostics"] = ["See diagnostics.", "См. диагностику.", "פרטים באבחון."],
            ["MetadataNoAdRecords"] =
                ["No matching record visible. Verify device, source and scope; backup absence is not proven.",
                 "Совпадений не видно. Проверьте устройство, источник и область поиска; отсутствие резервной копии не доказано.",
                 "לא נמצאה רשומה תואמת. בדקו מכשיר, מקור וטווח חיפוש; אין בכך הוכחה שאין גיבוי."],
            ["MetadataNoCloudRecords"] =
                ["No matching Entra/Intune record visible. Verify tenant, identity and scope; backup absence is not proven.",
                 "В Entra/Intune не видно совпадений. Проверьте клиентскую организацию, идентификатор и область; отсутствие резервной копии не доказано.",
                 "לא נמצאה רשומת Entra/Intune תואמת. בדקו דייר, זהות והיקף; אין בכך הוכחה שאין גיבוי."],
            ["MetadataBackupUnverified"] =
                ["No backup absence is proven.", "Отсутствие резервной копии не доказано.", "לא הוכח שאין גיבוי."],
            ["MetadataKindUnknown"] = ["Lookup failed", "Ошибка поиска", "החיפוש נכשל"],
            ["MetadataKindNoRecord"] = ["No record visible", "Запись не видна", "לא נמצאה רשומה גלויה"],
            ["MetadataKindAccessDenied"] = ["Access denied", "Доступ запрещён", "הגישה נדחתה"],
            ["MetadataKindSignInRequired"] = ["Sign-in required", "Требуется вход", "נדרשת התחברות"],
            ["MetadataKindSourceOffline"] = ["Source unavailable", "Источник недоступен", "המקור אינו זמין"],
            ["MetadataKindTimeout"] = ["Timeout", "Превышено время ожидания", "תם זמן ההמתנה"],
            ["MetadataKindDecryptionFailed"] = ["Decryption failed", "Ошибка расшифровки", "הפענוח נכשל"],
            ["MetadataKindRateLimited"] = ["Source throttled", "Ограничение запросов", "המקור מגביל בקשות"],
            ["MetadataActionUnknown"] =
                ["Open sanitized diagnostics.", "Откройте безопасную диагностику.", "פתחו אבחון ללא פרטים רגישים."],
            ["MetadataActionNoRecord"] =
                ["Check device identity and scope.", "Проверьте идентификатор и область.", "בדקו זהות מכשיר והיקף."],
            ["MetadataActionAccessDenied"] =
                ["Check rights in Access Health.", "Проверьте права в Access Health.", "בדקו הרשאות ב-Access Health."],
            ["MetadataActionSignInRequired"] =
                ["Reconnect the intended account.", "Подключитесь нужной учётной записью.", "התחברו מחדש עם החשבון המתאים."],
            ["MetadataActionSourceOffline"] =
                ["Check source connectivity.", "Проверьте доступность источника.", "בדקו קישוריות למקור."],
            ["MetadataActionTimeout"] =
                ["Verify connection and retry metadata.", "Проверьте связь и повторите запрос метаданных.", "בדקו את החיבור ונסו שוב להביא נתונים."],
            ["MetadataActionDecryptionFailed"] =
                ["Check authorized LAPS decrypt rights.", "Проверьте права расшифровки LAPS.", "בדקו הרשאות לפענוח LAPS."],
            ["MetadataActionRateLimited"] =
                ["Retry after source backoff.", "Повторите после паузы источника.", "נסו שוב לאחר ההמתנה הנדרשת."],
            ["MainWindowsAccount"] = ["Use current Windows account", "Текущая учётная запись Windows", "שימוש בחשבון Windows הנוכחי"],
            ["MainOtherAdAccount"] = ["Use another AD account", "Другая учётная запись AD", "שימוש בחשבון AD אחר"],
            ["MainConnectAd"] = ["Connect to AD", "Подключить AD", "התחברות ל-AD"],
            ["MainDisconnectAd"] = ["Disconnect", "Отключиться", "ניתוק"],
            ["MainConnectionTools"] = ["Connection tools...", "Инструменты подключения...", "כלי חיבור..."],
            ["MainRecoverySteps"] = ["Recovery Steps", "Этапы восстановления", "שלבי שחזור"],
            ["MainViewCommands"] = ["View diagnostic commands", "Команды диагностики", "הצגת פקודות אבחון"],
            ["MainLoadHistory"] = ["Load history", "Загрузить историю", "טעינת היסטוריה"],
            ["MainBackToStart"] = ["Back to Start", "На главную", "חזרה להתחלה"],
            ["MainBitLockerCommands"] = ["BitLocker Commands...", "Команды BitLocker...", "פקודות BitLocker..."],
            ["MainLapsCommands"] = ["LAPS Commands...", "Команды LAPS...", "פקודות LAPS..."],
            ["MainOpenDeviceSearch"] = ["Open device search", "Поиск устройств", "פתיחת חיפוש מכשירים"],
            ["MainEnterprise"] = ["Enterprise", "Организация", "ארגוני"],
            ["MainEnvironmentCheck"] = ["Environment Check...", "Проверка среды...", "בדיקת סביבה..."],
            ["MainDeviceDiagnostics"] = ["Device / Graph Diagnostics...", "Диагностика устройств / Graph...", "אבחון מכשיר / Graph..."],
            ["MainCheckProtectedAudit"] = ["Check Protected Audit...", "Проверить защищённый аудит...", "בדיקת יומן ביקורת מוגן..."],
            ["MainUiPerformance"] = ["UI Performance...", "Производительность интерфейса...", "ביצועי הממשק..."],
            ["MainResetLayout"] = ["Reset Layout", "Сбросить расположение", "איפוס פריסת מסך"],
            ["MainCloudRotation"] = ["Allow Intune BitLocker rotation (write permission, optional)", "Разрешить ротацию BitLocker через Intune (запись, необязательно)", "אפשר סבב מפתחות BitLocker דרך Intune (הרשאת כתיבה, לא חובה)"],
            ["MainSaveConfig"] = ["Save Config", "Сохранить настройки", "שמירת הגדרות"],
            ["MainEntraWizard"] = ["Intune / Entra Setup Wizard...", "Мастер настройки Intune / Entra...", "אשף הגדרת Intune / Entra..."],
            ["MainConnectTest"] = ["Connect / Test", "Подключить / проверить", "התחברות / בדיקה"],
            ["MainFirstRunRepair"] = ["First-Run / Repair", "Первая настройка / восстановление", "הגדרה ראשונית / תיקון"],
            ["MainBootstrap"] = ["Bootstrap...", "Первичная настройка...", "אתחול ראשוני..."],
            ["MainRolloverCertificate"] = ["Rollover Certificate...", "Сменить сертификат...", "החלפת אישור..."],
            ["MainCloudLapsRead"] = ["Include LAPS read permissions in setup", "Включить разрешения чтения LAPS", "כלול הרשאות קריאה של LAPS"],
            ["MainAdvancedAd"] = ["Advanced AD settings...", "Дополнительные настройки AD...", "הגדרות AD מתקדמות..."],
            ["MainHideAdvancedAd"] = ["Hide advanced AD settings", "Скрыть дополнительные настройки AD", "הסתרת הגדרות AD מתקדמות"],
        };

    internal static string PreferenceFile =>
        Path.Combine(AppPaths.LocalConfigDirectory, "ui-language.json");

    internal static string Normalize(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return "en";
        var language = code.Trim().Split('-', '_')[0].ToLowerInvariant();
        return language is "en" or "ru" or "he" ? language : "en";
    }

    internal static bool IsRightToLeft(string? language) =>
        Normalize(language) == "he";

    // Resource coverage only. Do NOT run script checks against user-entered
    // identities, computer names, recovery values or dynamic status text.
    internal static bool HasLanguageScript(string language, string value) =>
        Normalize(language) switch
        {
            "ru" => value.Any(static c => c >= '\u0400' && c <= '\u052f'),
            "he" => value.Any(static c => c >= '\u0590' && c <= '\u05ff'),
            _ => true
        };

    internal static string Translate(string? language, string key)
    {
        if (!Messages.TryGetValue(key, out var words))
            return key;
        return words[Normalize(language) switch
        {
            "ru" => 1,
            "he" => 2,
            _ => 0
        }];
    }

    internal static string ReadPreferred(string? path = null)
    {
        try
        {
            var target = path ?? PreferenceFile;
            var stored = JsonStore.Read<Preferences>(target);
            if (stored is not null)
                return Normalize(stored.Code);
        }
        catch (Exception ex) when (ex is IOException or
                                   UnauthorizedAccessException or
                                   System.Text.Json.JsonException)
        {
            // Preference loss must never cause a security-policy fallback
            // or abort a standard-user recovery GUI session.
            System.Diagnostics.Debug.WriteLine(
                "UI language preference unavailable: " +
                DiagnosticRedaction.Sanitize(ex.Message));
        }
        return Normalize(CultureInfo.CurrentUICulture.Name);
    }

    internal static void SavePreferred(string code, string? path = null)
    {
        if (!SupportedLanguages.Any(x => x.Code == code))
            throw new ArgumentException("Unsupported interface language.", nameof(code));
        JsonStore.WriteAtomic(path ?? PreferenceFile, new Preferences { Code = code });
    }

    internal static IEnumerable<string> RunOfflineTests(string testDirectory)
    {
        var failures = new List<string>();
        try
        {
            // All shell resources must have three nonempty translations.
            // Some technical product names are deliberately language-neutral.
            foreach (var (key, words) in Messages)
            {
                if (words.Length != 3 || words.Any(string.IsNullOrWhiteSpace))
                {
                    failures.Add("Missing EN/RU/HE shell resource: " + key);
                    continue;
                }
                if (!HasLanguageScript("ru", words[1]) &&
                    key is not ("BitLocker" or "LAPS" or "RecoveryStatusOk"))
                    failures.Add("Untranslated Russian shell resource: " + key);
                if (!HasLanguageScript("he", words[2]) &&
                    key is not ("BitLocker" or "LAPS"))
                    failures.Add("Untranslated Hebrew shell resource: " + key);
            }
            if (!HasLanguageScript("ru", "Тест") ||
                !HasLanguageScript("he", "בדיקה") ||
                HasLanguageScript("ru", "Untranslated") ||
                HasLanguageScript("he", "Untranslated"))
                failures.Add("Language-script coverage validator is incorrect.");

            if (Normalize("ru-RU") != "ru" ||
                Normalize("he-IL") != "he" ||
                Normalize("en-US") != "en" ||
                Normalize("unknown") != "en" ||
                !IsRightToLeft("he") ||
                IsRightToLeft("ru"))
                failures.Add("Language normalization or Hebrew RTL policy invalid.");

            foreach (var code in SupportedLanguages.Select(x => x.Code))
            {
                foreach (var key in new[]
                {
                    "Start", "BitLocker", "LAPS", "Devices",
                    "Helpdesk", "Diagnostics", "Settings", "Tools",
                    "Language", "Search", "Cancel", "ConnectionSettings",
                    "UpdateCenterTitle", "UpdateCenterSubtitle", "UpdateCenterRefresh",
                    "UpdateCenterRetry", "UpdateCenterPause", "UpdateCenterEnable",
                    "UpdateCenterCompact", "UpdateCenterVersion", "UpdateCenterLatest",
                    "UpdateCenterChannel", "UpdateCenterLastCheck", "UpdateCenterLastResult",
                    "UpdateCenterTask", "UpdateCenterNextRun", "UpdateCenterNever",
                    "UpdateCenterStable", "UpdateCenterPreview", "UpdateCenterChecksOff",
                    "UpdateCenterInstallOff", "UpdateCenterHeld", "UpdateCenterPolicyBlocked",
                    "UpdateCenterInstallReady", "UpdateCenterTaskSafe", "UpdateCenterTaskMissing",
                    "UpdateCenterTaskUnsafe", "UpdateCenterLastTask", "UpdateCenterTaskResult",
                    "UpdateCenterTrust", "UpdateCenterNoHistory", "UpdateCenterInstallAction",
                    "UpdateCenterRollbackAction", "UpdateCenterSuccess", "UpdateCenterFailed",
                    "UpdateCenterLastSuccess", "UpdateCenterLastFailure",
                    "RecoveryStepsShortcut", "AccessHealthShortcut",
                    "RecoveryDialogTitle", "RecoveryDialogHeading",
                    "RecoveryDeviceLabel", "RecoverySourceLabel",
                    "RecoveryOpenStart", "RecoveryAccessHealth",
                    "RecoveryOpenBitLocker", "RecoveryOpenLaps",
                    "RecoveryOpenDevices", "RecoveryClose",
                    "RecoveryStep1", "RecoveryStep2", "RecoveryStep3",
                    "RecoveryStep4", "RecoveryStep5", "RecoveryStep6", "RecoveryStep7",
                    "RecoveryStatusOk", "RecoveryStatusAction", "RecoveryStatusUnverified",
                    "RecoveryStatusNotTested", "RecoveryStatusInfo",
                    "RecoveryStatusRequired", "RecoveryStatusOptional",
                    "RecoveryStatusPendingAck", "RecoveryStatusPolicy",
                    "RecoveryStatusExplicit"
                })
                {
                    var text = Translate(code, key);
                    if (string.IsNullOrWhiteSpace(text))
                        failures.Add("Missing localized shell resource: " + key);
                }
            }

            // Checklist progress is formatted at runtime, not translated
            // from a fixed English "0 of 7" label. Both placeholders must
            // remain valid in all three supported languages.
            foreach (var language in SupportedLanguages.Select(x => x.Code))
            {
                var template = Translate(language, "HelpdeskDeploymentProgressTemplate");
                try
                {
                    var value = string.Format(CultureInfo.InvariantCulture,
                        template, 2, 7);
                    if (!value.Contains("2", StringComparison.Ordinal) ||
                        !value.Contains("7", StringComparison.Ordinal))
                        failures.Add("Helpdesk progress template lost a count: " + language);
                }
                catch (FormatException)
                {
                    failures.Add("Invalid Helpdesk progress template: " + language);
                }
            }

            // Diagnostic status messages must stay in a scoped display-only
            // resource set; never make the global static-caption observer
            // capture mutable security/service result labels.
            var readinessKeys = new[]
            {
                "HelpdeskStatusNotInstalled", "HelpdeskStatusUnavailableQuery",
                "HelpdeskStatusDisabled", "HelpdeskStatusAuditOptional",
                "HelpdeskStatusAuditStrict", "HelpdeskStatusAclVerified",
                "HelpdeskStatusAclUntrusted", "HelpdeskStatusVerifyUnavailable",
                "HelpdeskStatusNotChecked", "HelpdeskStatusChainValid",
                "HelpdeskStatusChainInvalid", "HelpdeskStatusNoReadAccess",
                "HelpdeskStatusConnected", "HelpdeskStatusDisconnected",
                "HelpdeskStatusAllowed", "HelpdeskStatusDenied"
            };
            foreach (var language in SupportedLanguages.Select(x => x.Code))
                foreach (var key in readinessKeys)
                {
                    var value = Translate(language, key);
                    if (string.IsNullOrWhiteSpace(value) ||
                        string.Equals(value, key, StringComparison.Ordinal))
                        failures.Add("Helpdesk scoped status resource missing: " +
                            key + " (" + language + ")");
                }

            foreach (var language in SupportedLanguages.Select(x => x.Code))
            {
                foreach (var kind in Enum.GetValues<MetadataFailureKind>()
                    .Where(x => x != MetadataFailureKind.None))
                {
                    foreach (var prefix in new[] { "MetadataKind", "MetadataAction" })
                    {
                        var key = prefix + kind;
                        if (Translate(language, key) is var word &&
                            (string.IsNullOrWhiteSpace(word) || word == key))
                            failures.Add("Missing translated metadata failure: " + key +
                                " (" + language + ")");
                    }
                }
                foreach (var key in new[] {
                    "MetadataPartial", "MetadataVisible",
                    "MetadataSeeDiagnostics", "MetadataNoAdRecords",
                    "MetadataNoCloudRecords", "MetadataBackupUnverified"
                })
                {
                    if (Translate(language, key) is var word &&
                        (string.IsNullOrWhiteSpace(word) || word == key))
                        failures.Add("Missing translated metadata guidance: " + key);
                }
            }

            foreach (var language in SupportedLanguages.Select(x => x.Code))
            {
                foreach (var key in new[] { "MainWindowsAccount", "MainOtherAdAccount", "MainConnectAd", "MainDisconnectAd", "MainConnectionTools", "MainRecoverySteps", "MainViewCommands", "MainLoadHistory", "MainBackToStart", "MainBitLockerCommands", "MainLapsCommands", "MainOpenDeviceSearch", "MainEnterprise", "MainEnvironmentCheck", "MainDeviceDiagnostics", "MainCheckProtectedAudit", "MainUiPerformance", "MainResetLayout", "MainCloudRotation", "MainSaveConfig", "MainEntraWizard", "MainConnectTest", "MainFirstRunRepair", "MainBootstrap", "MainRolloverCertificate", "MainCloudLapsRead", "MainAdvancedAd", "MainHideAdvancedAd" })
                {
                    var translation = Translate(language, key);
                    if (string.IsNullOrWhiteSpace(translation) ||
                        string.Equals(translation, key, StringComparison.Ordinal))
                        failures.Add("Missing localized static caption: " + key +
                            " [" + language + "]");
                }
            }

            failures.AddRange(UiStaticCaptionCatalog.ValidateLanguages());
            foreach (var original in new[]
            {
                "Export & Automation", "Recovery Export Storage",
                "Export Scopes", "Output root:", "Run Export",
                "Save Export Settings", "Remove Selected",
                "Override publish guards (row drop / scope change)",
                "Updates", "Advanced", "Service & Health",
                "Domain Controllers", "Audit"
            })
            {
                foreach (var language in SupportedLanguages.Select(x => x.Code))
                {
                    if (!UiStaticCaptionCatalog.TryTranslate(language,
                            original, out var caption) ||
                        string.IsNullOrWhiteSpace(caption))
                        failures.Add("Missing full RU/HE Admin translation: " +
                            original + " (" + language + ")");
                }
            }

            var preference = Path.Combine(testDirectory, "ui-language-fixture.json");
            SavePreferred("he", preference);
            if (ReadPreferred(preference) != "he")
                failures.Add("Hebrew language choice did not persist.");
            SavePreferred("ru", preference);
            if (ReadPreferred(preference) != "ru")
                failures.Add("Russian language choice did not persist.");
            bool badRejected = false;
            try { SavePreferred("invalid", preference); }
            catch (ArgumentException) { badRejected = true; }
            if (!badRejected)
                failures.Add("Invalid language setting was silently accepted.");
        }
        catch (Exception ex)
        {
            failures.Add("UI language preferences: " +
                DiagnosticRedaction.Sanitize(ex.Message));
        }

        return failures;
    }
}
