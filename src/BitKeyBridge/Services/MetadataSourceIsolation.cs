namespace BitKeyBridge;

/// <summary>
/// A failure of one metadata source must not discard results from another.
/// Cancellation is never converted into an empty result.
/// </summary>
public sealed record MetadataLookupResult<T>(List<T> Rows, string? Error)
{
    public bool Succeeded => Error is null;
}

public static class MetadataSourceIsolation
{
    public static async Task<MetadataLookupResult<T>> ExecuteAsync<T>(
        Func<Task<List<T>>> lookup,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var rows = await lookup();
            cancellationToken.ThrowIfCancellationRequested();
            return new MetadataLookupResult<T>(rows, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new MetadataLookupResult<T>(
                [],
                DiagnosticRedaction.Sanitize(ex.Message));
        }
    }
}
