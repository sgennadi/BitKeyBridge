namespace BitKeyBridge;

/// <summary>
/// Fixed, non-secret English captions in the existing WinForms workspaces.
/// Entries are identified by their original English literal, not by a control's
/// current translated text. Never apply to editable text, device metadata,
/// recovery secrets, dynamically generated status, or exception messages.
/// </summary>
internal static class UiStaticCaptionCatalog
{
    private static readonly IReadOnlyDictionary<string, string[]> Captions =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["Updates"] = ["Обновления", "עדכונים"],
            ["Advanced"] = ["Дополнительно", "מתקדם"],
            ["Export & Automation"] = ["Экспорт и автоматизация", "ייצוא ואוטומציה"],
            ["Coverage"] = ["Покрытие", "כיסוי"],
            ["Cloud"] = ["Облако", "ענן"],
            ["Service & Health"] = ["Служба и состояние", "שירות ומצב"],
            ["Domain Controllers"] = ["Контроллеры домена", "בקרי תחום"],
            ["Audit"] = ["Аудит", "ביקורת"],
            ["Helpdesk"] = ["Поддержка", "תמיכה"],
            ["Tools"] = ["Инструменты", "כלים"],
            ["Open recovery export"] = ["Открыть файл экспорта", "פתיחת קובץ הייצוא"],
            ["Open export log"] = ["Открыть журнал экспорта", "פתיחת יומן הייצוא"],
            ["Open export folder"] = ["Открыть папку экспорта", "פתיחת תיקיית הייצוא"],
            ["Windows Event Log"] = ["Журнал событий Windows", "יומן האירועים של Windows"],
            ["Latest GitHub release"] = ["Последний выпуск GitHub", "הגרסה האחרונה ב-GitHub"],
            ["Refresh"] = ["Обновить", "רענון"],
            ["Cancel"] = ["Отмена", "ביטול"],
            ["Start"] = ["Запуск", "התחלה"],
            ["Stop"] = ["Остановить", "עצירה"],
            ["Search"] = ["Поиск", "חיפוש"],
            ["Close"] = ["Закрыть", "סגירה"],
            ["Save"] = ["Сохранить", "שמירה"],
            ["Disable"] = ["Отключить", "השבתה"],
            ["Clear"] = ["Очистить", "ניקוי"],
            ["From:"] = ["С:", "מאת:"],
            ["To:"] = ["По:", "עד:"],

            // Enterprise / Export & Automation, including every visible
            // caption in the administrator screenshot (but never file paths).
            ["Recovery Export Storage"] = ["Хранилище экспорта восстановления", "אחסון ייצוא שחזור"],
            ["Export is an administrative/offline-cache function and is intentionally separate from the normal Recovery screen."] =
                ["Экспорт служит для администрирования и автономного кэша; он отделён от обычного экрана восстановления.",
                 "הייצוא מיועד לניהול ולמטמון לא מקוון, והוא נפרד ממסך השחזור הרגיל."],
            ["Output root:"] = ["Корневая папка:", "תיקיית יעד ראשית:"],
            ["Subdirectory:"] = ["Подпапка:", "תיקיית משנה:"],
            ["Save Export Settings"] = ["Сохранить настройки экспорта", "שמירת הגדרות ייצוא"],
            ["Secure Output..."] = ["Защитить папку экспорта...", "אבטחת תיקיית הייצוא..."],
            ["Export Scopes"] = ["Области экспорта", "טווחי ייצוא"],
            ["Checked OUs are used by export and DC comparison. With no checked scope, configured defaults are used."] =
                ["Выбранные OU используются при экспорте и сравнении контроллеров домена. Если ничего не выбрано, применяются области по умолчанию.",
                 "יחידות OU מסומנות משמשות לייצוא ולהשוואת בקרי תחום. ללא בחירה ייעשה שימוש בטווחי ברירת המחדל."],
            ["Add OU..."] = ["Добавить OU...", "הוספת OU..."],
            ["Use Defaults"] = ["По умолчанию", "שימוש בברירות מחדל"],
            ["Remove Selected"] = ["Удалить выбранное", "הסרת הבחירה"],
            ["Check All"] = ["Выбрать всё", "בחירת הכול"],
            ["Save as Defaults"] = ["Сохранить как стандартные", "שמירה כברירת מחדל"],
            ["Run Export"] = ["Выполнить экспорт", "הפעלת ייצוא"],
            ["The normal helpdesk Recovery flow does not require this export. Use it for offline cache / automation only."] =
                ["Обычная работа службы поддержки не требует этого экспорта. Используйте его только для автономного кэша или автоматизации.",
                 "תהליך השחזור הרגיל אינו מצריך ייצוא זה. השתמשו בו רק למטמון לא מקוון או לאוטומציה."],
            ["Dry Run"] = ["Пробный запуск", "הרצת בדיקה"],
            ["Override publish guards (row drop / scope change)"] =
                ["Обойти ограничения публикации (уменьшение строк / смена области)",
                 "עקיפת הגנות פרסום (ירידת רשומות / שינוי טווח)"],
            ["No run in this GUI session yet."] =
                ["В этом сеансе запусков ещё не было.", "טרם בוצעה הרצה בהפעלה זו."],
            ["Last successful export: not recorded yet."] =
                ["Последний успешный экспорт: пока нет данных.", "ייצוא מוצלח אחרון: טרם נרשם."],
            ["Last successful export: status unavailable."] =
                ["Последний успешный экспорт: данные недоступны.", "ייצוא מוצלח אחרון: המצב אינו זמין."],
            ["Scope"] = ["Область", "טווח"],
            ["Status"] = ["Состояние", "מצב"],
            ["Objects"] = ["Объекты", "אובייקטים"],
            ["Valid"] = ["Корректные", "תקינים"],
            ["Duplicates"] = ["Дубликаты", "כפילויות"],
            ["Invalid"] = ["Ошибки", "לא תקינים"],
            ["Error"] = ["Ошибка", "שגיאה"],

