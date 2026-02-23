using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using CVAPI.Models; // Din egen User model
using CVAPI.Services;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using User = CVAPI.Models.User;

namespace CVAPI.Repos
{
    public class UserRepository
    {
        private readonly CosmosClient _cosmosClient;
        private readonly string _databaseName = "DK";
        private readonly IConfiguration _configuration; // Add IConfiguration
        private readonly ILogger<UserRepository> _logger; // Add ILogger

        public UserRepository(
            CosmosClient cosmosClient,
            IConfiguration configuration,
            ILogger<UserRepository> logger
        )
        {
            _cosmosClient = cosmosClient;
            _configuration = configuration; // Assign IConfiguration
            _logger = logger; // Assign ILogger
        }

        private Container GetContainer(string region = "DK") // Default region set to "DK"
        {
            var database = _cosmosClient.GetDatabase(_databaseName);
            return database.GetContainer(region); // Region "DK" or "VN"
        }

        public async Task AddUserAsync(CVAPI.Models.User user, string region = "DK")
        {
            var container = GetContainer(region);
            await container.CreateItemAsync(user);
        }

        public async Task AddConsultantAsync(
            CVAPI.Models.Consultant consultant,
            string region = "DK"
        )
        {
            if (string.IsNullOrEmpty(consultant.Id))
            {
                consultant.Id = Guid.NewGuid().ToString();
            }
            if (consultant.DateAdded == default) // Hvis DateAdded ikke er sat, sæt den nu
            {
                consultant.DateAdded = DateTime.UtcNow;
            }

            var container = GetContainer(region);
            await container.CreateItemAsync(consultant);
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

                // Get container from Cosmos DB
                var container = GetContainer(region);

                // Save the applicant to Cosmos DB
                await container.CreateItemAsync(applicant);

                // Log success
                Console.WriteLine("[DEBUG] Applicant successfully saved in CosmosDB.");
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
            var query = GetContainer(region)
                .GetItemQueryIterator<CVAPI.Models.User>(
                    new QueryDefinition("SELECT * FROM c WHERE c.UserId = @UserId").WithParameter(
                        "@UserId",
                        UserId
                    )
                );

            while (query.HasMoreResults)
            {
                var response = await query.ReadNextAsync();
                foreach (var user in response)
                {
                    return user;
                }
            }
            return null;
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

                // Get container from Cosmos DB
                var container = GetContainer(region);

                // Save the manager to Cosmos DB
                await container.CreateItemAsync(manager);

                // Log success
                Console.WriteLine("[DEBUG] Manager successfully saved in CosmosDB.");
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
            var query = GetContainer(region)
                .GetItemQueryIterator<CVAPI.Models.Consultant>(
                    new QueryDefinition("SELECT * FROM c WHERE c.UserId = @UserId").WithParameter(
                        "@UserId",
                        UserId
                    )
                );

            // Gennemgår alle resultaterne fra forespørgslen
            while (query.HasMoreResults)
            {
                var response = await query.ReadNextAsync();
                foreach (var consultant in response)
                {
                    return consultant; // Returner den første Consultant, der matcher UserId
                }
            }

            // Hvis ingen Consultant er fundet, returner null
            return null;
        }

        public async Task<CVAPI.Models.Applicant?> GetApplicantAsync(
            string UserId,
            string region = "DK"
        )
        {
            var query = GetContainer(region)
                .GetItemQueryIterator<CVAPI.Models.Applicant>(
                    new QueryDefinition("SELECT * FROM c WHERE c.UserId = @UserId").WithParameter(
                        "@UserId",
                        UserId
                    )
                );

            while (query.HasMoreResults)
            {
                var response = await query.ReadNextAsync();
                foreach (var applicant in response)
                {
                    return applicant;
                }
            }

            return null;
        }

        public async Task<List<CVAPI.Models.Consultant>> GetAllConsultants(string region = "DK")
        {
            var container = GetContainer(region);
            var query = container.GetItemQueryIterator<CVAPI.Models.Consultant>(
                new QueryDefinition("SELECT * FROM c WHERE c.UserRole = 1")
            );

            var consultants = new List<CVAPI.Models.Consultant>();

            while (query.HasMoreResults)
            {
                var response = await query.ReadNextAsync();
                foreach (var consultant in response)
                {
                    Console.WriteLine(
                        $"Consultant: {consultant.FirstName} {consultant.LastName} - Competencies Count: {consultant.Competencies?.Count ?? 0}"
                    );
                    consultants.Add(consultant);
                }
            }

            Console.WriteLine($"Total Consultants Loaded: {consultants.Count}");
            return consultants;
        }

