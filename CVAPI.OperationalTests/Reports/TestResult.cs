using CVAPI.OperationalTests.Config;

namespace CVAPI.OperationalTests.Reports;

public class TestResult
{
    public string TestName { get; set; } = string.Empty;
    public string TestCategory { get; set; } = string.Empty; // "Stability", "Availability", "Operations"
    public TestEnvironment Environment { get; set; }
    public DateTime RunAt { get; set; } = DateTime.UtcNow;
    public bool Passed { get; set; }
    public double? ValueMs { get; set; }           // Primær måling i ms
    public double? ValuePercent { get; set; }      // Procentmåling
    public int? Iteration { get; set; }            // Hvilken iteration af N
    public int? ConcurrentUsers { get; set; }      // For load tests
    public string? ErrorMessage { get; set; }      // Ved fejl
    public Dictionary<string, double> Metrics { get; set; } = new(); // Fleksible extra metrics
    public string? Notes { get; set; }
}
