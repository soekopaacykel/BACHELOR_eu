using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using CVAPI.OperationalTests.Config;
using CVAPI.OperationalTests.Reports;
using FluentAssertions;

namespace CVAPI.OperationalTests.Availability;

/// <summary>
/// T3 — SSL/TLS Certifikat Validering.
/// Verificerer at certifikatet er gyldigt, ikke udløbet og har mere end 30 dage tilbage.
/// </summary>
[Trait("Category", "Availability")]
public class SSLCertificateTests
{
    [Theory]
    [InlineData(TestEnvironment.Azure)]
    public async Task SSL_CertificateShouldBeValidAndNotExpiringSoon(TestEnvironment env)
    {
        var baseUrl = TestConfig.Instance.GetBaseUrl(env);
        var uri = new Uri(baseUrl);

        uri.Scheme.Should().Be("https", because: "applikationen skal køre over HTTPS");

        X509Certificate2? cert = null;

        using var tcpClient = new TcpClient();
        await tcpClient.ConnectAsync(uri.Host, 443);

        using var sslStream = new SslStream(tcpClient.GetStream(), false,
            (_, certificate, _, _) =>
            {
                if (certificate is X509Certificate2 c2) cert = c2;
                else if (certificate != null) cert = new X509Certificate2(certificate);
                return true;
            });

        await sslStream.AuthenticateAsClientAsync(uri.Host);

        cert.Should().NotBeNull(because: "der skal være et SSL-certifikat tilgængeligt");

        var daysUntilExpiry = (cert!.NotAfter - DateTime.UtcNow).TotalDays;
        var isValid = DateTime.UtcNow >= cert.NotBefore && DateTime.UtcNow <= cert.NotAfter;

        Report.Record(new TestResult
        {
            TestName = "SSLCertificate",
            TestCategory = "Availability",
            Environment = env,
            Passed = isValid && daysUntilExpiry > 30,
            Metrics = new Dictionary<string, double>
            {
                ["DaysUntilExpiry"] = daysUntilExpiry
            },
            Notes = $"Udsteder: {cert.Issuer} | Udløber: {cert.NotAfter:yyyy-MM-dd} ({daysUntilExpiry:F0} dage) | Gyldig: {isValid}"
        });

        isValid.Should().BeTrue(because: $"[{env}] SSL-certifikatet skal være gyldigt nu");
        daysUntilExpiry.Should().BeGreaterThan(30,
            because: $"[{env}] certifikatet må ikke udløbe inden for 30 dage (udløber: {cert.NotAfter:yyyy-MM-dd})");
    }
}