            // Settings / Updates.
            ["Verified Updater"] = ["Проверяемый обновлятор", "מעדכן מאומת"],
            ["Updates are checked against GitHub Releases, verified by SHA-256 and self-tested before installation. Use the controls below for manual updates or rollback; the release link is also under Tools."] =
                ["Обновления сверяются с GitHub Releases, проверяются по SHA-256 и тестируются до установки. Здесь можно обновить программу или выполнить откат вручную.",
                 "עדכונים נבדקים מול GitHub Releases, מאומתים באמצעות SHA-256 ועוברים בדיקות לפני התקנה. כאן ניתן לעדכן או לשחזר ידנית."],
            ["GitHub repository:"] = ["Репозиторий GitHub:", "מאגר GitHub:"],
            ["Automatically check for updates when GUI starts"] =
                ["Проверять обновления при запуске", "בדיקת עדכונים בהפעלה"],
            ["Automatically install verified updates and restart"] =
                ["Автоматически устанавливать проверенные обновления и перезапускать",
                 "התקנה אוטומטית של עדכונים מאומתים והפעלה מחדש"],
            ["Allow unsigned auto-install from official GitHub (requires consent)"] =
                ["Разрешить автоустановку неподписанных релизов из GitHub (нужно согласие)",
                 "לאפשר התקנה אוטומטית לא חתומה מ-GitHub (נדרשת הסכמה)"],
            ["Allow prerelease versions"] = ["Разрешить предварительные версии", "לאפשר גרסאות קדם"],
            ["Require a valid trusted Authenticode signature"] =
                ["Требовать действительную доверенную подпись Authenticode",
                 "לדרוש חתימת Authenticode מהימנה ותקפה"],
            ["Trusted publisher:"] = ["Доверенный издатель:", "מפרסם מהימן:"],
            ["Save Update Settings"] = ["Сохранить настройки обновлений", "שמירת הגדרות עדכון"],
            ["Check for Updates"] = ["Проверить обновления", "בדיקת עדכונים"],
            ["Update Now"] = ["Обновить сейчас", "עדכון עכשיו"],
            ["Resume Auto-Install"] = ["Возобновить автоустановку", "חידוש התקנה אוטומטית"],
            ["Background updater (optional)"] = ["Фоновое обновление (необязательно)", "עדכון ברקע (אופציונלי)"],
            ["Run verified updates daily even when the GUI is closed. Requires an installed Program Files executable, local admin permission, and explicitly approved automatic updates."] =
                ["Ежедневная установка проверенных обновлений при закрытом интерфейсе. Требуются установка в Program Files, права администратора и явное разрешение.",
                 "התקנת עדכונים מאומתים בכל יום גם כשהממשק סגור. נדרשים התקנה ב-Program Files, הרשאות מנהל ואישור מפורש."],
            ["Local time (hour):"] = ["Местное время (час):", "שעה מקומית:"],
            ["Schedule Daily"] = ["Ежедневное задание", "תזמון יומי"],
            ["Task Status"] = ["Состояние задания", "מצב המשימה"],
            ["Remove Schedule"] = ["Удалить расписание", "הסרת תזמון"],
            ["Update History / Rollback"] = ["История обновлений и откат", "היסטוריית עדכונים ושחזור"],
            ["Refresh History"] = ["Обновить историю", "רענון היסטוריה"],
            ["Rollback Previous Build"] = ["Откатить предыдущую сборку", "שחזור לבנייה קודמת"],
            ["Online Rollback..."] = ["Откат из сети...", "שחזור מקוון..."],
            ["Time (UTC)"] = ["Время (UTC)", "זמן (UTC)"],
            ["Action"] = ["Действие", "פעולה"],
            ["From"] = ["Исходная версия", "מגרסה"],
            ["Result"] = ["Результат", "תוצאה"],
            ["Mode"] = ["Режим", "מצב פעולה"],
            ["Publisher"] = ["Издатель", "מפרסם"],
            ["Details"] = ["Подробности", "פרטים"],

