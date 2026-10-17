using System.Net;

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
    // Only transient GET metadata failures are eligible for ONE bounded
    // retry. 400/401/403/404 and identity/permission errors are never
    // retried; secret retrieval and rotation use their own explicit flow.
    internal static bool IsTransientHttp(Exception ex) =>
        ex is HttpRequestException http &&
        http.StatusCode is
            HttpStatusCode.TooManyRequests or
            HttpStatusCode.InternalServerError or
            HttpStatusCode.BadGateway or
            HttpStatusCode.ServiceUnavailable or
            HttpStatusCode.GatewayTimeout;

    public static async Task<MetadataLookupResult<T>> ExecuteWithTransientRetryAsync<T>(
        Func<Task<List<T>>> lookup,
        CancellationToken cancellationToken)
    {
        var tries = 0;
        cancellationToken.ThrowIfCancellationRequested();
        return await ExecuteAsync(async () =>
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try { return await lookup(); }
                catch (Exception ex) when (tries == 0 && IsTransientHttp(ex) &&
                                           !cancellationToken.IsCancellationRequested)
                {
                    tries++;
                    // A single short pause reduces 429/503 bursts. Do not
                    // fan out repeated per-device key metadata requests.
                    await Task.Delay(TimeSpan.FromMilliseconds(350),
                        cancellationToken);
                }
            }
        }, cancellationToken);
    }

    internal static IEnumerable<string> RunRetryOfflineTests()
    {
        var failures = new List<string>();
        var transientCalls = 0;
        var result = ExecuteWithTransientRetryAsync(
            () =>
            {
                transientCalls++;
                if (transientCalls == 1)
                    throw new HttpRequestException("Synthetic 503", null,
                        HttpStatusCode.ServiceUnavailable);
                return Task.FromResult(new List<string> { "synthetic-device" });
            }, CancellationToken.None).GetAwaiter().GetResult();
        if (!result.Succeeded || result.Rows.Count != 1 ||
            transientCalls != 2)
            failures.Add("Transient 503 metadata request was not retried once.");

        var deniedCalls = 0;
        var denied = ExecuteWithTransientRetryAsync(
            () =>
            {
                deniedCalls++;
                return Task.FromException<List<string>>(
                    new HttpRequestException("Synthetic 403", null,
                        HttpStatusCode.Forbidden));
            }, CancellationToken.None).GetAwaiter().GetResult();
        if (deniedCalls != 1 || denied.Succeeded)
            failures.Add("Permission-denied metadata query was retried.");

        var repeatedCalls = 0;
        var repeated = ExecuteWithTransientRetryAsync(
            () =>
            {
                repeatedCalls++;
                return Task.FromException<List<string>>(
                    new HttpRequestException("Synthetic 429", null,
                        HttpStatusCode.TooManyRequests));
            }, CancellationToken.None).GetAwaiter().GetResult();
        if (repeatedCalls != 2 || repeated.Succeeded)
            failures.Add("Transient metadata failure exceeded retry budget.");

        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var cancellationReachedCaller = false;
        try
        {
            _ = ExecuteWithTransientRetryAsync(
                () => Task.FromResult(new List<string>()),
                cancelled.Token).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            cancellationReachedCaller = true;
        }
        if (!cancellationReachedCaller)
            failures.Add("Canceled metadata retry was converted into a success.");

        return failures;
    }

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
