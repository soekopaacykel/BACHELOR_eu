using System.Diagnostics;
using System.Net;
using CVAPI.OperationalTests.Config;
using CVAPI.OperationalTests.Reports;
using FluentAssertions;

namespace CVAPI.OperationalTests.Availability;

/// <summary>
/// T1 — Uptime Monitoring.
/// Poller /health hvert UptimeIntervalSeconds i UptimeDurationMinutes minutter.
/// OBS: Default er 1440 min (24h) — kør separat med f.eks. TEST_UPTIME_MINUTES=5 for hurtig test.
/// </summary>
[Trait("Category", "Availability")]
public class UptimeMonitorTests
{
    private readonly HttpClient _http = new(new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    }) { Timeout = TimeSpan.FromMilliseconds(TestConfig.Instance.TimeoutMs) };

    [Theory]
    [InlineData(TestEnvironment.Azure)]
    public async Task UptimeMonitor_ShouldMeetUptimeThreshold(TestEnvironment env)
    {
        var url = TestConfig.Instance.GetBaseUrl(env) + TestConfig.Instance.GetHealthEndpoint(env);

        // Tillad override via env-var for kortere testkørsler
        var durationMinutes = int.TryParse(
            Environment.GetEnvironmentVariable("TEST_UPTIME_MINUTES"), out var m)
            ? m : TestConfig.Instance.UptimeDurationMinutes;

        var intervalSeconds = TestConfig.Instance.UptimeIntervalSeconds;
        var totalChecks = (durationMinutes * 60) / intervalSeconds;

        int success = 0, failure = 0;

        for (int i = 0; i < totalChecks; i++)
        {
            bool ok;
            try
            {
                var response = await _http.GetAsync(url);
                ok = response.StatusCode == HttpStatusCode.OK;
            }
            catch
            {
                ok = false;
            }

            if (ok) success++; else failure++;

            Report.Record(new TestResult
            {
                TestName = "UptimeMonitor",
                TestCategory = "Availability",
                Environment = env,
                Passed = ok,
                Iteration = i + 1,
                Notes = ok ? "OK" : "FAIL"
            });

            if (i < totalChecks - 1)
                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds));
        }

        var uptimePercent = (double)success / totalChecks * 100;

        Report.Record(new TestResult
        {
            TestName = "UptimeMonitor_Summary",
            TestCategory = "Availability",
            Environment = env,
            Passed = uptimePercent >= 99.0,
            ValuePercent = uptimePercent,
            Notes = $"{success}/{totalChecks} checks OK → {uptimePercent:F2}% uptime"
        });

        uptimePercent.Should().BeGreaterOrEqualTo(99.0,
            because: $"[{env}] uptime skal være mindst 99% ({success}/{totalChecks} checks OK)");
    }
}
