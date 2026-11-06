namespace BitKeyBridge;

/// <summary>
/// Derived, entirely offline and secret-free technician guidance.
/// Only typed metadata states and explicit operator-initiated WMI outcomes
/// are accepted; never parse remote text, exceptions or Graph payloads.
/// </summary>
internal enum Device360FindingPriority
{
    Action,
    Verify
}

internal sealed record Device360DiagnosticFinding(
    string Code, Device360FindingPriority Priority,
    string Source, string Explanation, string NextAction);

internal static class Device360SmartDiagnosticsService
{
    internal static IReadOnlyList<Device360DiagnosticFinding> Evaluate(
        Device360Snapshot snapshot, Device360OperationalStatus status,
        Device360HardwareInventory? hardware = null,
        string? hardwareFailureCategory = null, DateTime? nowUtc = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(status);
        var issues = new List<Device360DiagnosticFinding>();
        var now = (nowUtc ?? DateTime.UtcNow).ToUniversalTime();

        void Add(string code, Device360FindingPriority priority, string source,
            string explanation, string nextAction) =>
            issues.Add(new Device360DiagnosticFinding(code, priority, source,
                explanation, nextAction));

        if (snapshot.IdentityAmbiguous)
            Add("IDENTITY.CONFLICT", Device360FindingPriority.Action, "Identity",
                "One or more source identities are conflicting or duplicated.",
                "Compare the authoritative AD GUID, Entra device ID and Intune managedDeviceId separately. Do not match by display name.");
        else if (!snapshot.IdentityVerified)
            Add("IDENTITY.UNVERIFIED", Device360FindingPriority.Verify, "Identity",
                "A uniquely qualified source identity was not established.",
                "Select the exact authoritative source record before opening a recovery workflow.");

        AddSource("AD", status.Ad, status.AdFailureKind);
        AddSource("ENTRA", status.Entra, status.EntraFailureKind);
        AddSource("INTUNE", status.Intune, status.IntuneFailureKind);

        if (snapshot.BitLockerMetadataIncomplete)
            Add("BITLOCKER.METADATA_INCOMPLETE", Device360FindingPriority.Verify,
                "BitLocker", "This search did not establish complete backup metadata.",
                "Verify the exact device ID and the authoritative backup source. Do not assume the key is missing or readable.");
        if (snapshot.LapsMetadataExpired)
            Add("LAPS.EXPIRED", Device360FindingPriority.Action, "LAPS",
                "Observed expiration is in the past; the password was not read.",
                "Use approved LAPS diagnostics to inspect policy and authorized backup state.");
        else if (!snapshot.LapsMetadataDetected)
            Add("LAPS.UNVERIFIED", Device360FindingPriority.Verify, "LAPS",
                "Current search did not verify a LAPS backup.",
                "Use the dedicated LAPS workspace; do not infer absence.");

        if (snapshot.InIntune == true)
        {
            if (string.Equals(snapshot.Compliance.Trim(), "noncompliant",
                    StringComparison.OrdinalIgnoreCase))
                Add("INTUNE.NONCOMPLIANT", Device360FindingPriority.Action,
                    "Intune", "Intune reports noncompliance; the policy was not re-run.",
                    "Inspect the assigned compliance policy in an authorized Intune session.");
            if (snapshot.Encrypted == false)
                Add("INTUNE.ENCRYPTION_REPORTED_OFF",
                    Device360FindingPriority.Action, "Intune",
                    "Managed inventory reports encryption disabled; local protection was not checked.",
                    "Verify the endpoint's actual BitLocker status using an authorized workflow.");
            if (snapshot.LastSyncUtc is { } sync)
            {
                var utc = sync.ToUniversalTime();
                if (utc <= now && utc < now.AddDays(-14))
                    Add("INTUNE.STALE_SYNC", Device360FindingPriority.Action,
                        "Intune", "Last reported sync is older than 14 days.",
                        "Check device reachability, enrollment and the Intune management status.");
                else if (utc > now.AddMinutes(5))
                    Add("INTUNE.CLOCK_UNVERIFIED", Device360FindingPriority.Verify,
                        "Intune", "Last-sync time is in the future.",
                        "Verify clock skew and source data before assessing compliance freshness.");
            }
        }

        // A WMI failure is classified by the existing typed WMI transport
        // mapper. Unknown or external strings cannot become user-visible codes.
        var wmiCode = hardwareFailureCategory switch
        {
            "AccessDenied" => "ACCESS_DENIED",
            "Timeout" => "TIMEOUT",
            "DCOM/RPCUnavailable" => "RPC_UNAVAILABLE",
            "WmiError" => "PROVIDER_ERROR",
            "Unavailable" => "UNAVAILABLE",
            _ => string.Empty
        };
        if (wmiCode.Length > 0)
        {
            var action = wmiCode switch
            {
                "ACCESS_DENIED" =>
                    "Verify the existing operator DCOM/WMI read permissions; do not grant new rights automatically.",
                "TIMEOUT" =>
                    "Check the authorized host network path and retry only by operator action.",
                "RPC_UNAVAILABLE" =>
                    "Check DNS, TCP 135, dynamic RPC and the existing firewall policy without relaxing security.",
                "PROVIDER_ERROR" =>
                    "Inspect the remote WMI service, namespace and provider using approved tools.",
                _ => "Confirm the endpoint is reachable and WMI inventory is permitted."
            };
            Add("WMI." + wmiCode, Device360FindingPriority.Action,
                "Hardware", "Explicit remote hardware inventory did not complete.",
                action);
        }

        if (hardware is not null)
        {
            if (hardware.TpmEnabled == "No")
                Add("WMI.TPM_DISABLED", Device360FindingPriority.Action,
                    "Hardware", "Remote WMI reports TPM disabled at the sampled state.",
                    "Check TPM state in approved endpoint management; no TPM secret was accessed.");
            else if (hardware.TpmEnabled != "Yes")
                Add("WMI.TPM_UNVERIFIED", Device360FindingPriority.Verify,
                    "Hardware", "TPM status was not verified by remote WMI.",
                    "Check the WMI TPM namespace and existing read permissions.");
            if (hardware.ExtendedDiagnostic.Length > 0)
                Add("WMI.PARTIAL_INVENTORY", Device360FindingPriority.Verify,
                    "Hardware", "At least one optional hardware class was unavailable.",
                    "Review the typed read-only WMI status; existing inventory remains valid.");
        }

        return issues.OrderBy(x => x.Priority).ThenBy(x => x.Code,
            StringComparer.Ordinal).ToArray();

        void AddSource(string name, Device360SourceEvidence evidence,
            MetadataFailureKind reason)
        {
            if (evidence == Device360SourceEvidence.LookupFailed)
            {
                var kind = reason == MetadataFailureKind.None
                    ? MetadataFailureKind.Unknown : reason;
                Add("SOURCE." + name + "." + ReasonCode(kind),
                    kind is MetadataFailureKind.AccessDenied or
                        MetadataFailureKind.SignInRequired or
                        MetadataFailureKind.SourceOffline or
                        MetadataFailureKind.Timeout or
                        MetadataFailureKind.RateLimited
                        ? Device360FindingPriority.Action :
                        Device360FindingPriority.Verify,
                    name, MetadataFailureGuidance.Label(kind),
                    MetadataFailureGuidance.NextAction(kind, name));
            }
            else if (evidence != Device360SourceEvidence.FoundInSearch)
            {
                Add("SOURCE." + name + ".UNVERIFIED",
                    Device360FindingPriority.Verify, name,
                    "This exact device was not verified in the source.",
                    "Use an explicit metadata search or approved connection; a missing record is not proven.");
            }
        }
    }

