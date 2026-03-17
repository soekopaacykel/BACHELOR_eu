using System.Diagnostics;
using System.Net.Sockets;

namespace CVAPI.OperationalTests.Stability;

/// <summary>
/// S5 — Netværkslatens Baseline
/// Måler ren TCP-latens til port 443 — uden applikationslogik.
/// Bruges til at skelne netværksforsinkelse fra applikationsforsinkelse i analysen.
/// Output: TCP gennemsnit (ms), jitter/standardafvigelse (ms).
/// </summary>
[Collection("Stability")]
[Trait("Category", "Stability")]
public class NetworkLatencyTests
{
    private readonly TestConfig _config;
    private readonly TestEnvironment _env;
    private const int TcpPort = 443;
    private const int Repetitions = 20;

    public NetworkLatencyTests()
    {
        _config = TestConfig.Instance;
        _env = TestConfig.CurrentEnvironment;
    }

    [Fact]
    [Trait("Category", "Stability")]
    [Trait("TestId", "S5")]
    public async Task NetworkLatency_TCP_ShouldMeasureBaseline()
    {
        var baseUrl = _config.GetBaseUrl(_env);
        var host = ExtractHost(baseUrl);

        host.Should().NotBeNullOrEmpty(because: $"[{_env}] Kunne ikke udtrække hostname fra {baseUrl}");

        var timings = new List<double>();
        int errors = 0;

        for (int i = 0; i < Repetitions; i++)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                using var tcpClient = new TcpClient();
                await tcpClient.ConnectAsync(host!, TcpPort);
                sw.Stop();
                timings.Add(sw.ElapsedMilliseconds);
            }
            catch
            {
                sw.Stop();
                errors++;
            }

            // Kort pause mellem TCP-forbindelser
            await Task.Delay(100);
        }

        timings.Should().NotBeEmpty(
            because: $"[{_env}] S5: Ingen succesfulde TCP-forbindelser til {host}:{TcpPort}");

        var avg = timings.Average();
        var jitter = Math.Sqrt(timings.Average(t => Math.Pow(t - avg, 2)));
        var min = timings.Min();
        var max = timings.Max();

        Report.Record(new TestResult
        {
            TestName = "S5_NetworkLatency_TCP",
            TestCategory = "Stability",
            Environment = _env,
            Passed = errors == 0,
            ValueMs = avg,
            Notes = $"host={host} | n={timings.Count} | avg={avg:F1}ms | jitter={jitter:F1}ms | min={min:F0}ms | max={max:F0}ms | errors={errors}",
            Metrics = new Dictionary<string, double>
            {
                ["Avg_ms"] = avg,
                ["Jitter_ms"] = jitter,
                ["Min_ms"] = min,
                ["Max_ms"] = max,
                ["ErrorCount"] = errors
            }
        });

        // TCP-latens over 2 sekunder er et alvorligt infrastrukturproblem
        avg.Should().BeLessThan(2000,
            because: $"[{_env}] S5: Gennemsnitlig TCP-latens {avg:F1}ms til {host} er usædvanlig høj");
    }

    private static string? ExtractHost(string baseUrl)
    {
        if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
            return uri.Host;
        return null;
    }
}
