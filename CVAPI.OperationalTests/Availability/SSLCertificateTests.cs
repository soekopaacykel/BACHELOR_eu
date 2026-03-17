using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;

namespace CVAPI.OperationalTests.Availability;

/// <summary>
/// T3 — SSL/TLS Certifikat Validering
/// Verificerer certifikatets gyldighed, kæde og at det ikke udløber inden for 30 dage.
/// Output: gyldig (ja/nej), dage til udløb, certifikatudsteder.
/// </summary>
[Collection("Availability")]
[Trait("Category", "Availability")]
public class SSLCertificateTests
{
    private readonly TestConfig _config;
    private readonly TestEnvironment _env;

    public SSLCertificateTests()
    {
        _config = TestConfig.Instance;
        _env = TestConfig.CurrentEnvironment;
    }

    [Fact]
    [Trait("Category", "Availability")]
    [Trait("TestId", "T3")]
    public async Task SSL_Certificate_ShouldBeValid()
    {
        var baseUrl = _config.GetBaseUrl(_env);
        var host = ExtractHost(baseUrl);
        host.Should().NotBeNullOrEmpty(because: $"[{_env}] Kunne ikke udtrække hostname fra {baseUrl}");

        X509Certificate2? cert = null;
        var chainValid = false;
        string? errorMessage = null;

        try
        {
            using var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(host!, 443);

            using var sslStream = new SslStream(tcpClient.GetStream(),
                leaveInnerStreamOpen: false,
                userCertificateValidationCallback: (_, certificate, chain, errors) =>
                {
                    chainValid = errors == SslPolicyErrors.None;
                    if (certificate != null)
                        cert = new X509Certificate2(certificate);
                    return true; // Hent info selv om der er fejl
                });

            await sslStream.AuthenticateAsClientAsync(host!);
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
        }

        var passed = cert != null && chainValid && errorMessage == null;
        var daysUntilExpiry = cert != null
            ? (cert.NotAfter - DateTime.UtcNow).TotalDays
            : -1;
        var issuer = cert?.Issuer ?? "ukendt";
        var subject = cert?.Subject ?? "ukendt";

        Report.Record(new TestResult
        {
            TestName = "T3_SSL_Certificate",
            TestCategory = "Availability",
            Environment = _env,
            Passed = passed && daysUntilExpiry > 30,
            Notes = $"host={host} | valid={passed} | daysLeft={daysUntilExpiry:F0} | issuer={issuer} | subject={subject}" +
                    (errorMessage != null ? $" | error={errorMessage}" : ""),
            Metrics = new Dictionary<string, double>
            {
                ["DaysUntilExpiry"] = daysUntilExpiry,
                ["ChainValid"] = chainValid ? 1 : 0
            }
        });

        cert.Should().NotBeNull(because: $"[{_env}] T3: Kunne ikke hente SSL-certifikat fra {host}");
        chainValid.Should().BeTrue(because: $"[{_env}] T3: Certifikatkæde er ugyldig for {host}");
        daysUntilExpiry.Should().BeGreaterThan(30,
            because: $"[{_env}] T3: Certifikatet udløber om {daysUntilExpiry:F0} dage (grænse: 30 dage)");
    }

    [Fact]
    [Trait("Category", "Availability")]
    [Trait("TestId", "T3")]
    public async Task SSL_Certificate_ShouldNotBeExpired()
    {
        var baseUrl = _config.GetBaseUrl(_env);
        var host = ExtractHost(baseUrl);
        host.Should().NotBeNullOrEmpty();

        DateTime? notAfter = null;
        DateTime? notBefore = null;

        try
        {
            using var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(host!, 443);

            using var sslStream = new SslStream(tcpClient.GetStream(),
                leaveInnerStreamOpen: false,
                userCertificateValidationCallback: (_, certificate, _, _) =>
                {
                    if (certificate != null)
                    {
                        var x509 = new X509Certificate2(certificate);
                        notAfter = x509.NotAfter;
                        notBefore = x509.NotBefore;
                    }
                    return true;
                });

            await sslStream.AuthenticateAsClientAsync(host!);
        }
        catch { /* Håndteres af assertions nedenfor */ }

        notAfter.Should().NotBeNull(because: $"[{_env}] T3: Kunne ikke hente certifikatets udløbsdato");
        notAfter!.Value.Should().BeAfter(DateTime.UtcNow,
            because: $"[{_env}] T3: Certifikatet er udløbet (udløb: {notAfter:yyyy-MM-dd})");
        notBefore!.Value.Should().BeBefore(DateTime.UtcNow,
            because: $"[{_env}] T3: Certifikatet er endnu ikke gyldigt (gyldig fra: {notBefore:yyyy-MM-dd})");
    }

    private static string? ExtractHost(string baseUrl)
    {
        if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
            return uri.Host;
        return null;
    }
}
