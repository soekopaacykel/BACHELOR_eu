using System.Diagnostics;
using System.Net;
using System.Text.Json;

namespace CVAPI.OperationalTests.Stability;

/// <summary>
/// S1 — Health Check Test
/// Verificerer at /health svarer HTTP 200 med korrekt JSON-struktur og måler responstid.
/// </summary>
public class HealthCheckTests
{
    private readonly HttpClient _httpClient;
    private readonly TestConfig _config;
    private readonly TestEnvironment _env;

    public HealthCheckTests()
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
    [Trait("TestId", "S1")]
    public async Task HealthCheck_ShouldReturn200()
    {
        var url = _config.GetBaseUrl(_env) + _config.GetHealthEndpoint(_env);

        var sw = Stopwatch.StartNew();
        var response = await _httpClient.GetAsync(url);
        sw.Stop();

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            because: $"[{_env}] /health skal returnere 200 OK");

        Report.Record(new TestResult
        {
            TestName = "S1_HealthCheck",
            TestCategory = "Stability",
            Environment = _env,
            Passed = response.StatusCode == HttpStatusCode.OK,
            ValueMs = sw.ElapsedMilliseconds,
            Notes = $"HTTP {(int)response.StatusCode}"
        });
    }

    [Fact]
    [Trait("Category", "Stability")]
    [Trait("TestId", "S1")]
    public async Task HealthCheck_ShouldReturnValidJson()
    {
        var url = _config.GetBaseUrl(_env) + _config.GetHealthEndpoint(_env);

        var response = await _httpClient.GetAsync(url);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        JsonDocument doc;
        var parseSucceeded = true;
        try
        {
            doc = JsonDocument.Parse(body);
        }
        catch
        {
            parseSucceeded = false;
            doc = JsonDocument.Parse("{}");
        }

        parseSucceeded.Should().BeTrue(because: $"[{_env}] /health skal returnere gyldig JSON, fik: {body}");

        var root = doc.RootElement;
        root.TryGetProperty("status", out var statusProp).Should().BeTrue(
            because: "JSON skal indeholde 'status' felt");
        statusProp.GetString().Should().Be("healthy");

        root.TryGetProperty("timestamp", out _).Should().BeTrue(
            because: "JSON skal indeholde 'timestamp' felt");

        root.TryGetProperty("version", out _).Should().BeTrue(
            because: "JSON skal indeholde 'version' felt");

        Report.Record(new TestResult
        {
            TestName = "S1_HealthCheck_JsonStructure",
            TestCategory = "Stability",
            Environment = _env,
            Passed = parseSucceeded,
            Notes = parseSucceeded ? "JSON struktur OK" : $"Ugyldig JSON: {body}"
        });
    }

    [Fact]
    [Trait("Category", "Stability")]
    [Trait("TestId", "S1")]
    public async Task HealthCheck_ShouldRespondWithin2000ms()
    {
        var url = _config.GetBaseUrl(_env) + _config.GetHealthEndpoint(_env);
        const int thresholdMs = 2000;

        var sw = Stopwatch.StartNew();
        var response = await _httpClient.GetAsync(url);
        sw.Stop();

        sw.ElapsedMilliseconds.Should().BeLessThan(thresholdMs,
            because: $"[{_env}] /health bør svare inden {thresholdMs}ms, tog {sw.ElapsedMilliseconds}ms");

        Report.Record(new TestResult
        {
            TestName = "S1_HealthCheck_ResponseTime",
            TestCategory = "Stability",
            Environment = _env,
            Passed = sw.ElapsedMilliseconds < thresholdMs,
            ValueMs = sw.ElapsedMilliseconds,
            Notes = $"Threshold: {thresholdMs}ms"
        });
    }
}