            // Advanced / security settings.
            ["Remote Monitoring / Management API"] = ["API удалённого мониторинга и управления", "API לניטור ולניהול מרחוק"],
            ["Optional TLS + bearer-token API. Recovery passwords are never exposed by the API."] =
                ["Необязательный API TLS и токенов. Пароли восстановления через API не выдаются.",
                 "API אופציונלי עם TLS ואסימוני גישה. סיסמאות שחזור לעולם אינן נחשפות דרך ה-API."],
            ["TCP port:"] = ["Порт TCP:", "יציאת TCP:"],
            ["Allow remote export + Coverage run"] = ["Разрешить удалённый экспорт и отчёт о покрытии", "לאפשר ייצוא מרחוק והרצת דוח כיסוי"],
            ["Enable / Reconfigure"] = ["Включить / перенастроить", "הפעלה / הגדרה מחדש"],
            ["Rotate Admin Token"] = ["Сменить токен администратора", "החלפת אסימון מנהל"],
            ["Scoped Tokens..."] = ["Токены с ограничением области...", "אסימונים מוגבלי טווח..."],
            ["Enterprise Recovery Policies"] = ["Корпоративные политики восстановления", "מדיניות שחזור ארגונית"],
            ["RBAC, JIT recovery, two-person approval, SIEM and related gates are optional. Fresh installations keep all of them disabled."] =
                ["RBAC, JIT, согласование двумя лицами и SIEM необязательны и по умолчанию выключены.",
                 "RBAC, שחזור JIT, אישור דו-אישי ו-SIEM הם אופציונליים וכבויים כברירת מחדל."],
            ["RBAC Setup Wizard..."] = ["Мастер настройки RBAC...", "אשף הגדרת RBAC..."],
            ["RBAC Advanced..."] = ["Дополнительные настройки RBAC...", "הגדרות RBAC מתקדמות..."],
            ["Privileged Access..."] = ["Привилегированный доступ...", "גישה מורשית..."],
            ["Configuration / Diagnostics / Storage"] = ["Конфигурация / диагностика / хранение", "תצורה / אבחון / אחסון"],
            ["Backup, restore, diagnostics, incident metadata and storage housekeeping."] =
                ["Резервное копирование, восстановление, диагностика, инциденты и очистка хранилища.",
                 "גיבוי, שחזור, אבחון, נתוני אירועים ותחזוקת אחסון."],
            ["Backup Config"] = ["Сохранить резервную копию", "גיבוי תצורה"],
            ["Restore Config"] = ["Восстановить конфигурацию", "שחזור תצורה"],
            ["Diagnostics ZIP"] = ["Архив диагностики ZIP", "קובץ אבחון ZIP"],
            ["Open Incidents"] = ["Открыть инциденты", "פתיחת אירועים"],
            ["Housekeeping..."] = ["Очистка хранилища...", "תחזוקת אחסון..."],

