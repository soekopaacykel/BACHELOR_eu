using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace CVAPI.OperationalTests.Availability;

/// <summary>
/// T6 — Authentication Flow Test
/// Tester hele auth-flowet end-to-end: login → modtag JWT → kald auth-krævet endpoint.
/// Kritisk fordi JWT-secrets skifter fra Azure Key Vault til et EU-alternativ ved migration.
///
/// Kræver environment variables med testbruger-credentials:
///   TEST_USER_EMAIL=test@example.com
///   TEST_USER_PASSWORD=TestPassword123
///
/// Opret en dedikeret testbruger i begge miljøer — brug IKKE produktionsdata.
/// </summary>
[Collection("Availability")]
[Trait("Category", "Availability")]
public class AuthFlowTests
{
    private readonly HttpClient _httpClient;
    private readonly TestConfig _config;
    private readonly TestEnvironment _env;

    // Credentials læses fra environment variables — aldrig hardkodet
    private static readonly string? TestEmail =
        Environment.GetEnvironmentVariable("TEST_USER_EMAIL");
    private static readonly string? TestPassword =
        Environment.GetEnvironmentVariable("TEST_USER_PASSWORD");

    // Region der bruges til login-endpoint
    private const string TestRegion = "DK";

    public AuthFlowTests()
    {
        _config = TestConfig.Instance;
        _env = TestConfig.CurrentEnvironment;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMilliseconds(_config.TimeoutMs)
        };
    }

    [Fact]
    [Trait("Category", "Availability")]
    [Trait("TestId", "T6")]
    public async Task AuthFlow_Login_ShouldReturnJwtToken()
    {
        SkipIfCredentialsMissing();

        var loginUrl = $"{_config.GetBaseUrl(_env)}/api/user/{TestRegion}/login";

        var sw = Stopwatch.StartNew();
        var response = await _httpClient.PostAsJsonAsync(loginUrl, new
        {
            email = TestEmail,
            password = TestPassword
        });
        sw.Stop();

        var body = await response.Content.ReadAsStringAsync();
        var token = ExtractToken(body);

        var passed = response.StatusCode == HttpStatusCode.OK && !string.IsNullOrEmpty(token);

        Report.Record(new TestResult
        {
            TestName = "T6_AuthFlow_Login",
            TestCategory = "Availability",
            Environment = _env,
            Passed = passed,
            ValueMs = sw.ElapsedMilliseconds,
            Notes = $"loginUrl={loginUrl} | status={response.StatusCode} | tokenReceived={!string.IsNullOrEmpty(token)}"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            because: $"[{_env}] T6: Login returnerede {response.StatusCode} — forventede 200 OK. Body: {body[..Math.Min(200, body.Length)]}");

        token.Should().NotBeNullOrEmpty(
            because: $"[{_env}] T6: Login svarede 200 men ingen JWT token i response");
    }

    [Fact]
    [Trait("Category", "Availability")]
    [Trait("TestId", "T6")]
    public async Task AuthFlow_EndToEnd_ShouldAccessProtectedEndpoint()
    {
        SkipIfCredentialsMissing();

        var baseUrl = _config.GetBaseUrl(_env);
        var loginUrl = $"{baseUrl}/api/user/{TestRegion}/login";

        // Trin 1: Login og hent JWT
        var swTotal = Stopwatch.StartNew();

        var loginResponse = await _httpClient.PostAsJsonAsync(loginUrl, new
        {
            email = TestEmail,
            password = TestPassword
        });

        var loginBody = await loginResponse.Content.ReadAsStringAsync();
        var token = ExtractToken(loginBody);

        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            because: $"[{_env}] T6: Login fejlede — kan ikke fortsætte auth flow test");
        token.Should().NotBeNullOrEmpty(because: $"[{_env}] T6: Ingen JWT token modtaget fra login");

        // Trin 2: Kald auth-krævet endpoint med JWT
        var protectedUrl = $"{baseUrl}/api/user/{TestRegion}/consultants";

        using var authedRequest = new HttpRequestMessage(HttpMethod.Get, protectedUrl);
        authedRequest.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var authedResponse = await _httpClient.SendAsync(authedRequest);
        swTotal.Stop();

        var passed = authedResponse.StatusCode == HttpStatusCode.OK;

        Report.Record(new TestResult
        {
            TestName = "T6_AuthFlow_EndToEnd",
            TestCategory = "Availability",
            Environment = _env,
            Passed = passed,
            ValueMs = swTotal.ElapsedMilliseconds,
            Notes = $"login={loginResponse.StatusCode} | protected={authedResponse.StatusCode} | totalMs={swTotal.ElapsedMilliseconds}",
            Metrics = new Dictionary<string, double>
            {
                ["TotalFlowMs"] = swTotal.ElapsedMilliseconds
            }
        });

        authedResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            because: $"[{_env}] T6: Kald med JWT token til {protectedUrl} returnerede {authedResponse.StatusCode} — forventede 200 OK");
    }

    /// <summary>
    /// Springer testen over med en klar besked hvis credentials ikke er sat.
    /// Kørsel: TEST_USER_EMAIL=x TEST_USER_PASSWORD=y dotnet test
    /// </summary>
    private static void SkipIfCredentialsMissing()
    {
        if (string.IsNullOrEmpty(TestEmail) || string.IsNullOrEmpty(TestPassword))
        {
            throw new InvalidOperationException(
                "T6 Auth Flow kræver TEST_USER_EMAIL og TEST_USER_PASSWORD environment variables.\n" +
                "Kørsel: TEST_USER_EMAIL=x TEST_USER_PASSWORD=y dotnet test --filter TestId=T6");
        }
    }

    private static string? ExtractToken(string responseBody)
    {
        try
        {
            var doc = JsonDocument.Parse(responseBody);
            // Prøv token, jwtToken, accessToken, jwt
            foreach (var key in new[] { "token", "jwtToken", "accessToken", "jwt" })
            {
                if (doc.RootElement.TryGetProperty(key, out var prop))
                    return prop.GetString();
            }
        }
        catch { /* Ugyldig JSON */ }
        return null;
    }
}
