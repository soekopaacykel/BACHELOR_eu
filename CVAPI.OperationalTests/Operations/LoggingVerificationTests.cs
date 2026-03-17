namespace CVAPI.OperationalTests.Operations;

/// <summary>
/// D3 — Log Tilgængelighed.
/// Trigger et request og verificér at API svarer (proxy for at log-infrastrukturen modtager events).
/// Fuld log-latens måling kræver adgang til log-platformen (Azure Monitor / Seq / Grafana)
/// og gøres manuelt efter kørsel.
/// </summary>
[Trait("Category", "Operations")]
public class LoggingVerificationTests
{
    private readonly HttpClient _http = new(new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    }) { Timeout = TimeSpan.FromMilliseconds(TestConfig.Instance.TimeoutMs) };

    [Theory]
    [InlineData(TestEnvironment.Azure)]
    public async Task Logging_TriggeredRequestShouldSucceed(TestEnvironment env)
    {
        var url = TestConfig.Instance.GetBaseUrl(env) + TestConfig.Instance.GetHealthEndpoint(env);

        // Send 3 requests der burde logges af applikationen
        for (int i = 0; i < 3; i++)
        {
            var sw = Stopwatch.StartNew();
            var response = await _http.GetAsync(url);
            sw.Stop();

            var passed = response.StatusCode == HttpStatusCode.OK;

            Report.Record(new TestResult
            {
                TestName = "LoggingVerification",
                TestCategory = "Operations",
                Environment = env,
                Passed = passed,
                ValueMs = sw.ElapsedMilliseconds,
                Iteration = i + 1,
                Notes = $"HTTP {(int)response.StatusCode} — verificer manuelt i log-platformen at event er registreret"
            });

            response.StatusCode.Should().Be(HttpStatusCode.OK,
                because: $"[{env}] request der triggede logging skal returnere 200");
        }

        // Manuel note til rapporten
        Report.Record(new TestResult
        {
            TestName = "LoggingVerification_Manual",
            TestCategory = "Operations",
            Environment = env,
            Passed = true,
            Notes = "MANUEL CHECK KRÆVET: Verificer i log-platformen (Azure Monitor / Seq) at ovenstående requests er logget. Mål latens fra request-tid til log er synlig."
        });
    }
}
