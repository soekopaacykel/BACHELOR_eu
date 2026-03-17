namespace CVAPI.OperationalTests.Stability;

[Collection("Stability")]
[Trait("Category", "Stability")]
public class HealthCheckTests
{
    private readonly HttpClient _http = new(new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    }) { Timeout = TimeSpan.FromMilliseconds(TestConfig.Instance.TimeoutMs) };

    [Theory]
    [InlineData(TestEnvironment.Azure)]
    public async Task HealthCheck_ShouldReturn200(TestEnvironment env)
    {
        var url = TestConfig.Instance.GetBaseUrl(env) + TestConfig.Instance.GetHealthEndpoint(env);

        var sw = Stopwatch.StartNew();
        var response = await _http.GetAsync(url);
        sw.Stop();

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            because: $"[{env}] /health skal returnere 200 OK");

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("status").GetString()
            .Should().Be("healthy", because: "status-feltet skal være 'healthy'");

        Report.Record(env, "HealthCheck", "Stability", sw.ElapsedMilliseconds, passed: true,
            notes: $"HTTP {(int)response.StatusCode}");
    }
}
