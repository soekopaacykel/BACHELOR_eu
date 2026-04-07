using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using CVAPI.Models;
using CVAPI.Services;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using User = CVAPI.Models.User;

namespace CVAPI.Repos
{
    public class UserRepository
    {
        private readonly CellarStorageService _storage;
        private readonly IConfiguration _configuration;
        private readonly ILogger<UserRepository> _logger;

        public UserRepository(
            CellarStorageService storage,
            IConfiguration configuration,
            ILogger<UserRepository> logger
        )
        {
            _storage = storage;
            _configuration = configuration;
            _logger = logger;
        }

        private static string GetBucket(string region) =>
            region.ToUpper() == "VN" ? "bachelor-vn" : "bachelor-dk";

        private static string UserKey(string userId) => $"users/{userId}.json";

        public async Task AddUserAsync(CVAPI.Models.User user, string region = "DK")
        {
            var json = JsonConvert.SerializeObject(user);
            await _storage.PutObjectAsync(GetBucket(region), UserKey(user.UserId), json);
        }

        public async Task AddConsultantAsync(
            CVAPI.Models.Consultant consultant,
            string region = "DK"
        )
        {
            if (string.IsNullOrEmpty(consultant.Id))
                consultant.Id = Guid.NewGuid().ToString();
            if (consultant.DateAdded == default)
                consultant.DateAdded = DateTime.UtcNow;
            if (string.IsNullOrEmpty(consultant.UserId))
                consultant.UserId = consultant.Id;

            var json = JsonConvert.SerializeObject(consultant);
            await _storage.PutObjectAsync(GetBucket(region), UserKey(consultant.UserId), json);
        }

        public async Task AddApplicantAsync(Applicant applicant, string region = "DK")
        {
            try
            {
                Console.WriteLine(
                    $"[DEBUG] Attempting to save applicant: {applicant.FirstName} {applicant.LastName} in region {region}"
                );

                // Check if Id is empty, create a new one if needed
                if (string.IsNullOrEmpty(applicant.Id))
                {
                    applicant.Id = Guid.NewGuid().ToString();
                    Console.WriteLine($"[DEBUG] Generated new ID: {applicant.Id}");
                }

                // Set DateAdded if not already set
                if (applicant.DateAdded == default)
                {
                    applicant.DateAdded = DateTime.UtcNow;
                    Console.WriteLine($"[DEBUG] Set DateAdded: {applicant.DateAdded}");
                }

                // Ensure competencies is initialized
                if (applicant.Competencies == null)
                {
                    applicant.Competencies = new List<CompetencyCategory>();
                    Console.WriteLine("[DEBUG] Initialized empty competencies list");
                }

                // Log the competencies before saving
                Console.WriteLine(
                    $"[DEBUG] Competencies: {JsonConvert.SerializeObject(applicant.Competencies, Formatting.Indented)}"
                );

                var json = JsonConvert.SerializeObject(applicant);
                await _storage.PutObjectAsync(GetBucket(region), UserKey(applicant.UserId), json);

                Console.WriteLine("[DEBUG] Applicant successfully saved in Cellar.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to save applicant: {ex.Message}");
                Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
                throw; // Re-throw the exception for the caller to handle
            }
        }

        public async Task<CVAPI.Models.User?> GetUserAsync(string UserId, string region = "DK")
        {
            var json = await _storage.GetObjectAsync(GetBucket(region), UserKey(UserId));
            if (json == null) return null;
            return JsonConvert.DeserializeObject<CVAPI.Models.User>(json);
        }

        public async Task AddManagerAsync(Manager manager, string region = "DK")
        {
            try
            {
                Console.WriteLine(
                    $"[DEBUG] Attempting to save manager: {manager.FirstName} {manager.LastName} in region {region}"
                );

                // Check if Id is empty, create a new one if needed
                if (string.IsNullOrEmpty(manager.Id))
                {
                    manager.Id = Guid.NewGuid().ToString();
                    Console.WriteLine($"[DEBUG] Generated new ID: {manager.Id}");
                }

                // Check if UserId is empty, create a new one if needed
                if (string.IsNullOrEmpty(manager.UserId))
                {
                    manager.UserId = Guid.NewGuid().ToString();
                    Console.WriteLine($"[DEBUG] Generated new UserId: {manager.UserId}");
                }

                var json = JsonConvert.SerializeObject(manager);
                await _storage.PutObjectAsync(GetBucket(region), UserKey(manager.UserId), json);

                Console.WriteLine("[DEBUG] Manager successfully saved in Cellar.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to save manager: {ex.Message}");
                Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
                throw; // Re-throw the exception for the caller to handle
            }
        }

