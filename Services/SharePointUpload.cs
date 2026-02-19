using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using Microsoft.Extensions.Configuration;

namespace CVAPI.Services
{
    public class SharePointUploader
    {
        private readonly ILogger<SharePointUploader> _logger;
        private readonly IConfiguration _configuration;

        public SharePointUploader(ILogger<SharePointUploader> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        public async Task<string> UploadToSharePoint(IFormFile file)
        {
            try
            {
                // Get config from IConfiguration instead of Environment
                var clientId = _configuration["SHAREPOINT_CLIENT_ID"];
                var tenantId = _configuration["SHAREPOINT_TENANT_ID"];
                var clientSecret = _configuration["SHAREPOINT_CLIENT_SECRET"];
                var siteId = _configuration["SHAREPOINT_SITE_ID"];
                var driveId = _configuration["SHAREPOINT_DRIVE_ID"];

                Console.WriteLine($"Using client ID: {clientId}");
                Console.WriteLine($"Using tenant ID: {tenantId}");
                Console.WriteLine($"Using site ID: {siteId}");

                if (string.IsNullOrEmpty(clientSecret))
                {
                    throw new Exception("SharePoint client secret is missing from configuration");
                }

                // 2. Auth with Graph API
                var authority = $"https://login.microsoftonline.com/{tenantId}";
                var app = ConfidentialClientApplicationBuilder
                    .Create(clientId)
                    .WithClientSecret(clientSecret)
                    .WithAuthority(new Uri(authority))
                    .Build();

                var scopes = new[] { "https://graph.microsoft.com/.default" };
                var authResult = await app.AcquireTokenForClient(scopes).ExecuteAsync();

                Console.WriteLine("Got auth token from Graph API");

                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                    "Bearer",
                    authResult.AccessToken
                );
                httpClient.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/json")
                );

                using var stream = file.OpenReadStream();
                using var content = new StreamContent(stream);
                content.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
                content.Headers.ContentLength = file.Length;

                // Using the correct format for Graph API
                var fileName = Uri.EscapeDataString(file.FileName);
                var uploadUrl = $"https://graph.microsoft.com/v1.0/sites/{siteId}/drives/{driveId}/items/root:/Shared Documents/CV-upload for new system (trial)/{fileName}:/content";

                Console.WriteLine($"Using URL: {uploadUrl}");
                Console.WriteLine($"File name: {fileName}");
                Console.WriteLine($"File size: {file.Length} bytes");

                var response = await httpClient.PutAsync(uploadUrl, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                Console.WriteLine($"SharePoint Response: {response.StatusCode}");
                Console.WriteLine($"Response content: {responseContent}");

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception(
                        $"SharePoint upload failed ({response.StatusCode}): {responseContent}"
                    );
                }

                var uploadResponse = JsonSerializer.Deserialize<DriveItemResponse>(responseContent);
                if (string.IsNullOrEmpty(uploadResponse?.WebUrl))
                {
                    throw new Exception("No WebUrl in SharePoint response");
                }

                Console.WriteLine($"Upload successful! URL: {uploadResponse.WebUrl}");
                return uploadResponse.WebUrl;
            }
            catch (Exception ex)
            {
                _logger.LogError($"SharePoint Upload Error: {ex.Message}");
                throw;
            }
        }

        public async Task<string> UploadProfilePicture(IFormFile file)
        {
            try
            {
                var clientId = _configuration["SHAREPOINT_CLIENT_ID"];
                var tenantId = _configuration["SHAREPOINT_TENANT_ID"];
                var clientSecret = _configuration["SHAREPOINT_CLIENT_SECRET"];
                var siteId = _configuration["SHAREPOINT_SITE_ID"];
                var driveId = _configuration["SHAREPOINT_DRIVE_ID"];

                var authority = $"https://login.microsoftonline.com/{tenantId}";
                var app = ConfidentialClientApplicationBuilder
                    .Create(clientId)
                    .WithClientSecret(clientSecret)
                    .WithAuthority(new Uri(authority))
                    .Build();

                var scopes = new[] { "https://graph.microsoft.com/.default" };
                var authResult = await app.AcquireTokenForClient(scopes).ExecuteAsync();

                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authResult.AccessToken);
                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                using var stream = file.OpenReadStream();
                using var content = new StreamContent(stream);
                content.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
                content.Headers.ContentLength = file.Length;

                var fileName = Uri.EscapeDataString(file.FileName);
                var uploadUrl = $"https://graph.microsoft.com/v1.0/sites/{siteId}/drives/{driveId}/items/root:/Shared Documents/CV DB profile pics/{fileName}:/content";

                var response = await httpClient.PutAsync(uploadUrl, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"SharePoint profile picture upload failed ({response.StatusCode}): {responseContent}");
                }

                var uploadResponse = JsonSerializer.Deserialize<DriveItemResponse>(responseContent);
                if (string.IsNullOrEmpty(uploadResponse?.WebUrl))
                {
                    throw new Exception("No WebUrl in SharePoint response for profile picture");
                }

                // Transform the SharePoint URL to ensure it's directly accessible
                var directUrl = uploadResponse.WebUrl;
                if (directUrl.Contains("?"))
                {
                    directUrl = directUrl.Split('?')[0];
                }
                directUrl += "?download=1";

                return directUrl;
            }
            catch (Exception ex)
            {
                _logger.LogError($"SharePoint Profile Picture Upload Error: {ex.Message}");
                throw;
            }
        }

        private class DriveItemResponse
        {
            [JsonPropertyName("webUrl")]
            public string WebUrl { get; set; }
        }
    }
}