        public async Task<List<CVAPI.Models.Applicant>> GetAllApplicants(string region = "DK")
        {
            var container = GetContainer(region);
            var query = container.GetItemQueryIterator<CVAPI.Models.Applicant>(
                new QueryDefinition("SELECT * FROM c WHERE c.UserRole = 0")
            );

            var applicants = new List<CVAPI.Models.Applicant>();

            while (query.HasMoreResults)
            {
                var response = await query.ReadNextAsync();
                applicants.AddRange(response);
            }

            return applicants;
        }

        public async Task<User> GetUserByEmailAsync(string email, string region)
        {
            var container = GetContainer(region);

            // Query to find the user by email
            var query = container.GetItemQueryIterator<dynamic>(
                new QueryDefinition("SELECT * FROM c WHERE c.Email = @Email").WithParameter(
                    "@Email",
                    email
                )
            );

            while (query.HasMoreResults)
            {
                var response = await query.ReadNextAsync();
                foreach (var record in response)
                {
                    // Log the raw database record for debugging
                    _logger.LogInformation(
                        $"[DEBUG] Raw database record: {JsonConvert.SerializeObject(record)}"
                    );

                    // Deserialize the record into the User object
                    var user = JsonConvert.DeserializeObject<User>(
                        JsonConvert.SerializeObject(record)
                    );

                    // Check the UserRole and cast to the appropriate subclass
                    switch (user.UserRole)
                    {
                        case Role.Manager:
                            var manager = JsonConvert.DeserializeObject<Manager>(
                                JsonConvert.SerializeObject(record)
                            );
                            _logger.LogInformation(
                                $"[DEBUG] Manager AdminInitials: {manager.AdminInitials}"
                            );
                            return manager;

                        case Role.Admin:
                            var admin = JsonConvert.DeserializeObject<Admin>(
                                JsonConvert.SerializeObject(record)
                            );
                            _logger.LogInformation(
                                $"[DEBUG] Admin AdminInitials: {admin.AdminInitials}"
                            );
                            return admin;

                        default:
                            _logger.LogInformation(
                                $"[DEBUG] User is not a Manager or Admin. Role: {user.UserRole}"
                            );
                            return user;
                    }
                }
            }

            _logger.LogWarning($"[DEBUG] No user found with email: {email} in region: {region}");
            return null;
        }

        public async Task UpdateUserPasswordsToHashedAsync(string region = "DK")
        {
            var container = GetContainer(region);
            var query = container.GetItemQueryIterator<User>(
                new QueryDefinition("SELECT * FROM c")
            );

            var users = new List<User>();

            while (query.HasMoreResults)
            {
                var response = await query.ReadNextAsync();
                users.AddRange(response);
            }

            foreach (var user in users)
            {
                // Tjek om passwordet ikke allerede er hashet
                if (!string.IsNullOrEmpty(user.Password) && !user.Password.StartsWith("$2a$"))
                {
                    // Hasher passwordet
                    user.Password = PasswordHelper.HashPassword(user.Password);

                    // Opdater relaterede objekter som Consultant eller Applicant
                    var consultant = await GetConsultantAsync(user.UserId, region);
                    if (consultant != null)
                    {
                        consultant.Password = user.Password;
                        await container.UpsertItemAsync(consultant); // Opdater Consultant
                    }

                    var applicant = await GetApplicantAsync(user.UserId, region);
                    if (applicant != null)
                    {
                        applicant.Password = user.Password;
                        await container.UpsertItemAsync(applicant); // Opdater Applicant
                    }

                    // Opdater brugeren i databasen
                    await container.UpsertItemAsync(user);
                }
            }

            Console.WriteLine($"Updated {users.Count} user passwords.");
        }

