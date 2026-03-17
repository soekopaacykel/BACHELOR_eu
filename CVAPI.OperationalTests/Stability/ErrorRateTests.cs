namespace CVAPI.OperationalTests.Stability;

[Collection("Stability")]
[Trait("Category", "Stability")]
public class ErrorRateTests
{
    private readonly HttpClient _http = new(new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    }) { Timeout = TimeSpan.FromMilliseconds(TestConfig.Instance.TimeoutMs) };

    [Theory]
    [InlineData(TestEnvironment.Azure)]
    public async Task ErrorRate_UnderConcurrentLoad(TestEnvironment env)
    {
        var url = TestConfig.Instance.GetBaseUrl(env) + TestConfig.Instance.GetHealthEndpoint(env);
        var concurrentLevels = TestConfig.Instance.ConcurrentUsers;

        foreach (var concurrency in concurrentLevels)
        {
            var tasks = Enumerable.Range(0, concurrency).Select(async _ =>
            {
                try
                {
                    var sw = Stopwatch.StartNew();
                    var response = await _http.GetAsync(url);
                    sw.Stop();
                    return (Success: (int)response.StatusCode < 500, Ms: sw.Elapsed.TotalMilliseconds);
                }
                catch
                {
                    return (Success: false, Ms: (double)TestConfig.Instance.TimeoutMs);
                }
            });

            var results = await Task.WhenAll(tasks);
            var errors = results.Count(r => !r.Success);
            var errorRate = (double)errors / concurrency * 100;

            Report.Record(new TestResult
            {
                TestName = "ErrorRate",
                TestCategory = "Stability",
                Environment = env,
                Passed = errorRate < 10,
                ValuePercent = errorRate,
                ConcurrentUsers = concurrency,
                Notes = $"{errors}/{concurrency} fejl ({errorRate:F1}%) ved {concurrency} samtidige brugere"
            });
        }

        // Samlet assertion: ingen af niveauerne må have over 10% fejl
        var allRecorded = Report.GetAll()
            .Where(r => r.TestName == "ErrorRate" && r.Environment == env);
        allRecorded.Should().OnlyContain(r => r.Passed,
            because: "fejlprocenten må ikke overstige 10% på noget belastningsniveau");
    }
}
