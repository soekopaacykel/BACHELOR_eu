using System.Diagnostics;
using System.Net;

namespace CVAPI.OperationalTests.Stability;

[Collection("Stability")]
[Trait("Category", "Stability")]
/// <summary>
/// S2 — Response Time Consistency Test
/// Måler svartidsvariationen over N requests med 5 warm-up requests inden målingen.
/// Output: mean, median, P95, P99, standardafvigelse.
/// </summary>
public class ResponseTimeTests
{
    private readonly HttpClient _httpClient;
    private readonly TestConfig _config;
    private readonly TestEnvironment _env;

    public ResponseTimeTests()
    {
        _config = TestConfig.Instance;
        _env = TestConfig.CurrentEnvironment;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMilliseconds(_config.TimeoutMs)
        };
    }

    [Fact]
    [Trait("Category", "Stability")]
    [Trait("TestId", "S2")]
    public async Task ResponseTime_HealthEndpoint_ShouldBeConsistent()
    {
        var url = _config.GetBaseUrl(_env) + _config.GetHealthEndpoint(_env);
        await RunResponseTimeTest("S2_ResponseTime_Health", url);
    }

    [Fact]
    [Trait("Category", "Stability")]
    [Trait("TestId", "S2")]
    public async Task ResponseTime_ConsultantsEndpoint_ShouldBeConsistent()
    {
        var url = _config.GetBaseUrl(_env) + "/api/user/eu/consultants";
        await RunResponseTimeTest("S2_ResponseTime_Consultants", url);
    }

    private async Task RunResponseTimeTest(string testName, string url)
    {
        const int warmUpCount = 5;
        var repeatCount = _config.RepeatCount;

        // Warm-up: tælles ikke med i målingen
        for (int i = 0; i < warmUpCount; i++)
        {
            try { await _httpClient.GetAsync(url); } catch { /* ignorer warm-up fejl */ }
        }

        // Selve målingen
        var timings = new List<double>();
        int errors = 0;

        for (int i = 0; i < repeatCount; i++)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                var response = await _httpClient.GetAsync(url);
                sw.Stop();

                if (response.StatusCode == HttpStatusCode.OK ||
                    response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    timings.Add(sw.ElapsedMilliseconds);
                }
                else
                {
                    errors++;
                }
            }
            catch
            {
                sw.Stop();
                errors++;
            }
        }

        timings.Should().NotBeEmpty(because: $"[{_env}] {testName}: Ingen succesfulde requests ud af {repeatCount}");

        var sorted = timings.OrderBy(t => t).ToList();
        var mean = timings.Average();
        var median = Percentile(sorted, 50);
        var p95 = Percentile(sorted, 95);
        var p99 = Percentile(sorted, 99);
        var stdDev = Math.Sqrt(timings.Average(t => Math.Pow(t - mean, 2)));

        Report.Record(new TestResult
        {
            TestName = testName,
            TestCategory = "Stability",
            Environment = _env,
            Passed = errors == 0,
            ValueMs = mean,
            Notes = $"n={timings.Count} | errors={errors} | median={median:F0}ms | P95={p95:F0}ms | P99={p99:F0}ms | stddev={stdDev:F1}ms",
            Metrics = new Dictionary<string, double>
            {
                ["Mean_ms"] = mean,
                ["Median_ms"] = median,
                ["P95_ms"] = p95,
                ["P99_ms"] = p99,
                ["StdDev_ms"] = stdDev,
                ["ErrorCount"] = errors
            }
        });

        // P95 bør ikke overstige 5 sekunder — alarmgrænse
        p95.Should().BeLessThan(5000,
            because: $"[{_env}] {testName}: P95 på {p95:F0}ms overstiger 5000ms-grænsen");
    }

    private static double Percentile(List<double> sorted, int percentile)
    {
        if (sorted.Count == 0) return 0;
        var index = (percentile / 100.0) * (sorted.Count - 1);
        var lower = (int)Math.Floor(index);
        var upper = (int)Math.Ceiling(index);
        if (lower == upper) return sorted[lower];
        return sorted[lower] + (index - lower) * (sorted[upper] - sorted[lower]);
    }
}