        public async Task<CVAPI.Models.Consultant?> GetConsultantAsync(
            string UserId,
            string region = "DK"
        )
        {
            var json = await _storage.GetObjectAsync(GetBucket(region), UserKey(UserId));
            if (json == null) return null;
            return JsonConvert.DeserializeObject<CVAPI.Models.Consultant>(json);
        }

        public async Task<CVAPI.Models.Applicant?> GetApplicantAsync(
            string UserId,
            string region = "DK"
        )
        {
            var json = await _storage.GetObjectAsync(GetBucket(region), UserKey(UserId));
            if (json == null) return null;
            return JsonConvert.DeserializeObject<CVAPI.Models.Applicant>(json);
        }

        public async Task<List<CVAPI.Models.Consultant>> GetAllConsultants(string region = "DK")
        {
            return await GetAllUsersOfType<CVAPI.Models.Consultant>(region, Role.Consultant);
        }

        public async Task<List<CVAPI.Models.Applicant>> GetAllApplicants(string region = "DK")
        {
            return await GetAllUsersOfType<CVAPI.Models.Applicant>(region, Role.Applicant);
        }

        public async Task<User?> GetUserByEmailAsync(string email, string region)
        {
            var keys = await _storage.ListObjectKeysAsync(GetBucket(region), "users/");
            foreach (var key in keys)
            {
                var json = await _storage.GetObjectAsync(GetBucket(region), key);
                if (json == null) continue;

                _logger.LogInformation($"[DEBUG] Raw database record: {json}");

                var user = JsonConvert.DeserializeObject<User>(json);
                if (user == null || !string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))
                    continue;

                switch (user.UserRole)
                {
                    case Role.Manager:
                        var manager = JsonConvert.DeserializeObject<Manager>(json);
                        _logger.LogInformation($"[DEBUG] Manager AdminInitials: {manager?.AdminInitials}");
                        return manager;
                    case Role.Admin:
                        var admin = JsonConvert.DeserializeObject<Admin>(json);
                        _logger.LogInformation($"[DEBUG] Admin AdminInitials: {admin?.AdminInitials}");
                        return admin;
                    default:
                        _logger.LogInformation($"[DEBUG] User is not a Manager or Admin. Role: {user.UserRole}");
                        return user;
                }
            }

            _logger.LogWarning($"[DEBUG] No user found with email: {email} in region: {region}");
            return null;
        }

        public async Task UpdateUserPasswordsToHashedAsync(string region = "DK")
        {
            var bucket = GetBucket(region);
            var keys = await _storage.ListObjectKeysAsync(bucket, "users/");
            var updated = 0;

            foreach (var key in keys)
            {
                var json = await _storage.GetObjectAsync(bucket, key);
                if (json == null) continue;

                var user = JsonConvert.DeserializeObject<User>(json)!;
                if (!string.IsNullOrEmpty(user.Password) && !user.Password.StartsWith("$2a$"))
                {
                    user.Password = PasswordHelper.HashPassword(user.Password);
                    await _storage.PutObjectAsync(bucket, key, JsonConvert.SerializeObject(user));
                    updated++;
                }
            }

            Console.WriteLine($"Updated {updated} user passwords in region {region}.");
        }

        public async Task<List<Manager>> GetAllManagersAsync(string region = "DK")
        {
            return await GetAllUsersOfType<Manager>(region, Role.Manager);
        }

        public async Task<List<Admin>> GetAllAdminsAsync(string region = "DK")
        {
            return await GetAllUsersOfType<Admin>(region, Role.Admin);
        }

