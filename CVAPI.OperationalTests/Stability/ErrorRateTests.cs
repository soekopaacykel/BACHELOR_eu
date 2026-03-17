using System.Diagnostics;
using System.Net;

namespace CVAPI.OperationalTests.Stability;

[Collection("Stability")]
[Trait("Category", "Stability")]
/// <summary>
/// S3 — Error Rate under Load
/// Sender X samtidige requests for X ∈ {10, 50, 100} og beregner fejlprocenten.
/// Output: fejl% per load-niveau.
/// </summary>
public class ErrorRateTests
{
    private readonly TestConfig _config;
    private readonly TestEnvironment _env;

    public ErrorRateTests()
    {
        _config = TestConfig.Instance;
        _env = TestConfig.CurrentEnvironment;
    }

    [Fact]
    [Trait("Category", "Stability")]
    [Trait("TestId", "S3")]
    public async Task ErrorRate_UnderLoad_ShouldBeAcceptable()
    {
        var url = _config.GetBaseUrl(_env) + _config.GetHealthEndpoint(_env);
        var concurrentLevels = _config.ConcurrentUsers;

        foreach (var concurrentUsers in concurrentLevels)
        {
            await RunLoadLevel(url, concurrentUsers);
        }
    }

    private async Task RunLoadLevel(string url, int concurrentUsers)
    {
        // Ny HttpClient per load-niveau for at undgå connection pool-problemer
        using var client = new HttpClient
        {
            Timeout = TimeSpan.FromMilliseconds(_config.TimeoutMs)
        };

        var tasks = Enumerable.Range(0, concurrentUsers)
            .Select(_ => SendRequest(client, url))
            .ToList();

        var results = await Task.WhenAll(tasks);

        var total = results.Length;
        var errors = results.Count(r => !r.success);
        var errorRate = (double)errors / total * 100;

        var avgMs = results.Where(r => r.success && r.elapsedMs > 0)
                           .Select(r => (double)r.elapsedMs)
                           .DefaultIfEmpty(0)
                           .Average();

        Report.Record(new TestResult
        {
            TestName = $"S3_ErrorRate",
            TestCategory = "Stability",
            Environment = _env,
            Passed = errorRate <= 10.0, // Acceptgrænse: max 10% fejl
            ValuePercent = errorRate,
            ConcurrentUsers = concurrentUsers,
            Notes = $"{concurrentUsers} samtidige | {errors}/{total} fejl | {errorRate:F1}% | avg={avgMs:F0}ms",
            Metrics = new Dictionary<string, double>
            {
                ["ConcurrentUsers"] = concurrentUsers,
                ["TotalRequests"] = total,
                ["ErrorCount"] = errors,
                ["ErrorRate_pct"] = errorRate,
                ["AvgResponseMs"] = avgMs
            }
        });

        errorRate.Should().BeLessThanOrEqualTo(10.0,
            because: $"[{_env}] Fejlrate ved {concurrentUsers} samtidige requests er {errorRate:F1}% — over 10%-grænsen");
    }

    private static async Task<(bool success, long elapsedMs)> SendRequest(HttpClient client, string url)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var response = await client.GetAsync(url);
            sw.Stop();
            var isSuccess = (int)response.StatusCode < 500;
            return (isSuccess, sw.ElapsedMilliseconds);
        }
        catch
        {
            sw.Stop();
            return (false, sw.ElapsedMilliseconds);
        }
    }
}