    private static string ReasonCode(MetadataFailureKind kind) => kind switch
    {
        MetadataFailureKind.NoRecord => "NO_RECORD",
        MetadataFailureKind.AccessDenied => "ACCESS_DENIED",
        MetadataFailureKind.SignInRequired => "SIGN_IN_REQUIRED",
        MetadataFailureKind.SourceOffline => "SOURCE_OFFLINE",
        MetadataFailureKind.Timeout => "TIMEOUT",
        MetadataFailureKind.DecryptionFailed => "DECRYPTION_FAILED",
        MetadataFailureKind.RateLimited => "RATE_LIMITED",
        _ => "UNKNOWN"
    };

    internal static string Format(IReadOnlyList<Device360DiagnosticFinding> findings,
        string? language)
    {
        var code = UiLanguageService.Normalize(language);
        var title = code switch
        {
            "ru" => "Smart Diagnostics — рекомендуемые действия",
            "he" => "אבחון חכם — פעולות מומלצות",
            _ => "Smart Diagnostics — recommended actions"
        };
        if (findings.Count == 0)
        {
            return title + Environment.NewLine + (code switch
            {
                "ru" => "Нет дополнительных замечаний. Неизвестные источники и права восстановления всё равно требуют проверки.",
                "he" => "אין ממצאים נוספים. יש לאמת מקורות לא ידועים והרשאות שחזור.",
                _ => "No additional findings. Unverified recovery access and sources still require validation."
            });
        }

        var actionTitle = code switch
        {
            "ru" => "Проверьте", "he" => "בדיקה", _ => "Verify"
        };
        return title + Environment.NewLine + Environment.NewLine +
            string.Join(Environment.NewLine + Environment.NewLine,
                findings.Select(x =>
                {
                    var detail = LocalizeFinding(x, code);
                    var priority = code switch
                    {
                        "ru" => x.Priority == Device360FindingPriority.Action ?
                            "Требует действия" : "Проверить",
                        "he" => x.Priority == Device360FindingPriority.Action ?
                            "נדרשת פעולה" : "לאמת",
                        _ => x.Priority.ToString()
                    };
                    return "[" + x.Code + "] " + x.Source +
                        " (" + priority + ")" + Environment.NewLine +
                        detail.Explanation + Environment.NewLine +
                        actionTitle + ": " + detail.NextAction;
                }));
    }