        private async Task<List<T>> GetAllUsersOfType<T>(string region, Role role) where T : User
        {
            var bucket = GetBucket(region);
            var keys = await _storage.ListObjectKeysAsync(bucket, "users/");
            var result = new List<T>();

            foreach (var key in keys)
            {
                var json = await _storage.GetObjectAsync(bucket, key);
                if (json == null) continue;

                var user = JsonConvert.DeserializeObject<User>(json);
                if (user?.UserRole != role) continue;

                var typed = JsonConvert.DeserializeObject<T>(json);
                if (typed != null)
                    result.Add(typed);
            }

            return result;
        }

        public async Task UpdateUserPasswordAsync(
            string userId,
            string hashedPassword,
            string region = "DK"
        )
        {
            var json = await _storage.GetObjectAsync(GetBucket(region), UserKey(userId));
            if (json == null) throw new ArgumentException("User not found");

            var user = JsonConvert.DeserializeObject<User>(json)!;
            user.Password = hashedPassword;
            await _storage.PutObjectAsync(GetBucket(region), UserKey(userId), JsonConvert.SerializeObject(user));
        }

        public async Task DeleteUserAsync(string userId, string region = "DK")
        {
            _logger.LogInformation($"[DEBUG] DeleteUserAsync called with userId={userId}, region={region}");
            try
            {
                var key = UserKey(userId);
                var json = await _storage.GetObjectAsync(GetBucket(region), key);
                if (json == null)
                {
                    _logger.LogWarning($"[DEBUG] User not found for deletion: {userId}");
                    throw new ArgumentException("User not found");
                }

                await _storage.DeleteObjectAsync(GetBucket(region), key);
                _logger.LogInformation($"[DEBUG] Deleted user: userId={userId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[ERROR] Exception in DeleteUserAsync for userId={userId}");
                throw;
            }
        }

        public async Task DeleteApplicantAsync(string userId, string region)
        {
            _logger.LogInformation($"[DEBUG] DeleteApplicantAsync called with userId={userId}, region={region}");
            try
            {
                var json = await _storage.GetObjectAsync(GetBucket(region), UserKey(userId));
                if (json == null)
                {
                    _logger.LogWarning($"[DEBUG] Applicant not found for deletion: {userId}");
                    throw new ArgumentException("Applicant not found");
                }

                var applicant = JsonConvert.DeserializeObject<Applicant>(json)!;
                if (applicant.UserRole != Role.Applicant)
                    throw new ArgumentException("Applicant not found");

                await _storage.DeleteObjectAsync(GetBucket(region), UserKey(userId));
                _logger.LogInformation($"[DEBUG] Deleted applicant: userId={userId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[ERROR] Exception in DeleteApplicantAsync for userId={userId}");
                throw;
            }
        }

        public async Task UpdateManagerPasswordAsync(
            string userId,
            string newPassword,
            string region = "DK"
        )
        {
            Console.WriteLine("[DEBUG] UpdateManagerPasswordAsync called.");
            Console.WriteLine($"[DEBUG] UserId: {userId}, Region: {region}");

            var json = await _storage.GetObjectAsync(GetBucket(region), UserKey(userId));
            if (json == null)
            {
                Console.WriteLine("[ERROR] Manager not found.");
                throw new ArgumentException("Manager not found");
            }

            var manager = JsonConvert.DeserializeObject<Manager>(json)!;
            Console.WriteLine($"[DEBUG] Manager found: {manager.FirstName} {manager.LastName}");

            manager.Password = PasswordHelper.HashPassword(newPassword);
            Console.WriteLine("[DEBUG] Password hashed successfully.");

            await _storage.PutObjectAsync(GetBucket(region), UserKey(userId), JsonConvert.SerializeObject(manager));
            Console.WriteLine("[DEBUG] Manager password updated in Cellar.");
        }

        public async Task UpdateConsultantAsync(Consultant consultant, string region = "DK")
        {
            var json = JsonConvert.SerializeObject(consultant);
            await _storage.PutObjectAsync(GetBucket(region), UserKey(consultant.UserId), json);
        }

        public async Task UpdateApplicantAsync(Applicant applicant, string region = "DK")
        {
            var json = JsonConvert.SerializeObject(applicant);
            await _storage.PutObjectAsync(GetBucket(region), UserKey(applicant.UserId), json);
        }

