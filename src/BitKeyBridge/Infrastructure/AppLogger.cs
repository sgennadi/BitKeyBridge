namespace BitKeyBridge;

public sealed class AppLogger
{
    private readonly string _path;
    private readonly int _maxSizeMb;
    private readonly object _sync = new();

    public AppLogger(string path, int maxSizeMb)
    {
        _path = path;
        _maxSizeMb = Math.Max(1, maxSizeMb);
    }

    public void Initialize()
    {
        var dir = Path.GetDirectoryName(_path)!;
        Directory.CreateDirectory(dir);
        if (File.Exists(_path) && new FileInfo(_path).Length >= _maxSizeMb * 1024L * 1024L)
        {
            var old = _path + ".old";
            if (File.Exists(old)) File.Delete(old);
            File.Move(_path, old);
        }
        if (!File.Exists(_path)) File.WriteAllText(_path, string.Empty);
    }

    public void Info(string message) => Write("INFO", message);
    public void Warning(string message) => Write("WARNING", message);
    public void Error(string message) => Write("ERROR", message);

    private void Write(string level, string message)
    {
        lock (_sync)
        {
            try
            {
                File.AppendAllText(_path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}{Environment.NewLine}");
            }
            catch
            {
                // Logging must never break the export path.
            }
        }
    }
}
