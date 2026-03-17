using System.Diagnostics;
using System.Net;

namespace CVAPI.OperationalTests.Availability;

/// <summary>
/// T1 — Uptime Monitoring
/// Kalder /health hvert 30. sekund i den konfigurerede varighed (default 1440 min = 24t).
/// BEMÆRK: Til hurtig verifikation kan UptimeDurationMinutes sættes til 1 i appsettings.test.json.
/// Output: uptime-procent.
/// </summary>
[Collection("Availability")]
[Trait("Category", "Availability")]
public class UptimeMonitorTests
{
    private readonly HttpClient _httpClient;
    private readonly TestConfig _config;
    private readonly TestEnvironment _env;

    public UptimeMonitorTests()
    {
        _config = TestConfig.Instance;
        _env = TestConfig.CurrentEnvironment;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMilliseconds(_config.TimeoutMs)
        };
    }

    [Fact]
    [Trait("Category", "Availability")]
    [Trait("TestId", "T1")]
    public async Task Uptime_ShouldExceed99Percent()
    {
        var url = _config.GetBaseUrl(_env) + _config.GetHealthEndpoint(_env);
        var durationMinutes = _config.UptimeDurationMinutes;
        var intervalSeconds = _config.UptimeIntervalSeconds;

        var totalChecks = (durationMinutes * 60) / intervalSeconds;
        var successCount = 0;
        var failCount = 0;
        var firstFailAt = (int?)null;

        for (int i = 0; i < totalChecks; i++)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                var response = await _httpClient.GetAsync(url);
                sw.Stop();

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    successCount++;
                }
                else
                {
                    failCount++;
                    firstFailAt ??= i;
                }

                Report.Record(new TestResult
                {
                    TestName = "T1_Uptime",
                    TestCategory = "Availability",
                    Environment = _env,
                    Passed = response.StatusCode == HttpStatusCode.OK,
                    Iteration = i,
                    ValueMs = sw.ElapsedMilliseconds,
                    Notes = $"Check {i + 1}/{totalChecks} — HTTP {(int)response.StatusCode}"
                });
            }
            catch (Exception ex)
            {
                sw.Stop();
                failCount++;
                firstFailAt ??= i;

                Report.Record(new TestResult
                {
                    TestName = "T1_Uptime",
                    TestCategory = "Availability",
                    Environment = _env,
                    Passed = false,
                    Iteration = i,
                    ValueMs = sw.ElapsedMilliseconds,
                    Notes = $"Check {i + 1}/{totalChecks} — Exception: {ex.Message}"
                });
            }

            if (i < totalChecks - 1)
                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds));
        }

        var uptimePct = (double)successCount / totalChecks * 100;

        Report.Record(new TestResult
        {
            TestName = "T1_Uptime_Summary",
            TestCategory = "Availability",
            Environment = _env,
            Passed = uptimePct >= 99.0,
            ValuePercent = uptimePct,
            Notes = $"{successCount}/{totalChecks} checks OK | uptime={uptimePct:F2}% | firstFail={firstFailAt?.ToString() ?? "ingen"}",
            Metrics = new Dictionary<string, double>
            {
                ["UptimePct"] = uptimePct,
                ["SuccessCount"] = successCount,
                ["FailCount"] = failCount,
                ["TotalChecks"] = totalChecks
            }
        });

        uptimePct.Should().BeGreaterThanOrEqualTo(99.0,
            because: $"[{_env}] T1: Uptime {uptimePct:F2}% er under 99%-grænsen ({failCount} fejl ud af {totalChecks} checks)");
    }
}
