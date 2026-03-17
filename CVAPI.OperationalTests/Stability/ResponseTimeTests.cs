using System.Diagnostics;
using System.Net;
using CVAPI.OperationalTests.Config;
using CVAPI.OperationalTests.Reports;
using FluentAssertions;

namespace CVAPI.OperationalTests.Stability;

[Trait("Category", "Stability")]
public class ResponseTimeTests
{
    private readonly HttpClient _http = new(new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    }) { Timeout = TimeSpan.FromMilliseconds(TestConfig.Instance.TimeoutMs) };

    [Theory]
    [InlineData(TestEnvironment.Azure)]
    public async Task ResponseTime_ConsistencyOver50Requests(TestEnvironment env)
    {
        var url = TestConfig.Instance.GetBaseUrl(env) + TestConfig.Instance.GetHealthEndpoint(env);
        var n = TestConfig.Instance.RepeatCount;
        var measurements = new List<double>(n);

        // 5 warm-up requests
        for (int i = 0; i < 5; i++)
            await _http.GetAsync(url);

        for (int i = 0; i < n; i++)
        {
            var sw = Stopwatch.StartNew();
            var response = await _http.GetAsync(url);
            sw.Stop();

            if (response.StatusCode == HttpStatusCode.OK)
                measurements.Add(sw.Elapsed.TotalMilliseconds);

            Report.Record(new TestResult
            {
                TestName = "ResponseTime",
                TestCategory = "Stability",
                Environment = env,
                Passed = response.StatusCode == HttpStatusCode.OK,
                ValueMs = sw.Elapsed.TotalMilliseconds,
                Iteration = i + 1
            });
        }

        measurements.Should().NotBeEmpty();

        var sorted = measurements.OrderBy(x => x).ToList();
        var mean = sorted.Average();
        var median = sorted[sorted.Count / 2];
        var p95 = sorted[(int)(sorted.Count * 0.95)];
        var p99 = sorted[(int)(sorted.Count * 0.99)];
        var stddev = Math.Sqrt(sorted.Average(x => Math.Pow(x - mean, 2)));

        Report.Record(new TestResult
        {
            TestName = "ResponseTime_Summary",
            TestCategory = "Stability",
            Environment = env,
            Passed = true,
            ValueMs = mean,
            Metrics = new Dictionary<string, double>
            {
                ["Mean_ms"] = mean,
                ["Median_ms"] = median,
                ["P95_ms"] = p95,
                ["P99_ms"] = p99,
                ["StdDev_ms"] = stddev
            },
            Notes = $"n={measurements.Count} | Mean:{mean:F1}ms | P95:{p95:F1}ms | P99:{p99:F1}ms | StdDev:{stddev:F1}ms"
        });
    }
}
