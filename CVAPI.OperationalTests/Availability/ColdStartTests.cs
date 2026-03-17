using System.Diagnostics;
using System.Net;

namespace CVAPI.OperationalTests.Availability;

/// <summary>
/// T4 — Cold Start Time
/// MANUEL TEST — kræver at "Always On" er deaktiveret i Azure og at applikationen
/// er idlet (typisk 20 min inaktivitet). Denne test måler tiden til første HTTP 200
/// efter en kold opstart og bør køres manuelt med dokumenteret Always On-status.
///
/// Automatiseret del: poller /health indtil svar og måler tid.
/// Manuel del: sørg for applikationen er "kold" inden testen startes.
/// </summary>
[Collection("Availability")]
[Trait("Category", "Availability")]
public class ColdStartTests
{
    private readonly TestConfig _config;
    private readonly TestEnvironment _env;

    private const int MaxWaitSeconds = 120; // Maks ventetid på kold start
    private const int PollIntervalMs = 500;

    public ColdStartTests()
    {
        _config = TestConfig.Instance;
        _env = TestConfig.CurrentEnvironment;
    }

    [Fact]
    [Trait("Category", "Availability")]
    [Trait("TestId", "T4")]
    public async Task ColdStart_ShouldRespondWithin30Seconds()
    {
        var url = _config.GetBaseUrl(_env) + _config.GetHealthEndpoint(_env);

        // Brug en ny HttpClient uden connection reuse for at simulere kold forbindelse
        using var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(MaxWaitSeconds)
        };

        var sw = Stopwatch.StartNew();
        HttpStatusCode? lastStatus = null;
        var attempts = 0;

        while (sw.Elapsed.TotalSeconds < MaxWaitSeconds)
        {
            attempts++;
            try
            {
                var response = await client.GetAsync(url);
                lastStatus = response.StatusCode;

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    sw.Stop();
                    break;
                }
            }
            catch
            {
                // Applikationen er ikke klar endnu — fortsæt polling
            }

            await Task.Delay(PollIntervalMs);
        }

        sw.Stop();
        var coldStartMs = sw.ElapsedMilliseconds;
        var succeeded = lastStatus == HttpStatusCode.OK;

        Report.Record(new TestResult
        {
            TestName = "T4_ColdStart",
            TestCategory = "Availability",
            Environment = _env,
            Passed = succeeded && coldStartMs < 30_000,
            ValueMs = coldStartMs,
            Notes = $"coldStartMs={coldStartMs} | attempts={attempts} | finalStatus={lastStatus} | " +
                    $"MANUEL: Verificér at 'Always On' var OFF inden testen",
            Metrics = new Dictionary<string, double>
            {
                ["ColdStartMs"] = coldStartMs,
                ["Attempts"] = attempts
            }
        });

        succeeded.Should().BeTrue(
            because: $"[{_env}] T4: Applikationen svarede ikke HTTP 200 inden {MaxWaitSeconds}s");

        coldStartMs.Should().BeLessThan(30_000,
            because: $"[{_env}] T4: Cold start tog {coldStartMs}ms — over 30 sekunders grænsen");
    }
}
