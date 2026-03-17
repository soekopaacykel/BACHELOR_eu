namespace CVAPI.OperationalTests.Operations;

/// <summary>
/// D2 — Konfigurationsvalidering.
/// Verificerer at alle nødvendige konfigurationer er tilgængelige:
/// - JWT-beskyttet endpoint svarer 401 (ikke 500 = config-fejl)
/// - DB-endpoint svarer 200 eller 401 (ikke 500)
/// </summary>
[Collection("Operations")]
[Trait("Category", "Operations")]
public class ConfigValidationTests
{
    private readonly HttpClient _http = new(new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    }) { Timeout = TimeSpan.FromMilliseconds(TestConfig.Instance.TimeoutMs) };

    [Theory]
    [InlineData(TestEnvironment.Azure)]
    public async Task Config_JwtEndpointShouldRespondWithout500(TestEnvironment env)
    {
        var url = TestConfig.Instance.GetBaseUrl(env) + "/api/user/DK/consultants";

        var sw = Stopwatch.StartNew();
        var response = await _http.GetAsync(url);
        sw.Stop();

        // 401 = JWT krævet, men konfigurationen virker. 500 = config-fejl.
        var passed = response.StatusCode != HttpStatusCode.InternalServerError;

        Report.Record(new TestResult
        {
            TestName = "ConfigValidation_JWT",
            TestCategory = "Operations",
            Environment = env,
            Passed = passed,
            ValueMs = sw.ElapsedMilliseconds,
            Notes = $"HTTP {(int)response.StatusCode} — forventet 200 eller 401, ikke 500"
        });

        response.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError,
            because: $"[{env}] JWT-konfiguration mangler hvis endpoint returnerer 500");
    }

    [Theory]
    [InlineData(TestEnvironment.Azure)]
    public async Task Config_DatabaseEndpointShouldRespondWithout500(TestEnvironment env)
    {
        var url = TestConfig.Instance.GetBaseUrl(env) + "/api/competencies/DK/predefined";

        var sw = Stopwatch.StartNew();
        var response = await _http.GetAsync(url);
        sw.Stop();

        var passed = response.StatusCode != HttpStatusCode.InternalServerError;

        Report.Record(new TestResult
        {
            TestName = "ConfigValidation_Database",
            TestCategory = "Operations",
            Environment = env,
            Passed = passed,
            ValueMs = sw.ElapsedMilliseconds,
            Notes = $"HTTP {(int)response.StatusCode} — DB-forbindelsen virker hvis ikke 500"
        });

        response.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError,
            because: $"[{env}] Database-konfiguration mangler hvis endpoint returnerer 500");
    }
}
