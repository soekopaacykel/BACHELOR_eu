using System.Net;
using System.Text.Json;

namespace CVAPI.OperationalTests.Operations;

/// <summary>
/// D4 — Data Residency Verificering
/// Verificerer at serverens fysiske lokation er inden for EU.
/// Dette er en af de vigtigste tests for bacheloropgavens konklusion.
///
/// Fremgangsmåde:
/// 1. DNS-opslag → IP-adresse
/// 2. GeoIP-lookup via ip-api.com → land, by, organisation
/// 3. Verificér at country er et EU-land
///
/// Output: server lokation (land/by), IP-adresse, EU-kompatibel (ja/nej).
/// </summary>
[Collection("Operations")]
public class DataResidencyTests
{
    private readonly HttpClient _httpClient;
    private readonly TestConfig _config;
    private readonly TestEnvironment _env;

    // EU-lande (ISO 3166-1 alpha-2)
    private static readonly HashSet<string> EuCountryCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "AT", "BE", "BG", "HR", "CY", "CZ", "DK", "EE", "FI", "FR",
        "DE", "GR", "HU", "IE", "IT", "LV", "LT", "LU", "MT", "NL",
        "PL", "PT", "RO", "SK", "SI", "ES", "SE"
    };

    public DataResidencyTests()
    {
        _config = TestConfig.Instance;
        _env = TestConfig.CurrentEnvironment;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
    }

    [Fact]
    [Trait("Category", "Operations")]
    [Trait("TestId", "D4")]
    public async Task DataResidency_ServerShouldBeInEU()
    {
        var baseUrl = _config.GetBaseUrl(_env);
        var host = ExtractHost(baseUrl);

        host.Should().NotBeNullOrEmpty(
            because: $"[{_env}] D4: Kunne ikke udtrække hostname fra {baseUrl}");

        // Trin 1: DNS-opslag → IP
        var ipAddress = await ResolveIpAsync(host!);
        ipAddress.Should().NotBeNullOrEmpty(
            because: $"[{_env}] D4: DNS-opslag fejlede for {host}");

        // Trin 2: GeoIP-lookup
        var geoInfo = await GeoIpLookupAsync(ipAddress!);

        var isEu = geoInfo.CountryCode != null &&
                   EuCountryCodes.Contains(geoInfo.CountryCode);

        Report.Record(new TestResult
        {
            TestName = "D4_DataResidency",
            TestCategory = "Operations",
            Environment = _env,
            Passed = isEu,
            Notes = $"host={host} | ip={ipAddress} | " +
                    $"country={geoInfo.Country} ({geoInfo.CountryCode}) | " +
                    $"city={geoInfo.City} | org={geoInfo.Org} | " +
                    $"region={geoInfo.Region} | eu={isEu} | " +
                    $"KONKLUSION: Data befinder sig i {(isEu ? "EU ✅" : "IKKE-EU ❌")}",
            Metrics = new Dictionary<string, double>
            {
                ["IsEU"] = isEu ? 1 : 0
            }
        });

        isEu.Should().BeTrue(
            because: $"[{_env}] D4: Server er IKKE i EU — " +
                     $"land: {geoInfo.Country} ({geoInfo.CountryCode}), " +
                     $"by: {geoInfo.City}, org: {geoInfo.Org}. " +
                     $"IP: {ipAddress}");
    }

    [Fact]
    [Trait("Category", "Operations")]
    [Trait("TestId", "D4")]
    public async Task DataResidency_RecordServerLocation()
    {
        var baseUrl = _config.GetBaseUrl(_env);
        var host = ExtractHost(baseUrl);
        if (host == null) return;

        var ip = await ResolveIpAsync(host);
        if (ip == null) return;

        var geo = await GeoIpLookupAsync(ip);
        var isEu = geo.CountryCode != null && EuCountryCodes.Contains(geo.CountryCode);

        // Denne test fejler aldrig — den logger altid lokationen til rapporten
        // uanset om serveren er i EU eller ej. God til manuel verifikation.
        Report.Record(new TestResult
        {
            TestName = "D4_DataResidency_LocationRecord",
            TestCategory = "Operations",
            Environment = _env,
            Passed = true, // Altid passed — formålet er dokumentation
            Notes = $"=== LOKATION FOR {_env} ===" +
                    $" | Host: {host}" +
                    $" | IP: {ip}" +
                    $" | Land: {geo.Country} ({geo.CountryCode})" +
                    $" | By: {geo.City}" +
                    $" | Region: {geo.Region}" +
                    $" | Organisation: {geo.Org}" +
                    $" | EU-kompatibel: {(isEu ? "JA ✅" : "NEJ ❌")}"
        });

        // Denne test består altid — resultatet bruges i rapporten
        true.Should().BeTrue();
    }

    private static async Task<string?> ResolveIpAsync(string host)
    {
        try
        {
            var addresses = await System.Net.Dns.GetHostAddressesAsync(host);
            return addresses.FirstOrDefault()?.ToString();
        }
        catch { return null; }
    }

    private async Task<GeoIpResult> GeoIpLookupAsync(string ip)
    {
        try
        {
            // ip-api.com er gratis uden API-nøgle for ikke-kommerciel brug
            var response = await _httpClient.GetAsync(
                $"http://ip-api.com/json/{ip}?fields=status,country,countryCode,region,city,org");

            if (response.StatusCode != HttpStatusCode.OK)
                return new GeoIpResult();

            var body = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            return new GeoIpResult
            {
                Country = root.TryGetProperty("country", out var c) ? c.GetString() : null,
                CountryCode = root.TryGetProperty("countryCode", out var cc) ? cc.GetString() : null,
                Region = root.TryGetProperty("region", out var r) ? r.GetString() : null,
                City = root.TryGetProperty("city", out var ci) ? ci.GetString() : null,
                Org = root.TryGetProperty("org", out var o) ? o.GetString() : null
            };
        }
        catch { return new GeoIpResult(); }
    }

    private static string? ExtractHost(string baseUrl)
    {
        if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
            return uri.Host;
        return null;
    }

    private class GeoIpResult
    {
        public string? Country { get; set; }
        public string? CountryCode { get; set; }
        public string? Region { get; set; }
        public string? City { get; set; }
        public string? Org { get; set; }
    }
}
