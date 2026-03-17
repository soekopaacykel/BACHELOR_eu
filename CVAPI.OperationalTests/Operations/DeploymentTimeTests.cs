using System.Diagnostics;
using System.Net;
using CVAPI.OperationalTests.Config;
using CVAPI.OperationalTests.Reports;
using FluentAssertions;

namespace CVAPI.OperationalTests.Operations;

/// <summary>
/// D1 — Deployment Tid.
/// Poller /health og venter på at et nyt version-timestamp dukker op.
/// Kræver at en deployment sættes i gang manuelt/via pipeline FØR testen køres.
/// Sæt environment variable DEPLOY_START_UTC til ISO-timestamp (fx 2026-03-17T10:00:00Z).
/// </summary>
[Trait("Category", "Operations")]
public class DeploymentTimeTests
{
    private readonly HttpClient _http = new(new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    }) { Timeout = TimeSpan.FromSeconds(15) };

    [Theory]
    [InlineData(TestEnvironment.Azure)]
    public async Task Deployment_PollUntilNewVersionIsLive(TestEnvironment env)
    {
        var url = TestConfig.Instance.GetBaseUrl(env) + TestConfig.Instance.GetHealthEndpoint(env);

        // Hent nuværende version/timestamp som baseline
        var before = await GetHealthTimestamp(url);
        before.Should().NotBeNull(because: "applikationen skal svare inden deployment startes");

        var deployStartStr = Environment.GetEnvironmentVariable("DEPLOY_START_UTC");
        if (string.IsNullOrEmpty(deployStartStr))
        {
            // Ingen deployment i gang — skip med note
            Report.Record(new TestResult
            {
                TestName = "DeploymentTime",
                TestCategory = "Operations",
                Environment = env,
                Passed = true,
                Notes = "SKIP: Sæt DEPLOY_START_UTC=<ISO-timestamp> og kør en deployment for at måle deployment-tid"
            });
            return;
        }

        var deployStart = DateTime.Parse(deployStartStr, null, System.Globalization.DateTimeStyles.RoundtripKind);
        var sw = Stopwatch.StartNew();
        var maxWait = TimeSpan.FromMinutes(15);

        while (sw.Elapsed < maxWait)
        {
            await Task.Delay(10_000); // Poll hvert 10. sekund
            var current = await GetHealthTimestamp(url);
            if (current.HasValue && current.Value > before!.Value)
            {
                var totalSeconds = (DateTime.UtcNow - deployStart).TotalSeconds;

                Report.Record(new TestResult
                {
                    TestName = "DeploymentTime",
                    TestCategory = "Operations",
                    Environment = env,
                    Passed = true,
                    ValueMs = totalSeconds * 1000,
                    Notes = $"Deployment tid: {totalSeconds:F0}s | Ny version live: {current.Value:HH:mm:ss}"
                });
                return;
            }
        }

        Report.Record(new TestResult
        {
            TestName = "DeploymentTime",
            TestCategory = "Operations",
            Environment = env,
            Passed = false,
            Notes = $"Timeout: ingen ny version inden for {maxWait.TotalMinutes} minutter"
        });

        Assert.Fail($"[{env}] Deployment ikke registreret inden for {maxWait.TotalMinutes} minutter");
    }

    private async Task<DateTime?> GetHealthTimestamp(string url)
    {
        try
        {
            var response = await _http.GetAsync(url);
            if (response.StatusCode != HttpStatusCode.OK) return null;

            var body = await response.Content.ReadAsStringAsync();
            using var doc = System.Text.Json.JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("timestamp", out var ts) &&
                ts.TryGetDateTime(out var dt))
                return dt;
        }
        catch { /* ignore */ }
        return null;
    }
}
