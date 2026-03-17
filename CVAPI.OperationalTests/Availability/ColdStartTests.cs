using System.Diagnostics;
using System.Net;
using CVAPI.OperationalTests.Config;
using CVAPI.OperationalTests.Reports;
using FluentAssertions;

namespace CVAPI.OperationalTests.Availability;

/// <summary>
/// T4 — Cold Start Time.
/// Sender request og måler tid til første HTTP 200.
/// OBS: Ægte cold start kræver at applikationen er idled/genstartet manuelt inden testen køres.
/// Testen måler "first response time" som proxy for cold start.
/// </summary>
[Trait("Category", "Availability")]
public class ColdStartTests
{
    private readonly HttpClient _http = new(new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    }) { Timeout = TimeSpan.FromSeconds(60) }; // Højere timeout — cold start kan tage lang tid

    private const int Iterations = 5;

    [Theory]
    [InlineData(TestEnvironment.Azure)]
    public async Task ColdStart_FirstResponseShouldBeWithinThreshold(TestEnvironment env)
    {
        var url = TestConfig.Instance.GetBaseUrl(env) + TestConfig.Instance.GetHealthEndpoint(env);
        var measurements = new List<double>(Iterations);

        for (int i = 0; i < Iterations; i++)
        {
            // Ny HttpClient per iteration for at undgå connection reuse
            using var client = new HttpClient(new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            }) { Timeout = TimeSpan.FromSeconds(60) };

            var sw = Stopwatch.StartNew();
            HttpStatusCode status;
            try
            {
                var response = await client.GetAsync(url);
                sw.Stop();
                status = response.StatusCode;
            }
            catch (Exception ex)
            {
                sw.Stop();
                Report.Record(new TestResult
                {
                    TestName = "ColdStart",
                    TestCategory = "Availability",
                    Environment = env,
                    Passed = false,
                    Iteration = i + 1,
                    ErrorMessage = ex.Message
                });
                continue;
            }

            measurements.Add(sw.Elapsed.TotalMilliseconds);

            Report.Record(new TestResult
            {
                TestName = "ColdStart",
                TestCategory = "Availability",
                Environment = env,
                Passed = status == HttpStatusCode.OK,
                ValueMs = sw.Elapsed.TotalMilliseconds,
                Iteration = i + 1,
                Notes = $"HTTP {(int)status}"
            });

            await Task.Delay(2000); // Pause for at lade evt. connection lukke
        }

        measurements.Should().NotBeEmpty();

        var mean = measurements.Average();
        var max = measurements.Max();

        Report.Record(new TestResult
        {
            TestName = "ColdStart_Summary",
            TestCategory = "Availability",
            Environment = env,
            Passed = mean < 10000,
            ValueMs = mean,
            Metrics = new Dictionary<string, double>
            {
                ["Mean_ms"] = mean,
                ["Max_ms"] = max,
                ["Min_ms"] = measurements.Min()
            },
            Notes = $"n={measurements.Count} | Mean: {mean:F0}ms | Max: {max:F0}ms"
        });
    }
}