            // Coverage / service / domain controller / audit workspaces.
            ["BitLocker Coverage"] = ["Покрытие BitLocker", "כיסוי BitLocker"],
            ["AD + Entra + Intune metadata only. Recovery passwords are never requested by this report."] =
                ["Только метаданные AD, Entra и Intune. Пароли восстановления не запрашиваются.",
                 "נתוני AD, Entra ו-Intune בלבד. דוח זה אינו מבקש סיסמאות שחזור."],
            ["Run Coverage"] = ["Проверить покрытие", "הרצת בדיקת כיסוי"],
            ["Export Visible CSV"] = ["Экспорт видимого в CSV", "ייצוא המוצג ל-CSV"],
            ["Policy..."] = ["Политика...", "מדיניות..."],
            ["Filter:"] = ["Фильтр:", "סינון:"],
            ["Intune stale days:"] = ["Устарело в Intune (дней):", "ימי חוסר עדכון ב-Intune:"],
            ["Old key days:"] = ["Возраст старого ключа (дней):", "גיל מפתח ישן בימים:"],
            ["Computer"] = ["Компьютер", "מחשב"],
            ["AD Keys"] = ["Ключи AD", "מפתחות AD"],
            ["Entra Keys"] = ["Ключи Entra", "מפתחות Entra"],
            ["Intune"] = ["Intune", "Intune"],
            ["Encrypted"] = ["Зашифрован", "מוצפן"],
            ["Compliance"] = ["Соответствие", "תאימות"],
            ["Last Sync"] = ["Последняя синхронизация", "סנכרון אחרון"],
            ["AD Last Logon"] = ["Последний вход AD", "כניסה אחרונה ל-AD"],
            ["Newest Cloud Key"] = ["Последний ключ в облаке", "המפתח העדכני בענן"],
            ["Serial"] = ["Серийный номер", "מספר סידורי"],
            ["User / UPN"] = ["Пользователь / UPN", "משתמש / UPN"],
            ["Model"] = ["Модель", "דגם"],
            ["BitKeyBridge Health & Service"] = ["Служба и состояние BitKeyBridge", "שירות ומצב BitKeyBridge"],
            ["Service automation, health endpoint and scheduled coverage."] =
                ["Автоматизация службы, проверка состояния и плановое покрытие.",
                 "אוטומציית שירות, נקודת בדיקת תקינות וכיסוי מתוזמן."],
            ["Install / Update"] = ["Установить / обновить", "התקנה / עדכון"],
            ["Uninstall"] = ["Удалить", "הסרה"],
            ["Open /health"] = ["Открыть /health", "פתיחת /health"],
            ["Export interval (minutes):"] = ["Интервал экспорта (мин):", "מרווח ייצוא (דקות):"],
            ["Health endpoint port:"] = ["Порт проверки состояния:", "יציאת בדיקת תקינות:"],
            ["Enable loopback health endpoint"] = ["Включить локальную проверку состояния", "הפעלת בדיקת תקינות מקומית"],
            ["Run export when service starts"] = ["Экспортировать при старте службы", "הרצת ייצוא עם הפעלת השירות"],
            ["Enable scheduled Coverage"] = ["Включить плановую проверку покрытия", "הפעלת בדיקת כיסוי מתוזמנת"],
            ["Coverage interval (minutes):"] = ["Интервал проверки (мин):", "מרווח בדיקת כיסוי (דקות):"],
            ["Run Coverage when service starts"] = ["Проверять покрытие при старте службы", "בדיקת כיסוי עם הפעלת השירות"],
            ["Save Service Settings"] = ["Сохранить настройки службы", "שמירת הגדרות שירות"],
            ["Save Cloud for Service"] = ["Сохранить облачные настройки службы", "שמירת הגדרות ענן לשירות"],
            ["Delete Machine Cloud"] = ["Удалить облачные настройки компьютера", "מחיקת הגדרות ענן למחשב"],
            ["Repair Service Access"] = ["Восстановить доступ к службе", "תיקון גישה לשירות"],
            ["Service Identity"] = ["Учётная запись службы", "זהות השירות"],
            ["Use LocalSystem, gMSA/managed account, or a domain account."] =
                ["Используйте LocalSystem, gMSA или доменную учётную запись.",
                 "ניתן להשתמש ב-LocalSystem, בחשבון gMSA מנוהל או בחשבון תחום."],
            ["LocalSystem"] = ["LocalSystem", "LocalSystem"],
            ["gMSA / Managed Account"] = ["gMSA / Управляемая запись", "gMSA / חשבון מנוהל"],
            ["Domain Account"] = ["Доменная запись", "חשבון תחום"],
            ["Identity:"] = ["Идентификатор:", "זהות:"],
            ["Account:"] = ["Учётная запись:", "חשבון:"],
            ["Password:"] = ["Пароль:", "סיסמה:"],
            ["Apply Identity"] = ["Применить учётную запись", "החלת זהות"],
            ["Domain Controller Comparison"] = ["Сравнение контроллеров домена", "השוואת בקרי תחום"],
            ["Discovers domain controllers dynamically and compares BitLocker object counts / replication health."] =
                ["Автоматически находит контроллеры домена, сравнивает число объектов BitLocker и состояние репликации.",
                 "איתור בקרי תחום והשוואת כמות אובייקטי BitLocker ומצב שכפול."],
            ["Discover and Test DCs"] = ["Найти и проверить контроллеры", "איתור ובדיקת בקרי תחום"],
            ["Site"] = ["Сайт", "אתר"],
            ["RODC"] = ["RODC", "RODC"],
            ["Reachable"] = ["Доступен", "נגיש"],
            ["Delta"] = ["Разница", "הפרש"],
            ["Repl errors"] = ["Ошибки репликации", "שגיאות שכפול"],
            ["Warnings"] = ["Предупреждения", "אזהרות"],
            ["Max age h"] = ["Макс. возраст, ч", "גיל מרבי בשעות"],
            ["IPv4"] = ["IPv4", "IPv4"],
            ["Open audit.jsonl"] = ["Открыть audit.jsonl", "פתיחת audit.jsonl"],
            ["Verify Chain"] = ["Проверить цепочку", "אימות שרשרת"],
            ["Setup Signing"] = ["Настроить подпись", "הגדרת חתימה"],
            ["Sign Now"] = ["Подписать сейчас", "חתימה עכשיו"],
            ["Verify Signature"] = ["Проверить подпись", "אימות חתימה"],
            ["Rollover Signing"] = ["Сменить сертификат подписи", "החלפת אישור חתימה"],
            ["Disable Signing"] = ["Отключить подпись", "השבתת חתימה"],
            ["Incident..."] = ["Инцидент...", "אירוע..."],
            ["Tamper-Evident Recovery Audit"] = ["Защищённый аудит восстановления", "יומן ביקורת שחזור מוגן"],
            ["Search by computer, ticket/reference, operator, Recovery ID, action, session or date. Recovery passwords are never written to audit."] =
                ["Поиск по компьютеру, заявке, оператору, Recovery ID, действию, сеансу и дате. Пароли восстановления в журнал не записываются.",
                 "חיפוש לפי מחשב, פנייה, מפעיל, Recovery ID, פעולה, הפעלה או תאריך. סיסמאות שחזור אינן נשמרות ביומן."],
            ["Ticket / Reference"] = ["Заявка / ссылка", "פנייה / אסמכתא"],
            ["Operator"] = ["Оператор", "מפעיל"],
            ["Recovery ID"] = ["Идентификатор восстановления", "מזהה שחזור"],
            ["Session"] = ["Сеанс", "הפעלה"],
            ["Copy Session ID"] = ["Копировать ID сеанса", "העתקת מזהה הפעלה"],
            ["User"] = ["Пользователь", "משתמש"],
            ["Host"] = ["Узел", "מארח"],
            ["Source"] = ["Источник", "מקור"],
            ["Auth"] = ["Аутентификация", "אימות"],
            ["Reference"] = ["Ссылка", "אסמכתא"],
            ["Reason"] = ["Причина", "סיבה"]
        };

    internal static bool TryTranslate(string? language, string english, out string translation)
    {
        if (!Captions.TryGetValue(english, out var variants))
        {
            translation = english;
            return false;
        }
        translation = UiLanguageService.Normalize(language) switch
        {
            "ru" => variants[0],
            "he" => variants[1],
            _ => english
        };
        return true;
    }

    internal static bool Contains(string english) => Captions.ContainsKey(english);

    internal static string TranslateOrOriginal(string? language, string english) =>
        TryTranslate(language, english, out var translation) ? translation : english;

    internal static IEnumerable<string> ValidateLanguages()
    {
        foreach (var (english, variants) in Captions)
        {
            if (string.IsNullOrWhiteSpace(english) || variants.Length != 2 ||
                string.IsNullOrWhiteSpace(variants[0]) ||
                string.IsNullOrWhiteSpace(variants[1]))
                yield return "Missing RU/HE static UI caption: " + english;
        }
    }
}
