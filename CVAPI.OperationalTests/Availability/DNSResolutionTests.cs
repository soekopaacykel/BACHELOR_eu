using System.Diagnostics;
using System.Net;
using CVAPI.OperationalTests.Config;
using CVAPI.OperationalTests.Reports;
using FluentAssertions;

namespace CVAPI.OperationalTests.Availability;

/// <summary>
/// T2 — DNS Resolution Time.
/// Måler DNS-opslagstid 20 gange og beregner gennemsnit.
/// </summary>
[Trait("Category", "Availability")]
public class DNSResolutionTests
{
    private const int Iterations = 20;

    [Theory]
    [InlineData(TestEnvironment.Azure)]
    public async Task DNS_ResolutionShouldBeFast(TestEnvironment env)
    {
        var baseUrl = TestConfig.Instance.GetBaseUrl(env);
        var host = new Uri(baseUrl).Host;

        var measurements = new List<double>(Iterations);

        for (int i = 0; i < Iterations; i++)
        {
            var sw = Stopwatch.StartNew();
            var addresses = await Dns.GetHostAddressesAsync(host);
            sw.Stop();

            addresses.Should().NotBeEmpty(because: $"DNS-opslag for {host} skal returnere mindst én IP");

            measurements.Add(sw.Elapsed.TotalMilliseconds);

            Report.Record(new TestResult
            {
                TestName = "DNSResolution",
                TestCategory = "Availability",
                Environment = env,
                Passed = true,
                ValueMs = sw.Elapsed.TotalMilliseconds,
                Iteration = i + 1,
                Notes = $"{host} → {string.Join(", ", addresses.Select(a => a.ToString()))}"
            });

            await Task.Delay(100); // kort pause mellem opslag
        }

        var mean = measurements.Average();
        var max = measurements.Max();

        Report.Record(new TestResult
        {
            TestName = "DNSResolution_Summary",
            TestCategory = "Availability",
            Environment = env,
            Passed = mean < 500,
            ValueMs = mean,
            Metrics = new Dictionary<string, double>
            {
                ["Mean_ms"] = mean,
                ["Max_ms"] = max,
                ["Min_ms"] = measurements.Min()
            },
            Notes = $"n={Iterations} | Mean: {mean:F1}ms | Max: {max:F1}ms"
        });

        mean.Should().BeLessThan(500,
            because: $"[{env}] gennemsnitlig DNS-opslagstid skal være under 500ms");
    }
}
