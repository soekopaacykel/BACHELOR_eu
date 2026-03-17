using System.Diagnostics;
using System.Net;

namespace CVAPI.OperationalTests.Availability;

/// <summary>
/// T5 — Endpoint Availability
/// Tester alle kendte API-endpoints fra konfigurationen systematisk.
/// HTTP 200 og 401 betragtes som "tilgængelig" — 401 betyder endpoint svarer men kræver auth.
/// Output: tilgængelige endpoints / totale endpoints, liste over fejlende.
/// </summary>
public class EndpointAvailabilityTests
{
    private readonly HttpClient _httpClient;
    private readonly TestConfig _config;
    private readonly TestEnvironment _env;

    public EndpointAvailabilityTests()
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
    [Trait("TestId", "T5")]
    public async Task AllEndpoints_ShouldBeAvailable()
    {
        var baseUrl = _config.GetBaseUrl(_env);
        var endpoints = _config.GetTestEndpoints(_env);

        endpoints.Should().NotBeEmpty(
            because: $"[{_env}] T5: Ingen endpoints konfigureret i appsettings.test.json");

        var results = new List<(string path, HttpStatusCode? status, long ms, bool available)>();

        foreach (var path in endpoints)
        {
            var url = baseUrl + path;
            var sw = Stopwatch.StartNew();
            HttpStatusCode? status = null;
            bool available;

            try
            {
                var response = await _httpClient.GetAsync(url);
                sw.Stop();
                status = response.StatusCode;

                // 200 OK eller 401 Unauthorized = endpoint er oppe (auth-krævet er OK)
                available = status == HttpStatusCode.OK ||
                            status == HttpStatusCode.Unauthorized;
            }
            catch
            {
                sw.Stop();
                available = false;
            }

            results.Add((path, status, sw.ElapsedMilliseconds, available));

            Report.Record(new TestResult
            {
                TestName = "T5_EndpointAvailability",
                TestCategory = "Availability",
                Environment = _env,
                Passed = available,
                ValueMs = sw.ElapsedMilliseconds,
                Notes = $"path={path} | status={status} | available={available}"
            });
        }

        var availableCount = results.Count(r => r.available);
        var total = results.Count;
        var unavailable = results.Where(r => !r.available).Select(r => r.path).ToList();

        Report.Record(new TestResult
        {
            TestName = "T5_EndpointAvailability_Summary",
            TestCategory = "Availability",
            Environment = _env,
            Passed = unavailable.Count == 0,
            ValuePercent = (double)availableCount / total * 100,
            Notes = $"{availableCount}/{total} endpoints tilgængelige" +
                    (unavailable.Count > 0 ? $" | Nede: {string.Join(", ", unavailable)}" : ""),
            Metrics = new Dictionary<string, double>
            {
                ["AvailableCount"] = availableCount,
                ["TotalCount"] = total,
                ["UnavailableCount"] = unavailable.Count
            }
        });

        unavailable.Should().BeEmpty(
            because: $"[{_env}] T5: Følgende endpoints er nede: {string.Join(", ", unavailable)}");
    }
}
