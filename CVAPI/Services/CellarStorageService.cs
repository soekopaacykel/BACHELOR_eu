using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace CVAPI.Services
{
    /// <summary>
    /// S3-compatible object storage client for Clever Cloud Cellar.
    /// Uses AWS Signature Version 4 implemented with built-in .NET cryptography only —
    /// no third-party SDK required.
    /// </summary>
    public class CellarStorageService
    {
        private readonly HttpClient _httpClient;
        private readonly string _accessKeyId;
        private readonly string _secretKey;
        private readonly string _endpoint;

        // Cellar uses these signing constants for S3-compatible auth
        private const string SigningRegion = "us-east-1";
        private const string SigningService = "s3";

        public CellarStorageService(IConfiguration configuration, HttpClient httpClient)
        {
            _httpClient = httpClient;
            _accessKeyId = configuration["Cellar:AccessKeyId"];
            _secretKey = configuration["Cellar:SecretKey"];
            _endpoint = configuration["Cellar:Endpoint"] ?? "cellar-c2.services.clever-cloud.com";

            if (string.IsNullOrEmpty(_accessKeyId))
                throw new InvalidOperationException(
                    "Cellar:AccessKeyId is not configured. Set the Cellar__AccessKeyId environment variable in Clever Cloud.");
            if (string.IsNullOrEmpty(_secretKey))
                throw new InvalidOperationException(
                    "Cellar:SecretKey is not configured. Set the Cellar__SecretKey environment variable in Clever Cloud.");
        }

        /// <summary>Returns the JSON content of an object, or null if it does not exist.</summary>
        public async Task<string?> GetObjectAsync(string bucket, string key)
        {
            using var request = BuildRequest(HttpMethod.Get, bucket, key);
            using var response = await _httpClient.SendAsync(request);
            if (response.StatusCode == HttpStatusCode.NotFound) return null;
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        /// <summary>Stores a JSON string as an object.</summary>
        public async Task PutObjectAsync(string bucket, string key, string jsonContent)
        {
            using var request = BuildRequest(HttpMethod.Put, bucket, key, body: jsonContent);
            using var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
        }

        /// <summary>Deletes an object. Silently succeeds if the object does not exist.</summary>
        public async Task DeleteObjectAsync(string bucket, string key)
        {
            using var request = BuildRequest(HttpMethod.Delete, bucket, key);
            using var response = await _httpClient.SendAsync(request);
            if (response.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.OK
                or HttpStatusCode.NotFound) return;
            response.EnsureSuccessStatusCode();
        }

        /// <summary>Lists all object keys that start with the given prefix.</summary>
        public async Task<List<string>> ListObjectKeysAsync(string bucket, string prefix)
        {
            var queryParams = new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["list-type"] = "2",
                ["prefix"] = prefix
            };

            using var request = BuildRequest(HttpMethod.Get, bucket, "", queryParams: queryParams);
            using var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var xml = await response.Content.ReadAsStringAsync();
            var doc = XDocument.Parse(xml);
            XNamespace ns = "http://s3.amazonaws.com/doc/2006-03-01/";
            return doc.Descendants(ns + "Key").Select(e => e.Value).ToList();
        }

        // ── S3 Signature Version 4 implementation ────────────────────────────────

        private HttpRequestMessage BuildRequest(
            HttpMethod method,
            string bucket,
            string key,
            string body = "",
            SortedDictionary<string, string>? queryParams = null)
        {
            var now = DateTime.UtcNow;
            var dateStamp = now.ToString("yyyyMMdd");
            var amzDate = now.ToString("yyyyMMddTHHmmssZ");
            var host = $"{bucket}.{_endpoint}";

            var bodyBytes = Encoding.UTF8.GetBytes(body);
            var bodyHash = HexHash(bodyBytes);

            // Canonical URI: URI-encode each path segment individually, keep '/' separators
            var canonicalUri = string.IsNullOrEmpty(key)
                ? "/"
                : "/" + string.Join("/", key.Split('/').Select(Uri.EscapeDataString));

            // Canonical query string: parameters must be sorted alphabetically
            var canonicalQueryString = queryParams is { Count: > 0 }
                ? string.Join("&", queryParams.Select(
                    p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"))
                : "";

            // Canonical headers (each header name lowercased, single newline at end)
            var canonicalHeaders =
                $"host:{host}\n" +
                $"x-amz-content-sha256:{bodyHash}\n" +
                $"x-amz-date:{amzDate}\n";
            const string signedHeaders = "host;x-amz-content-sha256;x-amz-date";

            // Canonical request — CanonicalHeaders already ends with \n so we
            // concatenate directly (no extra join separator) to avoid a blank line.
            var canonicalRequest =
                method.Method + "\n" +
                canonicalUri + "\n" +
                canonicalQueryString + "\n" +
                canonicalHeaders +        // ends with \n
                signedHeaders + "\n" +
                bodyHash;

            // Credential scope and string-to-sign
            var credentialScope = $"{dateStamp}/{SigningRegion}/{SigningService}/aws4_request";
            var stringToSign = string.Join("\n",
                "AWS4-HMAC-SHA256",
                amzDate,
                credentialScope,
                HexHash(Encoding.UTF8.GetBytes(canonicalRequest)));

            // Derive signing key and compute HMAC-SHA256 signature
            var signingKey = DeriveSigningKey(_secretKey, dateStamp, SigningRegion, SigningService);
            var signature = ToHex(HmacSha256(signingKey, stringToSign));

            var authorization =
                $"AWS4-HMAC-SHA256 Credential={_accessKeyId}/{credentialScope}, " +
                $"SignedHeaders={signedHeaders}, Signature={signature}";

            // Build the full URL (use same encoded form as canonical query string)
            var urlQuery = canonicalQueryString.Length > 0 ? $"?{canonicalQueryString}" : "";
            var url = $"https://{host}{canonicalUri}{urlQuery}";

            var req = new HttpRequestMessage(method, url);
            req.Headers.TryAddWithoutValidation("x-amz-date", amzDate);
            req.Headers.TryAddWithoutValidation("x-amz-content-sha256", bodyHash);
            req.Headers.TryAddWithoutValidation("Authorization", authorization);

            if (bodyBytes.Length > 0)
            {
                req.Content = new ByteArrayContent(bodyBytes);
                req.Content.Headers.ContentType =
                    new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            }

            return req;
        }

        private static string HexHash(byte[] data) =>
            Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();

        private static byte[] HmacSha256(byte[] key, string data)
        {
            using var hmac = new HMACSHA256(key);
            return hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        }

        private static string ToHex(byte[] bytes) =>
            Convert.ToHexString(bytes).ToLowerInvariant();

        private static byte[] DeriveSigningKey(
            string secretKey, string dateStamp, string region, string service)
        {
            var kDate = HmacSha256(Encoding.UTF8.GetBytes("AWS4" + secretKey), dateStamp);
            var kRegion = HmacSha256(kDate, region);
            var kService = HmacSha256(kRegion, service);
            return HmacSha256(kService, "aws4_request");
        }
    }
}