        public async Task SavePrivateNoteAsync(
            string userId,
            string noteText,
            string adminInitials,
            string region = "DK"
        )
        {
            var applicant = await GetApplicantAsync(userId, region);
            if (applicant != null)
            {
                applicant.PrivateNotes ??= new List<PrivateNote>();
                applicant.PrivateNotes.Add(new PrivateNote { Text = noteText, AdminInitials = adminInitials });
                await _storage.PutObjectAsync(GetBucket(region), UserKey(userId), JsonConvert.SerializeObject(applicant));
                return;
            }

            var consultant = await GetConsultantAsync(userId, region);
            if (consultant != null)
            {
                consultant.PrivateNotes ??= new List<PrivateNote>();
                consultant.PrivateNotes.Add(new PrivateNote { Text = noteText, AdminInitials = adminInitials });
                await _storage.PutObjectAsync(GetBucket(region), UserKey(userId), JsonConvert.SerializeObject(consultant));
                return;
            }

            throw new ArgumentException($"No applicant or consultant found with ID {userId}.");
        }

        public async Task SavePrivateNoteViaEndpointAsync(
            string region,
            string userId,
            string note,
            string adminInitials
        )
        {
            // Use the base URL from configuration
            var baseUrl = _configuration["ApiBaseUrl"] ?? "https://bachelor-ete0e0e5d4cphjg7.germanywestcentral-01.azurewebsites.net/api/user";
            using var client = new HttpClient();
            var requestBody = new { UserId = userId, Note = note };

            var response = await client.PostAsJsonAsync(
                $"{baseUrl}/{region}/{userId}/private-notes",
                requestBody
            );

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Failed to save private note: {response.ReasonPhrase}");
            }
        }

        public async Task<List<PrivateNote>> GetPrivateNotesViaEndpointAsync(
            string region,
            string userId
        )
        {
            // Use the base URL from configuration
            var baseUrl = _configuration["ApiBaseUrl"] ?? "https://bachelor-ete0e0e5d4cphjg7.germanywestcentral-01.azurewebsites.net/api/user";
            using var client = new HttpClient();
            var response = await client.GetAsync($"{baseUrl}/{region}/{userId}/private-notes");

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Failed to retrieve private notes: {response.ReasonPhrase}");
            }

            // Deserialize the response content to a list of PrivateNote
            var privateNotes = await response.Content.ReadFromJsonAsync<List<PrivateNote>>();
            if (privateNotes == null)
            {
                throw new Exception("Failed to deserialize private notes.");
            }

            return privateNotes;
        }

        public async Task UpdateUserRoleAsync(string userId, int newRole, string region = "DK")
        {
            var json = await _storage.GetObjectAsync(GetBucket(region), UserKey(userId));
            if (json == null) throw new ArgumentException($"User with ID {userId} not found.");

            var user = JsonConvert.DeserializeObject<User>(json)!;
            user.UserRole = (Role)newRole;
            await _storage.PutObjectAsync(GetBucket(region), UserKey(userId), JsonConvert.SerializeObject(user));
        }

        public async Task<List<PrivateNote>> GetPrivateNotesAsync(
            string userId,
            string region = "DK"
        )
        {
            var consultant = await GetConsultantAsync(userId, region);
            if (consultant != null)
            {
                return consultant.PrivateNotes ?? new List<PrivateNote>();
            }

            var applicant = await GetApplicantAsync(userId, region);
            if (applicant != null)
            {
                return applicant.PrivateNotes ?? new List<PrivateNote>();
            }

            throw new ArgumentException($"No applicant or consultant found with ID {userId}.");
        }

        public async Task UpdateApplicantUserRoleAsync(string userId, Role newRole, string region = "DK")
        {
            var applicant = await GetApplicantAsync(userId, region);
            if (applicant == null)
                throw new ArgumentException($"Applicant with ID {userId} not found.");

            applicant.UserRole = newRole;
            await _storage.PutObjectAsync(GetBucket(region), UserKey(userId), JsonConvert.SerializeObject(applicant));
        }
    }
}