    private static (string Explanation, string NextAction) LocalizeFinding(
        Device360DiagnosticFinding finding, string language)
    {
        // Source-specific reason codes share one safe translation per reason.
        // Neither device identifiers nor raw server exceptions are interpolated.
        var code = finding.Code.StartsWith("SOURCE.",
            StringComparison.Ordinal)
            ? "SOURCE." + finding.Code[(finding.Code.LastIndexOf('.') + 1)..]
            : finding.Code;
        if (language == "ru")
            return code switch
            {
                "IDENTITY.CONFLICT" =>
                    ("Обнаружены дублирующиеся или конфликтующие идентификаторы.",
                     "Сверьте точные GUID AD, Entra Device ID и Intune Managed Device ID, не связывайте объекты по имени."),
                "IDENTITY.UNVERIFIED" =>
                    ("Точный идентификатор компьютера не подтверждён.",
                     "Выберите запись из авторитетного источника, прежде чем открывать восстановление."),
                "SOURCE.UNVERIFIED" =>
                    ("Этот компьютер не подтверждён в указанном источнике.",
                     "Выполните явный поиск метаданных; отсутствие записи ещё не доказано."),
                "SOURCE.NO_RECORD" =>
                    ("Запись не найдена в доступной области поиска.",
                     "Проверьте точный ID и область поиска; это не означает отсутствия резервного ключа."),
                "SOURCE.ACCESS_DENIED" =>
                    ("Недостаточно разрешений для чтения метаданных.",
                     "Проверьте делегированные права AD или Graph/Intune RBAC; не повышайте привилегии автоматически."),
                "SOURCE.SIGN_IN_REQUIRED" =>
                    ("Для источника требуется подтверждённое подключение.",
                     "Явно подключите нужную учётную запись, соблюдая MFA и политику доступа."),
                "SOURCE.SOURCE_OFFLINE" =>
                    ("Источник временно недоступен.",
                     "Проверьте DNS, сеть, TLS и доступность сервиса, затем повторите запрос метаданных."),
                "SOURCE.TIMEOUT" =>
                    ("Источник не ответил в заданное время.",
                     "Проверьте соединение и повторите только чтение метаданных."),
                "SOURCE.RATE_LIMITED" =>
                    ("Источник ограничил частоту запросов.",
                     "Соблюдайте Retry-After; не повторяйте чтение секретов."),
                "SOURCE.DECRYPTION_FAILED" =>
                    ("Проверенная операция расшифровки завершилась ошибкой.",
                     "Проверьте разрешённые права LAPS и доступность GKDI, не записывайте содержимое пароля."),
                "SOURCE.UNKNOWN" =>
                    ("Причина ошибки источника не определена.",
                     "Откройте обезличенную диагностику и проверьте подключение и права."),
                "BITLOCKER.METADATA_INCOMPLETE" =>
                    ("Метаданные резервной копии BitLocker неполные.",
                     "Проверьте точный ID устройства и источник хранения; наличие ключа и права чтения не подтверждены."),
                "LAPS.EXPIRED" =>
                    ("Источник сообщает об истечении срока действия LAPS.",
                     "Проверьте состояние и политику через авторизованную диагностику LAPS."),
                "LAPS.UNVERIFIED" =>
                    ("Наличие резервной копии LAPS не подтверждено.",
                     "Проверьте информацию в разделе LAPS, не считайте пароль отсутствующим."),
                "INTUNE.NONCOMPLIANT" =>
                    ("Intune сообщает о несоответствии требованиям.",
                     "Проверьте назначенные политики соответствия в разрешённом сеансе Intune."),
                "INTUNE.ENCRYPTION_REPORTED_OFF" =>
                    ("Intune сообщает, что шифрование отключено.",
                     "Проверьте реальное состояние BitLocker разрешёнными средствами."),
                "INTUNE.STALE_SYNC" =>
                    ("С последней синхронизации Intune прошло более 14 дней.",
                     "Проверьте доступность, регистрацию и состояние управления устройством."),
                "INTUNE.CLOCK_UNVERIFIED" =>
                    ("Дата синхронизации Intune находится в будущем.",
                     "Проверьте время устройства и достоверность данных источника."),
                "WMI.ACCESS_DENIED" =>
                    ("У текущего пользователя недостаточно прав WMI.",
                     "Проверьте существующие DCOM/WMI-права на чтение без обхода безопасности."),
                "WMI.TIMEOUT" =>
                    ("Удалённый WMI не ответил вовремя.",
                     "Проверьте доступность узла; повторите запрос только по кнопке."),
                "WMI.RPC_UNAVAILABLE" =>
                    ("Подключение DCOM/RPC к компьютеру недоступно.",
                     "Проверьте DNS, TCP 135, динамические RPC-порты и действующие правила межсетевого экрана."),
                "WMI.PROVIDER_ERROR" =>
                    ("Провайдер WMI вернул ошибку.",
                     "Проверьте службу, пространство имён и поставщик WMI."),
                "WMI.UNAVAILABLE" =>
                    ("Не удалось выполнить удалённую проверку WMI.",
                     "Проверьте доступность компьютера и разрешения WMI на чтение."),
                "WMI.TPM_DISABLED" =>
                    ("WMI сообщает, что TPM отключён.",
                     "Проверьте TPM разрешённым инструментом управления компьютером."),
                "WMI.TPM_UNVERIFIED" =>
                    ("Статус TPM не подтверждён.",
                     "Проверьте пространство имён TPM и права WMI на чтение."),
                "WMI.PARTIAL_INVENTORY" =>
                    ("Часть аппаратных данных недоступна.",
                     "Проверьте отмеченные классы WMI; успешно полученные данные сохранены."),
                _ => (finding.Explanation, finding.NextAction)
            };
        if (language == "he")
            return code switch
            {
                "IDENTITY.CONFLICT" =>
                    ("זוהו מזהי מכשיר כפולים או סותרים.",
                     "השוו GUID של AD, מזהה מכשיר Entra ומזהה Intune מדויקים. אין לקשר לפי שם."),
                "IDENTITY.UNVERIFIED" =>
                    ("זהות המכשיר לא אומתה באופן חד-משמעי.",
                     "בחרו רשומה מוסמכת לפני פתיחת תהליך שחזור."),
                "SOURCE.UNVERIFIED" =>
                    ("המכשיר לא אומת במקור זה.",
                     "בצעו חיפוש נתונים מפורש. לא הוכח שהרשומה חסרה."),
                "SOURCE.NO_RECORD" =>
                    ("לא נמצאה רשומה בתחום החיפוש הזמין.",
                     "בדקו מזהה והיקף חיפוש מדויקים; אין זו הוכחה להיעדר מפתח גיבוי."),
                "SOURCE.ACCESS_DENIED" =>
                    ("אין הרשאת קריאה מספקת למקור הנתונים.",
                     "בדקו הרשאות AD או Graph/Intune הקיימות ללא הרחבת הרשאות אוטומטית."),
                "SOURCE.SIGN_IN_REQUIRED" =>
                    ("נדרש חיבור מאומת למקור הנתונים.",
                     "התחברו באופן מפורש באמצעות החשבון המתאים ותהליך MFA מאושר."),
                "SOURCE.SOURCE_OFFLINE" =>
                    ("מקור הנתונים אינו זמין כעת.",
                     "בדקו DNS, רשת, TLS וזמינות השירות לפני ניסיון קריאת נתונים נוסף."),
                "SOURCE.TIMEOUT" =>
                    ("המקור לא הגיב בזמן.",
                     "בדקו חיבור ונסו שוב קריאת נתונים בלבד."),
                "SOURCE.RATE_LIMITED" =>
                    ("המקור הגביל את קצב הבקשות.",
                     "כבדו Retry-After ואל תחזרו על קריאת סודות."),
                "SOURCE.DECRYPTION_FAILED" =>
                    ("פעולת פענוח מורשית נכשלה.",
                     "בדקו הרשאות LAPS ותקשורת GKDI ללא חשיפת הסוד."),
                "SOURCE.UNKNOWN" =>
                    ("סיבת כשל המקור אינה ידועה.",
                     "עיינו באבחון ללא פרטים מזהים ובדקו תקשורת והרשאות."),
                "BITLOCKER.METADATA_INCOMPLETE" =>
                    ("נתוני הגיבוי של BitLocker אינם מלאים.",
                     "בדקו מזהה מדויק ומקור גיבוי; זמינות המפתח והרשאות הקריאה לא אומתו."),
                "LAPS.EXPIRED" =>
                    ("תוקף נתוני LAPS המדווחים פג.",
                     "בדקו מדיניות וגיבוי דרך אבחון LAPS מורשה."),
                "LAPS.UNVERIFIED" =>
                    ("גיבוי LAPS לא אומת בחיפוש זה.",
                     "בדקו בכרטיסיית LAPS. אין להניח שהסיסמה חסרה."),
                "INTUNE.NONCOMPLIANT" =>
                    ("Intune מדווח על אי-תאימות למדיניות.",
                     "בדקו את מדיניות התאימות בחיבור Intune מורשה."),
                "INTUNE.ENCRYPTION_REPORTED_OFF" =>
                    ("Intune מדווח שההצפנה כבויה.",
                     "אמתו את מצב BitLocker בפועל בתהליך מורשה."),
                "INTUNE.STALE_SYNC" =>
                    ("חלפו יותר מ-14 יום מאז סנכרון Intune.",
                     "בדקו קישוריות, רישום וניהול המכשיר."),
                "INTUNE.CLOCK_UNVERIFIED" =>
                    ("זמן הסנכרון המדווח נמצא בעתיד.",
                     "בדקו שעון ונתוני מקור לפני הסקת מסקנות."),
                "WMI.ACCESS_DENIED" =>
                    ("לחשבון הנוכחי אין הרשאת WMI מתאימה.",
                     "בדקו הרשאות קריאה קיימות של DCOM/WMI ללא עקיפת אבטחה."),
                "WMI.TIMEOUT" =>
                    ("שאילתת WMI מרוחקת חרגה מזמן ההמתנה.",
                     "בדקו קישוריות ונסו שוב רק בפעולה מפורשת."),
                "WMI.RPC_UNAVAILABLE" =>
                    ("קישור DCOM/RPC למכשיר אינו זמין.",
                     "בדקו DNS, TCP 135, פורטי RPC דינמיים וכללי חומת אש קיימים."),
                "WMI.PROVIDER_ERROR" =>
                    ("ספק WMI החזיר שגיאה.",
                     "בדקו שירות WMI, מרחב שמות וספק."),
                "WMI.UNAVAILABLE" =>
                    ("בדיקת WMI מרוחקת אינה זמינה.",
                     "בדקו זמינות המכשיר והרשאות קריאה של WMI."),
                "WMI.TPM_DISABLED" =>
                    ("WMI מדווח שה-TPM כבוי.",
                     "בדקו מצב TPM בכלי ניהול מורשה."),
                "WMI.TPM_UNVERIFIED" =>
                    ("מצב TPM לא אומת.",
                     "בדקו מרחב שמות TPM והרשאות קריאה ב-WMI."),
                "WMI.PARTIAL_INVENTORY" =>
                    ("חלק מנתוני החומרה אינם זמינים.",
                     "בדקו מחלקות WMI שדווחו; נתונים שנאספו בהצלחה נשמרו."),
                _ => (finding.Explanation, finding.NextAction)
            };
        return (finding.Explanation, finding.NextAction);
    }
}
