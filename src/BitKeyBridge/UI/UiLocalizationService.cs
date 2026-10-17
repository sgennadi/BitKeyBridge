using System.Globalization;

namespace BitKeyBridge;

internal enum UiLanguage
{
    English,
    Russian,
    Hebrew
}

internal sealed class UiLanguagePreference
{
    public string Language { get; set; } = string.Empty;
}

internal static class UiLocalizationService
{
    private static readonly string[] Keys =
    [
        "Start", "BitLocker", "LAPS", "Devices",
        "Helpdesk", "Diagnostics", "Settings",
        "Language", "Tools", "English", "Russian", "Hebrew"
    ];

    private static readonly Dictionary<UiLanguage, string[]> Values = new()
    {
        [UiLanguage.English] =
        [
            "Start", "BitLocker", "LAPS", "Devices", "Helpdesk",
            "Diagnostics", "Settings", "Language", "Tools",
            "English", "Русский", "עברית"
        ],
        [UiLanguage.Russian] =
        [
            "Главная", "BitLocker", "LAPS", "Устройства", "Техподдержка",
            "Диагностика", "Настройки", "Язык", "Инструменты",
            "English", "Русский", "עברית"
        ],
        [UiLanguage.Hebrew] =
        [
            "התחלה", "BitLocker", "LAPS", "מכשירים", "תמיכה",
            "אבחון", "הגדרות", "שפה", "כלים",
            "English", "Русский", "עברית"
        ]
    };

    internal static bool IsRightToLeft(UiLanguage language) =>
        language == UiLanguage.Hebrew;

    internal static string Get(UiLanguage language, string key)
    {
        var idx = Array.IndexOf(Keys, key);
        if (idx < 0)
            throw new ArgumentOutOfRangeException(nameof(key), "Unknown navigation key.");
        return Values[language][idx];
    }

    internal static UiLanguage DefaultFromWindows()
    {
        return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName switch
        {
            "ru" => UiLanguage.Russian,
            "he" => UiLanguage.Hebrew,
            _ => UiLanguage.English
        };
    }

    internal static UiLanguage LoadPreference()
    {
        try
        {
            var saved = JsonStore.Read<UiLanguagePreference>(
                AppPaths.UiLanguagePreferencesFile);
            if (Enum.TryParse<UiLanguage>(saved?.Language, true, out var language) &&
                Enum.IsDefined(language))
                return language;
        }
        catch (Exception ex)
        {
            // UI preferences must never relax machine-level recovery policy.
            System.Diagnostics.Debug.WriteLine(
                "BitKeyBridge UI language preference unavailable: " +
                DiagnosticRedaction.Sanitize(ex.GetType().Name));
        }
        return DefaultFromWindows();
    }

    internal static void SavePreference(UiLanguage language)
    {
        if (!Enum.IsDefined(language))
            throw new ArgumentOutOfRangeException(nameof(language));
        JsonStore.WriteAtomic(
            AppPaths.UiLanguagePreferencesFile,
            new UiLanguagePreference { Language = language.ToString() });
    }

    internal static IEnumerable<string> RunOfflineTests()
    {
        var failures = new List<string>();
        foreach (var language in Enum.GetValues<UiLanguage>())
        {
            if (Values[language].Length != Keys.Length ||
                Values[language].Any(string.IsNullOrWhiteSpace))
                failures.Add($"UI language {language} has an incomplete navigation bundle.");
            if (IsRightToLeft(language) != (language == UiLanguage.Hebrew))
                failures.Add($"UI language {language} RTL selection is inconsistent.");
            var distinct = Keys.Take(7).Select(key => Get(language, key)).Distinct();
            if (distinct.Count() != 7)
                failures.Add($"UI language {language} contains ambiguous tab titles.");
        }
        return failures;
    }
}
