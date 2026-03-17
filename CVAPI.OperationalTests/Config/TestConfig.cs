using Microsoft.Extensions.Configuration;

namespace CVAPI.OperationalTests.Config;

public class TestConfig
{
    private readonly IConfiguration _config;

    private static readonly Lazy<TestConfig> _instance =
        new(() => new TestConfig());

    public static TestConfig Instance => _instance.Value;

    /// <summary>
    /// Aktuelt miljø styret via environment variable TEST_ENV (default: Azure).
    /// Kørsel: TEST_ENV=Azure dotnet test  /  TEST_ENV=EU dotnet test
    /// </summary>
    public static TestEnvironment CurrentEnvironment =>
        Enum.TryParse<TestEnvironment>(
            Environment.GetEnvironmentVariable("TEST_ENV"), ignoreCase: true, out var env)
            ? env
            : TestEnvironment.Azure;

    private TestConfig()
    {
        var basePath = Path.GetDirectoryName(typeof(TestConfig).Assembly.Location)!;

        _config = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("Config/appsettings.test.json", optional: false, reloadOnChange: false)
            .Build();
    }

    public string GetBaseUrl(TestEnvironment env) =>
        _config[$"Environments:{env}:BaseUrl"]
        ?? throw new InvalidOperationException($"BaseUrl ikke konfigureret for {env}");

    public string GetHealthEndpoint(TestEnvironment env) =>
        _config[$"Environments:{env}:HealthEndpoint"] ?? "/health";

    public string[] GetTestEndpoints(TestEnvironment env)
    {
        var section = _config.GetSection($"Environments:{env}:TestEndpoints");
        return section.Get<string[]>() ?? [];
    }

    public int RepeatCount =>
        int.TryParse(_config["TestSettings:RepeatCount"], out var v) ? v : 50;

    public int[] ConcurrentUsers =>
        _config.GetSection("TestSettings:ConcurrentUsers").Get<int[]>() ?? [10, 50, 100];

    public int UptimeDurationMinutes =>
        int.TryParse(_config["TestSettings:UptimeDurationMinutes"], out var v) ? v : 1440;

    public int UptimeIntervalSeconds =>
        int.TryParse(_config["TestSettings:UptimeIntervalSeconds"], out var v) ? v : 30;

    public int TimeoutMs =>
        int.TryParse(_config["TestSettings:TimeoutMs"], out var v) ? v : 10000;
}
