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
            ["RecoveryStatusExplicit"] = ["Explicit action only", "Только по команде", "פעולה מפורשת בלבד"]
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
