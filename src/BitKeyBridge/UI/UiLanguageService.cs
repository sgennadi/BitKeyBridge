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
            ["ConnectionSettings"] =
                ["Open Start / connection settings",
                 "Открыть главную / настройки подключения",
                 "פתיחת התחלה / הגדרות חיבור"]
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
                    "Language", "Search", "Cancel", "ConnectionSettings"
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
