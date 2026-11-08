using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;

namespace Core.BruteForce;

public sealed class BruteForceSimulator(
    HttpClient httpClient,
    Func<string, CancellationToken, Task<IPAddress[]>>? resolveHost = null)
{
    private readonly Func<string, CancellationToken, Task<IPAddress[]>> _resolveHost =
        resolveHost ?? ((host, ct) => Dns.GetHostAddressesAsync(host, ct));
    private sealed record LoginPayload(string Username, string Password);

    public async Task<BruteForceResult> RunAsync(BruteForceOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ValidateOptions(options);
        await EnsureTargetAllowedAsync(options.TargetUrl, cancellationToken);

        var stopwatch = Stopwatch.StartNew();
        var limit = Math.Min(options.MaxAttempts, options.Passwords.Count);
        var attempts = 0;

        for (var i = 0; i < limit; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            HttpStatusCode status;
            try
            {
                using var response = await httpClient.PostAsJsonAsync(
                    options.TargetUrl,
                    new LoginPayload(options.Username, options.Passwords[i]),
                    cancellationToken);
                status = response.StatusCode;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // HttpClient.Timeout surfaces as a cancellation; it is a failure, not a user cancel.
                throw new TimeoutException($"Login request to '{options.TargetUrl}' timed out.");
            }

            attempts++;

            if ((int)status is >= 200 and < 300)
            {
                return new BruteForceResult(attempts, stopwatch.Elapsed, BruteForceOutcome.Found);
            }
            
            if (options.BlockedStatusCodes.Contains(status))
            {
                return new BruteForceResult(attempts, stopwatch.Elapsed, BruteForceOutcome.Blocked);
            }

            if (status != HttpStatusCode.Unauthorized)
            {
                throw new HttpRequestException(
                    $"Unexpected status {(int)status} from login endpoint; aborting simulation.",
                    inner: null,
                    statusCode: status);
            }

            if (i < limit - 1 && options.DelayBetweenAttempts > TimeSpan.Zero)
            {
                await Task.Delay(options.DelayBetweenAttempts, cancellationToken);
            }
        }
        
        return new BruteForceResult(attempts, stopwatch.Elapsed, BruteForceOutcome.Exhausted);
    }

    private async Task EnsureTargetAllowedAsync(Uri target, CancellationToken cancellationToken)
    {
        // Literal IPs skip DNS entirely; hostnames must resolve ONLY to allowed addresses.
        var addresses = IPAddress.TryParse(target.DnsSafeHost, out var literal)
            ? [literal]
            : await _resolveHost(target.DnsSafeHost, cancellationToken);

        if (addresses.Length == 0 || !addresses.All(PrivateNetworkRanges.IsAllowed))
        {
            throw new InvalidOperationException(
                $"Target '{target.Host}' is outside the allowed loopback/private ranges.");
        }
    }

    private static void ValidateOptions(BruteForceOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Username))
            throw new ArgumentException("Username is required.", nameof(options));

        if (options.Passwords.Count == 0)
            throw new ArgumentException("Password list must not be empty.", nameof(options));

        if (options.MaxAttempts <= 0)
            throw new ArgumentOutOfRangeException(nameof(options), "MaxAttempts must be greater than zero.");

        if (options.DelayBetweenAttempts < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(options), "DelayBetweenAttempts must not be negative.");

        if (!options.TargetUrl.IsAbsoluteUri || options.TargetUrl.Scheme is not ("http" or "https"))
            throw new ArgumentException("TargetUrl must be an absolute http(s) URL.", nameof(options));
    }
}