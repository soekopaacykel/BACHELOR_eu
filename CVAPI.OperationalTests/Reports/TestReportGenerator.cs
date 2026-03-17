using System.Text;
using System.Text.Json;
using CVAPI.OperationalTests.Config;

namespace CVAPI.OperationalTests.Reports;

/// <summary>
/// Trådsikker singleton der akkumulerer testresultater og skriver JSON + CSV output.
/// Kald Report.Record(...) fra tests, og Report.Flush() til sidst.
/// </summary>
public static class Report
{
    private static readonly List<TestResult> _results = new();
    private static readonly object _lock = new();

    private static readonly string OutputDir = Path.Combine(
        AppContext.BaseDirectory, "Reports", "output");

    public static void Record(TestResult result)
    {
        lock (_lock)
        {
            _results.Add(result);
        }
    }

    /// <summary>Convenience-metode til simple ms-målinger.</summary>
    public static void Record(TestEnvironment env, string testName, string category,
        long elapsedMs, bool passed = true, string? notes = null)
    {
        Record(new TestResult
        {
            TestName = testName,
            TestCategory = category,
            Environment = env,
            ValueMs = elapsedMs,
            Passed = passed,
            Notes = notes
        });
    }

    /// <summary>
    /// Skriv alle akkumulerede resultater til JSON og CSV.
    /// Kaldes typisk via en IAsyncLifetime eller efter test-suite.
    /// </summary>
    public static void Flush()
    {
        Directory.CreateDirectory(OutputDir);

        var date = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var allResults = GetAll();

        foreach (var env in new[] { TestEnvironment.Azure, TestEnvironment.EU })
        {
            var envResults = allResults.Where(r => r.Environment == env).ToList();
            if (envResults.Count == 0) continue;

            var jsonPath = Path.Combine(OutputDir, $"results_{env.ToString().ToLower()}_{date}.json");
            var json = JsonSerializer.Serialize(envResults, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(jsonPath, json);
        }

        WriteCsv(allResults, date);
    }

    public static List<TestResult> GetAll()
    {
        lock (_lock)
        {
            return _results.ToList();
        }
    }

    private static void WriteCsv(List<TestResult> results, string date)
    {
        var csvPath = Path.Combine(OutputDir, $"comparison_{date}.csv");

        var testNames = results.Select(r => r.TestName).Distinct().OrderBy(n => n).ToList();

        var sb = new StringBuilder();
        sb.AppendLine("TestName,Category,Azure_Mean_ms,EU_Mean_ms,Difference_ms,Azure_Pass,EU_Pass");

        foreach (var name in testNames)
        {
            var azureResults = results.Where(r => r.TestName == name && r.Environment == TestEnvironment.Azure).ToList();
            var euResults = results.Where(r => r.TestName == name && r.Environment == TestEnvironment.EU).ToList();

            var category = azureResults.FirstOrDefault()?.TestCategory
                        ?? euResults.FirstOrDefault()?.TestCategory
                        ?? "";

            var azureMean = azureResults.Count > 0 && azureResults.Any(r => r.ValueMs.HasValue)
                ? azureResults.Where(r => r.ValueMs.HasValue).Average(r => r.ValueMs!.Value)
                : (double?)null;

            var euMean = euResults.Count > 0 && euResults.Any(r => r.ValueMs.HasValue)
                ? euResults.Where(r => r.ValueMs.HasValue).Average(r => r.ValueMs!.Value)
                : (double?)null;

            var diff = azureMean.HasValue && euMean.HasValue
                ? (euMean.Value - azureMean.Value).ToString("F1")
                : "";

            var azurePass = azureResults.Count > 0 ? azureResults.All(r => r.Passed).ToString().ToLower() : "";
            var euPass = euResults.Count > 0 ? euResults.All(r => r.Passed).ToString().ToLower() : "";

            sb.AppendLine($"{name},{category}," +
                          $"{azureMean?.ToString("F1") ?? ""}," +
                          $"{euMean?.ToString("F1") ?? ""}," +
                          $"{diff},{azurePass},{euPass}");
        }

        File.WriteAllText(csvPath, sb.ToString());
    }
}
