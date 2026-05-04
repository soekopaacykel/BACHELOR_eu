using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace CVAPI.Services
{
    /// <summary>
    /// WebDAV client for Hetzner Storage Share (Nextcloud).
    /// Files are stored privately and accessed exclusively through the
    /// application's own proxy endpoint — no public share links are created.
    /// </summary>
    public class HetznerWebDavService
    {
        private readonly HttpClient _httpClient;
        private readonly string _webDavUrl;

        private static readonly HashSet<string> AllowedCvTypes =
            new(StringComparer.OrdinalIgnoreCase) { "application/pdf" };

        private static readonly HashSet<string> AllowedImageTypes =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "image/jpeg",
                "image/png",
                "image/gif",
                "image/webp",
            };

        private const long MaxCvBytes = 10 * 1024 * 1024;    // 10 MB
        private const long MaxImageBytes = 5 * 1024 * 1024;  // 5 MB

        public HetznerWebDavService(IConfiguration configuration, HttpClient httpClient)
        {
            _httpClient = httpClient;

            _webDavUrl = configuration["Hetzner:WebDavUrl"]?.TrimEnd('/')
                ?? throw new InvalidOperationException("Hetzner:WebDavUrl is not configured.");

            var username = configuration["Hetzner:Username"]
                ?? throw new InvalidOperationException("Hetzner:Username is not configured.");

            var password = configuration["Hetzner:Password"]
                ?? throw new InvalidOperationException("Hetzner:Password is not configured.");

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"))
            );
        }

        /// <summary>
        /// Validates and uploads a CV (PDF only, max 10 MB).
        /// Returns the stored relative path, e.g. <c>cvs/cv_guid.pdf</c>.
        /// Pass this path to <see cref="GetFileAsync"/> to retrieve the file later.
        /// </summary>
        public async Task<string> UploadCvAsync(IFormFile file)
        {
            ValidateFile(file, AllowedCvTypes, MaxCvBytes);
            var fileName = $"cv_{Guid.NewGuid()}.pdf";
            return await UploadFileAsync(file, "cvs", fileName);
        }

        /// <summary>
        /// Validates and uploads a profile picture (JPEG/PNG/GIF/WEBP, max 5 MB).
        /// Returns the stored relative path, e.g. <c>profile-pictures/profile_guid.jpg</c>.
        /// Pass this path to <see cref="GetFileAsync"/> to retrieve the file later.
        /// </summary>
        public async Task<string> UploadProfilePictureAsync(IFormFile file)
        {
            ValidateFile(file, AllowedImageTypes, MaxImageBytes);
            var ext = SanitizedImageExtension(Path.GetExtension(file.FileName));
            var fileName = $"profile_{Guid.NewGuid()}{ext}";
            return await UploadFileAsync(file, "profile-pictures", fileName);
        }

        /// <summary>
        /// Downloads a file by its stored relative path (e.g. <c>profile-pictures/profile_guid.jpg</c>).
        /// Returns the raw bytes and the WebDAV-reported content-type.
        /// </summary>
        public async Task<(byte[] Data, string ContentType)> GetFileAsync(string filePath)
        {
            var url = $"{_webDavUrl}/{filePath.TrimStart('/')}";
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var data = await response.Content.ReadAsByteArrayAsync();
            var contentType = response.Content.Headers.ContentType?.MediaType
                ?? "application/octet-stream";
            return (data, contentType);
        }

        /// <summary>Deletes a file from Nextcloud storage. Silently ignores 404.</summary>
        public async Task DeleteFileAsync(string folder, string fileName)
        {
            var url = $"{_webDavUrl}/{folder}/{Uri.EscapeDataString(fileName)}";
            var response = await _httpClient.DeleteAsync(url);
            if (response.StatusCode is not (HttpStatusCode.NoContent
                or HttpStatusCode.OK
                or HttpStatusCode.NotFound))
            {
                response.EnsureSuccessStatusCode();
            }
        }

        // ── Private helpers ──────────────────────────────────────────────────────

        private async Task<string> UploadFileAsync(
            IFormFile file,
            string folder,
            string fileName)
        {
            await EnsureFolderExistsAsync(folder);

            var uploadUrl = $"{_webDavUrl}/{folder}/{Uri.EscapeDataString(fileName)}";

            await using var stream = file.OpenReadStream();
            var content = new StreamContent(stream);
            content.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);

            var putResponse = await _httpClient.PutAsync(uploadUrl, content);
            putResponse.EnsureSuccessStatusCode();

            // Return the relative path only — never a public URL
            return $"{folder}/{fileName}";
        }

        private async Task EnsureFolderExistsAsync(string folder)
        {
            var url = $"{_webDavUrl}/{folder}";
            using var request = new HttpRequestMessage(new HttpMethod("MKCOL"), url);
            var response = await _httpClient.SendAsync(request);

            // 201 = created, 405 Method Not Allowed = folder already exists — both acceptable.
            if (response.StatusCode is not (HttpStatusCode.Created
                or HttpStatusCode.MethodNotAllowed))
            {
                response.EnsureSuccessStatusCode();
            }
        }

        private static void ValidateFile(
            IFormFile file,
            HashSet<string> allowedTypes,
            long maxBytes)
        {
            if (file is null || file.Length == 0)
                throw new ArgumentException("No file provided or the file is empty.");

            if (file.Length > maxBytes)
                throw new ArgumentException(
                    $"File size exceeds the {maxBytes / (1024 * 1024)} MB limit.");

            if (!allowedTypes.Contains(file.ContentType))
                throw new ArgumentException(
                    $"File type '{file.ContentType}' is not permitted.");
        }

        private static string SanitizedImageExtension(string? ext) =>
            ext?.ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => ".jpg",
                ".png" => ".png",
                ".gif" => ".gif",
                ".webp" => ".webp",
                _ => ".jpg",
            };
    }
}
