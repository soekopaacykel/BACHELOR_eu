using System.Diagnostics;
using System.Net;

namespace CVAPI.OperationalTests.Availability;

/// <summary>
/// T2 — DNS Resolution Time
/// Måler DNS-opslag-tid for applikationens domæne 20 gange med op til 3 retries ved timeout.
/// Output: gennemsnitlig DNS-opslag tid (ms), antal retries.
/// </summary>
public class DNSResolutionTests
{
    private readonly TestConfig _config;
    private readonly TestEnvironment _env;
    private const int Repetitions = 20;
    private const int MaxRetries = 3;
    private const int RetryDelayMs = 2000;

    public DNSResolutionTests()
    {
        _config = TestConfig.Instance;
        _env = TestConfig.CurrentEnvironment;
    }

    [Fact]
    [Trait("Category", "Availability")]
    [Trait("TestId", "T2")]
    public async Task DnsResolution_ShouldBefast()
    {
        var host = ExtractHost(_config.GetBaseUrl(_env));
        host.Should().NotBeNullOrEmpty(because: $"[{_env}] Kunne ikke udtrække hostname");

        var timings = new List<double>();
        var totalRetries = 0;
        var errors = 0;

        for (int i = 0; i < Repetitions; i++)
        {
            var (elapsedMs, retries, success) = await ResolveDnsWithRetry(host!);

            totalRetries += retries;

            if (success)
            {
                timings.Add(elapsedMs);
            }
            else
            {
                errors++;
            }

            Report.Record(new TestResult
            {
                TestName = "T2_DnsResolution",
                TestCategory = "Availability",
                Environment = _env,
                Passed = success,
                Iteration = i,
                ValueMs = elapsedMs,
                Notes = $"host={host} | retries={retries}{(success ? "" : " | FEJL")}"
            });

            await Task.Delay(100);
        }

        timings.Should().NotBeEmpty(because: $"[{_env}] T2: Ingen succesfulde DNS-opslag ud af {Repetitions}");

        var avg = timings.Average();
        var min = timings.Min();
        var max = timings.Max();

        Report.Record(new TestResult
        {
            TestName = "T2_DnsResolution_Summary",
            TestCategory = "Availability",
            Environment = _env,
            Passed = errors == 0,
            ValueMs = avg,
            Notes = $"host={host} | n={timings.Count} | avg={avg:F1}ms | min={min:F0}ms | max={max:F0}ms | retries={totalRetries} | errors={errors}",
            Metrics = new Dictionary<string, double>
            {
                ["Avg_ms"] = avg,
                ["Min_ms"] = min,
                ["Max_ms"] = max,
                ["TotalRetries"] = totalRetries,
                ["ErrorCount"] = errors
            }
        });
    }

    private static async Task<(double elapsedMs, int retries, bool success)> ResolveDnsWithRetry(string host)
    {
        for (int attempt = 0; attempt <= MaxRetries; attempt++)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                await System.Net.Dns.GetHostAddressesAsync(host);
                sw.Stop();
                return (sw.ElapsedMilliseconds, attempt, true);
            }
            catch
            {
                sw.Stop();
                if (attempt < MaxRetries)
                    await Task.Delay(RetryDelayMs);
            }
        }
        return (0, MaxRetries, false);
    }

    private static string? ExtractHost(string baseUrl)
    {
        if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
            return uri.Host;
        return null;
    }
}
