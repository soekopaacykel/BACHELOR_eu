using System.Diagnostics;
using System.Net;
using CVAPI.OperationalTests.Config;
using CVAPI.OperationalTests.Reports;
using FluentAssertions;

namespace CVAPI.OperationalTests.Availability;

/// <summary>
/// T5 — Endpoint Availability.
/// Verificerer at alle kendte endpoints svarer HTTP 200 eller 401 (auth-krævet er OK).
/// </summary>
[Trait("Category", "Availability")]
public class EndpointAvailabilityTests
{
    private readonly HttpClient _http = new(new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    }) { Timeout = TimeSpan.FromMilliseconds(TestConfig.Instance.TimeoutMs) };

    [Theory]
    [InlineData(TestEnvironment.Azure)]
    public async Task AllEndpoints_ShouldBeReachable(TestEnvironment env)
    {
        var baseUrl = TestConfig.Instance.GetBaseUrl(env);
        var endpoints = TestConfig.Instance.GetTestEndpoints(env);

        endpoints.Should().NotBeEmpty(because: "der skal være konfigurerede endpoints at teste");

        var failed = new List<string>();

        foreach (var endpoint in endpoints)
        {
            var url = baseUrl + endpoint;
            HttpStatusCode status;
            long elapsedMs;

            try
            {
                var sw = Stopwatch.StartNew();
                var response = await _http.GetAsync(url);
                sw.Stop();
                status = response.StatusCode;
                elapsedMs = sw.ElapsedMilliseconds;
            }
            catch (Exception ex)
            {
                Report.Record(new TestResult
                {
                    TestName = $"EndpointAvailability_{endpoint}",
                    TestCategory = "Availability",
                    Environment = env,
                    Passed = false,
                    ErrorMessage = ex.Message,
                    Notes = url
                });
                failed.Add(endpoint);
                continue;
            }

            // 200 OK eller 401 Unauthorized tæller som "tilgængelig"
            var reachable = status is HttpStatusCode.OK or HttpStatusCode.Unauthorized;
            if (!reachable) failed.Add(endpoint);

            Report.Record(new TestResult
            {
                TestName = $"EndpointAvailability_{endpoint}",
                TestCategory = "Availability",
                Environment = env,
                Passed = reachable,
                ValueMs = elapsedMs,
                Notes = $"HTTP {(int)status} — {url}"
            });
        }

        failed.Should().BeEmpty(
            because: $"[{env}] følgende endpoints var ikke tilgængelige: {string.Join(", ", failed)}");
    }
}