        public async Task<List<Manager>> GetAllManagersAsync(string region = "DK")
        {
            var container = GetContainer(region);
            var query = container.GetItemQueryIterator<Manager>(
                new QueryDefinition(
                    "SELECT * FROM c WHERE c.UserRole = @managerRole"
                ).WithParameter("@managerRole", (int)Role.Manager)
            );

            var managers = new List<Manager>();

            while (query.HasMoreResults)
            {
                var response = await query.ReadNextAsync();
                managers.AddRange(response);
            }

            return managers;
        }

        public async Task<List<Admin>> GetAllAdminsAsync(string region = "DK")
        {
            var container = GetContainer(region);
            var query = container.GetItemQueryIterator<Admin>(
                new QueryDefinition("SELECT * FROM c WHERE c.UserRole = @adminRole").WithParameter(
                    "@adminRole",
                    (int)Role.Admin
                )
            );

            var admins = new List<Admin>();

            while (query.HasMoreResults)
            {
                var response = await query.ReadNextAsync();
                admins.AddRange(response);
            }

            return admins;
        }

        public async Task UpdateUserPasswordAsync(
            string userId,
            string hashedPassword,
            string region = "DK"
        )
        {
            var container = GetContainer(region);

            // Query to find the user
            var query = container.GetItemQueryIterator<User>(
                new QueryDefinition("SELECT * FROM c WHERE c.UserId = @userId").WithParameter(
                    "@userId",
                    userId
                )
            );

            while (query.HasMoreResults)
            {
                var response = await query.ReadNextAsync();
                foreach (var user in response)
                {
                    // Update the password
                    user.Password = hashedPassword;

                    // Upsert the updated user
                    await container.UpsertItemAsync(user);
                    return;
                }
            }

            throw new ArgumentException("User not found");
        }

        public async Task DeleteUserAsync(string userId, string region = "DK")
        {
            _logger.LogInformation($"[DEBUG] DeleteUserAsync called with userId={userId}, region={region}");
            try
            {
                var container = GetContainer(region);
                // Find the user first
                var query = container.GetItemQueryIterator<User>(
                    new QueryDefinition("SELECT * FROM c WHERE c.UserId = @userId")
                        .WithParameter("@userId", userId)
                );
                while (query.HasMoreResults)
                {
                    var response = await query.ReadNextAsync();
                    foreach (var user in response)
                    {
                        _logger.LogInformation($"[DEBUG] Attempting to delete user: id={user.Id}, userId={user.UserId}");
                        Console.WriteLine($"[DEBUG] Attempting to delete user: id={user.Id}, userId={user.UserId}");
                        await container.DeleteItemAsync<User>(
                            user.Id,
                            new PartitionKey(user.UserId)
                        );
                        _logger.LogInformation($"[DEBUG] Deleted user: id={user.Id}, userId={user.UserId}");
                        Console.WriteLine($"[DEBUG] Deleted user: id={user.Id}, userId={user.UserId}");
                        return;
                    }
                }
                _logger.LogWarning($"[DEBUG] User not found for deletion: {userId}");
                Console.WriteLine($"[DEBUG] User not found for deletion: {userId}");
                throw new ArgumentException("User not found");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[ERROR] Exception in DeleteUserAsync for userId={userId}");
                Console.WriteLine($"[ERROR] Exception in DeleteUserAsync: {ex}");
                throw;
            }
        }

