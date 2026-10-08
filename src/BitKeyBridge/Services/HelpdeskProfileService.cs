namespace BitKeyBridge;

public sealed class HelpdeskProfileService
{
    private readonly string _path;

    public HelpdeskProfileService(
        string? path = null)
    {
        _path =
            path ??
            AppPaths.HelpdeskProfilesFile;
    }

    public List<HelpdeskProfile> Load()
    {
        try
        {
            return (
                    JsonStore.Read<List<HelpdeskProfile>>(
                        _path) ??
                    [])
                .Where(
                    x =>
                        !string.IsNullOrWhiteSpace(
                            x.Name))
                .OrderBy(
                    x =>
                        x.Name,
                    StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    public void Save(
        HelpdeskProfile profile)
    {
        if (string.IsNullOrWhiteSpace(
                profile.Name))
        {
            throw new ArgumentException(
                "Profile name is required.");
        }

        profile.Name =
            profile.Name.Trim();
        profile.UpdatedAtUtc =
            DateTime.UtcNow;

        var rows =
            Load();

        rows.RemoveAll(
            x =>
                x.Name.Equals(
                    profile.Name,
                    StringComparison.OrdinalIgnoreCase));

        rows.Add(
            profile);

        JsonStore.WriteAtomic(
            _path,
            rows
                .OrderBy(
                    x =>
                        x.Name,
                    StringComparer.OrdinalIgnoreCase)
                .ToList());
    }

    public void Delete(
        string name)
    {
        var rows =
            Load();

        rows.RemoveAll(
            x =>
                x.Name.Equals(
                    name,
                    StringComparison.OrdinalIgnoreCase));

        JsonStore.WriteAtomic(
            _path,
            rows);
    }
}
