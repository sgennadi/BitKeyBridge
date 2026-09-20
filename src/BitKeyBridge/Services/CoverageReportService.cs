namespace BitKeyBridge;

public static class CoverageReportService
{
    public static void WriteResult(
        CoverageResult result,
        string csvPath,
        string jsonPath,
        DateTime startedUtc,
        bool tryWriteMachineStatus = true)
    {
        WriteCsvAtomic(csvPath, result.Rows);
        JsonStore.WriteAtomic(jsonPath, result);

        if (tryWriteMachineStatus)
        {
            TryWriteStatus(new CoverageRunStatus
            {
                Success = true,
                StartedUtc = startedUtc,
                FinishedUtc = DateTime.UtcNow,
                DomainController = result.DomainController,
                CsvPath = csvPath,
                JsonPath = jsonPath,
                Summary = result.Summary
            });
        }
    }

    public static void TryWriteFailure(
        DateTime startedUtc,
        Exception error,
        string csvPath = "",
        string jsonPath = "")
    {
        TryWriteStatus(new CoverageRunStatus
        {
            Success = false,
            StartedUtc = startedUtc,
            FinishedUtc = DateTime.UtcNow,
            ErrorMessage = error.Message,
            CsvPath = csvPath,
            JsonPath = jsonPath
        });
    }

    public static CoverageRunStatus? ReadStatus()
    {
        try
        {
            return JsonStore.Read<CoverageRunStatus>(AppPaths.CoverageStatusFile);
        }
        catch
        {
            return null;
        }
    }

    private static void WriteCsvAtomic(
        string path,
        IEnumerable<CoverageDeviceRow> rows)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var temp = path + "." + Environment.ProcessId + ".tmp";
        try
        {
            CoverageService.ExportCsv(temp, rows);
            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            try
            {
                if (File.Exists(temp))
                    File.Delete(temp);
            }
            catch
            {
            }
        }
    }

    private static void TryWriteStatus(CoverageRunStatus status)
    {
        try
        {
            JsonStore.WriteAtomic(AppPaths.CoverageStatusFile, status);
        }
        catch
        {
            // A non-admin interactive coverage run may not be able to update
            // ProgramData status. Report generation should still succeed.
        }
    }
}