        public async Task DeleteApplicantAsync(string userId, string region)
        {
            _logger.LogInformation($"[DEBUG] DeleteApplicantAsync called with userId={userId}, region={region}");
            try
            {
                var container = GetContainer(region);
                var query = $"SELECT * FROM c WHERE c.UserId = @userId AND c.UserRole = {(int)Role.Applicant}";
                var queryDef = new QueryDefinition(query)
                    .WithParameter("@userId", userId);
                var results = container.GetItemQueryIterator<Applicant>(queryDef);
                while (results.HasMoreResults)
                {
                    var response = await results.ReadNextAsync();
                    var applicant = response.FirstOrDefault();
                    if (applicant != null)
                    {
                        _logger.LogInformation($"[DEBUG] Attempting to delete applicant: id={applicant.Id}, userId={applicant.UserId}");
                        Console.WriteLine($"[DEBUG] Attempting to delete applicant: id={applicant.Id}, userId={applicant.UserId}");
                        await container.DeleteItemAsync<Applicant>(
                            applicant.Id,
                            new PartitionKey(applicant.UserId)
                        );
                        _logger.LogInformation($"[DEBUG] Deleted applicant: id={applicant.Id}, userId={applicant.UserId}");
                        Console.WriteLine($"[DEBUG] Deleted applicant: id={applicant.Id}, userId={applicant.UserId}");
                        return;
                    }
                }
                _logger.LogWarning($"[DEBUG] Applicant not found for deletion: {userId}");
                Console.WriteLine($"[DEBUG] Applicant not found for deletion: {userId}");
                throw new ArgumentException("Applicant not found");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[ERROR] Exception in DeleteApplicantAsync for userId={userId}");
                Console.WriteLine($"[ERROR] Exception in DeleteApplicantAsync: {ex}");
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

            var container = GetContainer(region);

            // Query to find the manager
            var query = container.GetItemQueryIterator<Manager>(
                new QueryDefinition("SELECT * FROM c WHERE c.UserId = @userId").WithParameter(
                    "@userId",
                    userId
                )
            );

            while (query.HasMoreResults)
            {
                var response = await query.ReadNextAsync();
                foreach (var manager in response)
                {
                    Console.WriteLine(
                        $"[DEBUG] Manager found: {manager.FirstName} {manager.LastName}"
                    );

                    // Update the password
                    manager.Password = PasswordHelper.HashPassword(newPassword);
                    Console.WriteLine("[DEBUG] Password hashed successfully.");

                    // Upsert the updated manager
                    await container.UpsertItemAsync(manager);
                    Console.WriteLine("[DEBUG] Manager password updated in the database.");
                    return;
                }
            }

            Console.WriteLine("[ERROR] Manager not found.");
            throw new ArgumentException("Manager not found");
        }

        public async Task UpdateConsultantAsync(Consultant consultant, string region = "DK")
        {
            var container = GetContainer(region);
            await container.UpsertItemAsync(consultant, new PartitionKey(consultant.UserId));
        }

        public async Task UpdateApplicantAsync(Applicant applicant, string region = "DK")
        {
            var container = GetContainer(region);
            await container.UpsertItemAsync(applicant, new PartitionKey(applicant.UserId));
        }

        public async Task SavePrivateNoteAsync(
            string userId,
            string noteText,
            string adminInitials,
            string region = "DK" // Default region to "DK"
        )
        {
            var container = GetContainer(region);

            // Try to retrieve the applicant
            var applicant = await GetApplicantAsync(userId, region);
            if (applicant != null)
            {
                if (applicant.PrivateNotes == null)
                {
                    applicant.PrivateNotes = new List<PrivateNote>();
                }

                applicant.PrivateNotes.Add(
                    new PrivateNote { Text = noteText, AdminInitials = adminInitials }
                );

                // Ensure the correct Id and PartitionKey are set for Cosmos DB
                applicant.Id = applicant.Id ?? Guid.NewGuid().ToString();
                await container.UpsertItemAsync(applicant, new PartitionKey(applicant.UserId));
                return;
            }

            // Try to retrieve the consultant
            var consultant = await GetConsultantAsync(userId, region);
            if (consultant != null)
            {
                if (consultant.PrivateNotes == null)
                {
                    consultant.PrivateNotes = new List<PrivateNote>();
                }

                consultant.PrivateNotes.Add(
                    new PrivateNote { Text = noteText, AdminInitials = adminInitials }
                );

                // Ensure the correct Id and PartitionKey are set for Cosmos DB
                consultant.Id = consultant.Id ?? Guid.NewGuid().ToString();
                await container.UpsertItemAsync(consultant, new PartitionKey(consultant.UserId));
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
            var container = GetContainer(region);
            var user = await GetUserAsync(userId, region);
            if (user != null)
            {
                // Update the user's role
                user.UserRole = (Role)newRole;

                // Save the updated user back to the database
                await container.UpsertItemAsync(user);
            }
            else
            {
                throw new ArgumentException($"User with ID {userId} not found.");
            }
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

            var container = GetContainer(region);

            // Retrieve the applicant
            var applicant = await GetApplicantAsync(userId, region);
            if (applicant == null)
            {
                throw new ArgumentException($"Applicant with ID {userId} not found.");
            }

            // Update the user role
            applicant.UserRole = newRole;

            // Save the updated applicant
            await container.UpsertItemAsync(applicant, new PartitionKey(applicant.UserId));
        }
    }
}
