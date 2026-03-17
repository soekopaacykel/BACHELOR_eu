using System.Diagnostics;
using System.Net;
using CVAPI.OperationalTests.Config;
using CVAPI.OperationalTests.Reports;
using FluentAssertions;

namespace CVAPI.OperationalTests.Stability;

/// <summary>
/// S4 — Database Resilience Test.
/// Kalder et readonly endpoint N gange og observerer om svartider forværres over tid.
/// Readonly endpoint: /api/competencies/eu/predefined (kræver ingen auth, returnerer statisk data).
/// </summary>
[Trait("Category", "Stability")]
public class DatabaseResilienceTests
{
    private readonly HttpClient _http = new(new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    }) { Timeout = TimeSpan.FromMilliseconds(TestConfig.Instance.TimeoutMs) };

    [Theory]
    [InlineData(TestEnvironment.Azure)]
    public async Task DatabaseResilience_ReadonlyEndpointStableOverTime(TestEnvironment env)
    {
        var baseUrl = TestConfig.Instance.GetBaseUrl(env);
        var endpoint = "/api/competencies/eu/predefined";
        var url = baseUrl + endpoint;
        var n = TestConfig.Instance.RepeatCount;

        var measurements = new List<double>(n);
        var errors = 0;

        for (int i = 0; i < n; i++)
        {
            try
            {
                var sw = Stopwatch.StartNew();
                var response = await _http.GetAsync(url);
                sw.Stop();

                var success = response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Unauthorized;
                if (!success) errors++;

                measurements.Add(sw.Elapsed.TotalMilliseconds);

                Report.Record(new TestResult
                {
                    TestName = "DatabaseResilience",
                    TestCategory = "Stability",
                    Environment = env,
                    Passed = success,
                    ValueMs = sw.Elapsed.TotalMilliseconds,
                    Iteration = i + 1,
                    ErrorMessage = success ? null : $"HTTP {(int)response.StatusCode}"
                });

                await Task.Delay(500); // 500ms pause mellem kald for at undgå rate-limiting
            }
            catch (Exception ex)
            {
                errors++;
                Report.Record(new TestResult
                {
                    TestName = "DatabaseResilience",
                    TestCategory = "Stability",
                    Environment = env,
                    Passed = false,
                    Iteration = i + 1,
                    ErrorMessage = ex.Message
                });
            }
        }

        var errorRate = (double)errors / n * 100;
        errorRate.Should().BeLessThan(5, because: "DB-forbindelsen må have max 5% fejlrate");

        // Tjek at svartider ikke drifter: sammenlign første 10 vs. sidste 10
        if (measurements.Count >= 20)
        {
            var first10 = measurements.Take(10).Average();
            var last10 = measurements.TakeLast(10).Average();
            var drift = last10 - first10;

            Report.Record(new TestResult
            {
                TestName = "DatabaseResilience_Drift",
                TestCategory = "Stability",
                Environment = env,
                Passed = drift < 2000,
                Metrics = new Dictionary<string, double>
                {
                    ["First10_Mean_ms"] = first10,
                    ["Last10_Mean_ms"] = last10,
                    ["Drift_ms"] = drift
                },
                Notes = $"Drift: {drift:F1}ms (første 10: {first10:F1}ms → sidste 10: {last10:F1}ms)"
            });
        }
    }
}
