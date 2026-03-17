namespace CVAPI.OperationalTests.Availability;

/// <summary>
/// T5 — Endpoint Availability.
/// Skelner mellem:
///   - Fuldstændig utilgængelig (exception/timeout) → testen fejler
///   - Server svarer men med fejl (5xx) → registreres som fund, testen advarer men fejler ikke
///   - Server svarer OK (2xx/3xx/4xx) → tilgængelig
/// HTTP 500 på Azure-endpoints er et vigtigt fund til bacheloropgaven.
/// </summary>
[Collection("Availability")]
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

        var unreachable = new List<string>(); // Timeout / connection refused
        var serverErrors = new List<string>(); // HTTP 5xx — vigtigt fund

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
                unreachable.Add(endpoint);
                Report.Record(new TestResult
                {
                    TestName = $"EndpointAvailability_{endpoint}",
                    TestCategory = "Availability",
                    Environment = env,
                    Passed = false,
                    ErrorMessage = $"UNREACHABLE: {ex.Message}",
                    Notes = url
                });
                continue;
            }

            var isServerError = (int)status >= 500;
            if (isServerError) serverErrors.Add($"{endpoint} → HTTP {(int)status}");

            Report.Record(new TestResult
            {
                TestName = $"EndpointAvailability_{endpoint}",
                TestCategory = "Availability",
                Environment = env,
                // Server svarer = tilgængelig, selv ved 5xx (det er et app-fejl, ikke netværksfejl)
                Passed = !isServerError,
                ValueMs = elapsedMs,
                Notes = $"HTTP {(int)status}{(isServerError ? " ⚠ SERVER ERROR — vigtigt fund" : "")} — {url}"
            });
        }

        // Kun hard-fail hvis endpoints er fuldstændig utilgængelige
        unreachable.Should().BeEmpty(
            because: $"[{env}] følgende endpoints er fuldstændig utilgængelige (timeout/connection refused): {string.Join(", ", unreachable)}");

        // 5xx rapporteres som fund — ikke en hård test-fejl, men vigtig observation
        if (serverErrors.Any())
        {
            Report.Record(new TestResult
            {
                TestName = "EndpointAvailability_ServerErrors",
                TestCategory = "Availability",
                Environment = env,
                Passed = false,
                Notes = $"⚠ FUND: Følgende endpoints returnerer 5xx på {env}: {string.Join(", ", serverErrors)}"
            });
        }
    }
}
