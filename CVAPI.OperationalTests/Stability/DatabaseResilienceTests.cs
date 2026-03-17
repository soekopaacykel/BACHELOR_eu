using System.Diagnostics;
using System.Net;

namespace CVAPI.OperationalTests.Stability;

/// <summary>
/// S4 — Database Resilience Test
/// Kalder et readonly DB-endpoint gentagne gange og observerer om svartider forværres
/// over tid (tegn på connection leak eller resource udtømning).
/// Output: fejlrate, gennemsnitlig DB-responstid, drift over tid.
/// </summary>
[Collection("Stability")]
[Trait("Category", "Stability")]
public class DatabaseResilienceTests
{
    private readonly HttpClient _httpClient;
    private readonly TestConfig _config;
    private readonly TestEnvironment _env;

    // Readonly endpoint der rammer Cosmos DB uden at skrive data
    private const string DbEndpointPath = "/api/competencies/eu/competencies";

    public DatabaseResilienceTests()
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
    [Trait("TestId", "S4")]
    public async Task DatabaseResilience_ShouldNotDegradeOverTime()
    {
        var url = _config.GetBaseUrl(_env) + DbEndpointPath;
        var repeatCount = _config.RepeatCount;

        var timings = new List<(int iteration, double ms, bool success)>();

        for (int i = 0; i < repeatCount; i++)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                var response = await _httpClient.GetAsync(url);
                sw.Stop();

                var success = response.StatusCode == HttpStatusCode.OK ||
                              response.StatusCode == HttpStatusCode.Unauthorized;
                timings.Add((i, sw.ElapsedMilliseconds, success));

                Report.Record(new TestResult
                {
                    TestName = "S4_DatabaseResilience",
                    TestCategory = "Stability",
                    Environment = _env,
                    Passed = success,
                    Iteration = i,
                    ValueMs = sw.ElapsedMilliseconds,
                    ErrorMessage = success ? null : $"HTTP {(int)response.StatusCode}"
                });
            }
            catch (Exception ex)
            {
                sw.Stop();
                timings.Add((i, sw.ElapsedMilliseconds, false));

                Report.Record(new TestResult
                {
                    TestName = "S4_DatabaseResilience",
                    TestCategory = "Stability",
                    Environment = _env,
                    Passed = false,
                    Iteration = i,
                    ValueMs = sw.ElapsedMilliseconds,
                    Notes = $"Exception ved iteration {i}: {ex.Message}"
                });
            }

            // Lille pause for ikke at hammere databasen
            await Task.Delay(200);
        }

        var successful = timings.Where(t => t.success).ToList();
        var errors = timings.Count(t => !t.success);
        var errorRate = (double)errors / timings.Count * 100;

        if (successful.Count == 0)
        {
            Report.Record(new TestResult
            {
                TestName = "S4_DatabaseResilience",
                TestCategory = "Stability",
                Environment = _env,
                Passed = false,
                Notes = $"Ingen succesfulde requests ud af {timings.Count}"
            });
            false.Should().BeTrue(because: $"[{_env}] S4: Ingen succesfulde DB-requests");
            return;
        }

        // Drift-analyse: sammenlign første og sidste 10% af requests
        var firstQuartile = successful.Take(successful.Count / 4).Select(t => t.ms).Average();
        var lastQuartile = successful.TakeLast(successful.Count / 4).Select(t => t.ms).Average();
        var drift = lastQuartile - firstQuartile;
        var driftPct = firstQuartile > 0 ? drift / firstQuartile * 100 : 0;

        var avgMs = successful.Select(t => t.ms).Average();

        Report.Record(new TestResult
        {
            TestName = "S4_DatabaseResilience",
            TestCategory = "Stability",
            Environment = _env,
            Passed = errorRate <= 5.0 && driftPct < 50.0,
            ValueMs = avgMs,
            ValuePercent = errorRate,
            Notes = $"n={timings.Count} | errors={errors} ({errorRate:F1}%) | avg={avgMs:F0}ms | drift={drift:F0}ms ({driftPct:F1}%)",
            Metrics = new Dictionary<string, double>
            {
                ["AvgMs"] = avgMs,
                ["ErrorRate_pct"] = errorRate,
                ["FirstQuartile_ms"] = firstQuartile,
                ["LastQuartile_ms"] = lastQuartile,
                ["Drift_ms"] = drift,
                ["Drift_pct"] = driftPct
            }
        });

        errorRate.Should().BeLessThanOrEqualTo(5.0,
            because: $"[{_env}] S4: DB fejlrate {errorRate:F1}% overstiger 5%-grænsen");

        driftPct.Should().BeLessThan(50.0,
            because: $"[{_env}] S4: Svartider forværres med {driftPct:F1}% over tid — mulig connection/memory drift");
    }
}
