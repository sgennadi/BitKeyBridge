using System.Text;

namespace BitKeyBridge;

public static class CsvUtility
{
    private static string Quote(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";

    public static void WriteRecoveryCsvAtomic(string path, IReadOnlyCollection<RecoveryRecord> rows)
    {
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        var temp = Path.Combine(directory, $".{Path.GetFileName(path)}.{Environment.MachineName}.{Environment.ProcessId}.tmp");

        try
        {
            using (var writer = new StreamWriter(temp, false, new UTF8Encoding(true)))
            {
                writer.WriteLine("\"ComputerName\",\"BitLockerID\",\"RecoveryKey\",\"LastChecked\"");
                foreach (var row in rows)
                {
                    writer.WriteLine(string.Join(",",
                        Quote(row.ComputerName),
                        Quote(row.BitLockerId),
                        Quote(row.RecoveryKey),
                        Quote(row.LastChecked.ToString("yyyy-MM-dd HH:mm:ss"))));
                }
            }

            var verify = ReadRecoveryCsv(temp);
            if (verify.Count != rows.Count)
                throw new InvalidDataException($"CSV verification failed. Expected {rows.Count} rows, read {verify.Count}.");

            if (File.Exists(path)) File.Replace(temp, path, null);
            else File.Move(temp, path);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    public static int CountDataRows(string path)
    {
        if (!File.Exists(path)) return 0;
        using var e = File.ReadLines(path).GetEnumerator();
        if (!e.MoveNext()) return 0;
        var count = 0;
        while (e.MoveNext()) count++;
        return count;
    }

    public static List<RecoveryRecord> ReadRecoveryCsv(string path)
    {
        var result = new List<RecoveryRecord>();
        if (!File.Exists(path)) return result;
        var lines = File.ReadLines(path).Skip(1);
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var fields = ParseLine(line);
            if (fields.Count < 4) continue;
            DateTime.TryParse(fields[3], out var checkedAt);
            result.Add(new RecoveryRecord(fields[0], fields[1], fields[2], checkedAt));
        }
        return result;
    }

    public static List<string> ParseLine(string line)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (ch == ',' && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(ch);
            }
        }
        result.Add(current.ToString());
        return result;
    }
}
