using System;
using System.Threading.Tasks;
using CVAPI.Models;
using CVAPI.Repos;
using CVAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace CVAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly UserRepository _userRepository;
        private readonly ILogger<UserController> _logger; // Tilføj loggeren
        private readonly JwtTokenService _jwtTokenService; // Tilføj JwtTokenService
        private readonly SharePointUploader _sharePointUploader; // Add this line

        public UserController(
            UserRepository userRepository,
            JwtTokenService jwtTokenService,
            ILogger<UserController> logger,
            SharePointUploader sharePointUploader
        )
        {
            _userRepository = userRepository;
            _jwtTokenService = jwtTokenService;
            _logger = logger;
            _sharePointUploader = sharePointUploader; // Add this line
        }

        // Get a specific user by UserId through the repository
        [HttpGet("{region}/{userId}")]
        public async Task<IActionResult> GetUser(string region, string UserId)
        {
            var user = await _userRepository.GetUserAsync(UserId, region);
            if (user == null)
            {
                return NotFound("User not found.");
            }

            return Ok(user);
        }

        [HttpPost("{region}/create-user")]
        public async Task<IActionResult> CreateUser(string region, [FromBody] User newUser)
        {
            if (newUser == null || string.IsNullOrWhiteSpace(newUser.Password))
            {
                return BadRequest("Brugerdata er ugyldige.");
            }

            newUser.UserId = Guid.NewGuid().ToString(); // Genererer en ny UserId

            // Hash brugerens password, før det gemmes
            newUser.Password = PasswordHelper.HashPassword(newUser.Password);

            await _userRepository.AddUserAsync(newUser, region);

            return CreatedAtAction(
                nameof(GetUser),
                new { region, UserId = newUser.UserId },
                newUser
            );
        }

        // Opret en ny Consultant i en region
        [HttpPost("{region}/create-consultant")]
        public async Task<IActionResult> CreateConsultant(
            string region,
            [FromBody] Consultant newConsultant
        )
        {
            if (newConsultant == null || string.IsNullOrWhiteSpace(newConsultant.Password))
            {
                return BadRequest("Consultant data is invalid.");
            }

            newConsultant.UserId = Guid.NewGuid().ToString();
            newConsultant.DateAdded = DateTime.UtcNow;

            // Hash passwordet, før det gemmes
            newConsultant.Password = PasswordHelper.HashPassword(newConsultant.Password);

            await _userRepository.AddConsultantAsync(newConsultant, region);

            return CreatedAtAction(
                nameof(GetUser),
                new { region, userId = newConsultant.UserId },
                newConsultant
            );
        }

        // Get all consultants from a specific region
        [HttpGet("{region}/consultants")]
        public async Task<IActionResult> GetAllConsultants(string region)
        {
            try
            {
                _logger.LogInformation($"[DEBUG] Fetching consultants for region: {region}");

                var consultants = await _userRepository.GetAllConsultants(region);

                _logger.LogInformation($"[DEBUG] Retrieved {consultants?.Count ?? 0} consultants");

                if (consultants == null || consultants.Count == 0)
                {
                    _logger.LogWarning($"[WARN] No consultants found for region: {region}");
                    return NotFound("No consultants found.");
                }

                // Log the first consultant as a sample (avoid logging sensitive data)
                if (consultants.Any())
                {
                    var sample = consultants.First();
                    _logger.LogInformation(
                        $"[DEBUG] Sample consultant - ID: {sample.UserId}, Name: {sample.FirstName} {sample.LastName}"
                    );
                }

                return Ok(consultants);
            }
            catch (Exception ex)
            {
                _logger.LogError($"[ERROR] Failed to retrieve consultants: {ex.Message}");
                _logger.LogError($"[ERROR] Stack trace: {ex.StackTrace}");
                return StatusCode(500, "An error occurred while retrieving consultants.");
            }
        }

        [HttpPost("{region}/create-manager")]
        public async Task<IActionResult> CreateManager(string region, [FromBody] Manager model)
        {
            try
            {
                // Valider input
                if (
                    model == null
                    || string.IsNullOrWhiteSpace(model.Email)
                    || string.IsNullOrWhiteSpace(model.Password)
                )
                {
                    return BadRequest("Manager data is invalid.");
                }

                // Generer GUID'er for Id og UserId, hvis de ikke allerede er sat
                model.Id = string.IsNullOrEmpty(model.Id) ? Guid.NewGuid().ToString() : model.Id;
                model.UserId = string.IsNullOrEmpty(model.UserId)
                    ? Guid.NewGuid().ToString()
                    : model.UserId;

                // Hash passwordet
                model.Password = PasswordHelper.HashPassword(model.Password);

                // Sæt UserRole til Manager
                model.UserRole = Role.Manager;

                // Gem Manageren i databasen
                await _userRepository.AddUserAsync(model, region);

                // Returner succes
                return CreatedAtAction(
                    nameof(GetUser),
                    new { region, UserId = model.UserId },
                    model
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to create manager: {ex.Message}");
                return StatusCode(500, "An error occurred while creating the manager.");
            }
        }

        [HttpPost("{region}/create-applicant")]
        public async Task<IActionResult> CreateApplicant(
            string region,
            [FromBody] Applicant newApplicant
        )
        {
            try
            {
                // More lenient validation - check if user session exists or if this is a temp session
                var userId = HttpContext.Session.GetString("UserId");
                var userRole = HttpContext.Session.GetString("UserRole");
                
                if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(userRole))
                {
                    // Create a minimal temp session if none exists
                    HttpContext.Session.SetString("UserId", "temp-" + Guid.NewGuid().ToString());
                    HttpContext.Session.SetString("UserRole", "2"); // Consultant role
                    HttpContext.Session.SetString("UserRegion", region); // Set the region
                    _logger.LogInformation($"Created temporary session for applicant creation in region: {region}");
                }

                // Ensure region is set in session
                var sessionRegion = HttpContext.Session.GetString("UserRegion");
                if (string.IsNullOrEmpty(sessionRegion))
                {
                    HttpContext.Session.SetString("UserRegion", region);
                    _logger.LogInformation($"Set region in session: {region}");
                }

                // Validate region access with the updated session
                if (!ValidateRegionAccess(region))
                {
                    var userRegion = HttpContext.Session.GetString("UserRegion");
                    Console.WriteLine($"[SECURITY] Unauthorized region access attempt: User from {userRegion} tried to access {region}");
                    return StatusCode(403, new { error = "Access denied", message = $"You do not have permission to create applicants in the {region} region." });
                }

                Console.WriteLine("Received applicant data:");
                Console.WriteLine(JsonConvert.SerializeObject(newApplicant, Formatting.Indented));

                if (newApplicant == null)
                {
                    return BadRequest("Applicant data is invalid.");
                }

                // Validate competencies structure
                if (newApplicant.Competencies == null)
                {
                    newApplicant.Competencies = new List<CompetencyCategory>();
                    Console.WriteLine("No competencies provided, initialized as empty list.");
                }
                else
                {
                    // Log competencies for debugging
                    Console.WriteLine("Received competencies:");
                    Console.WriteLine(
                        JsonConvert.SerializeObject(newApplicant.Competencies, Formatting.Indented)
                    );
                }

                // Set required fields
                newApplicant.UserId = Guid.NewGuid().ToString();
                newApplicant.DateAdded = DateTime.UtcNow;

                // Hash the password
                if (!string.IsNullOrEmpty(newApplicant.Password))
                {
                    newApplicant.Password = PasswordHelper.HashPassword(newApplicant.Password);
                }
                else
                {
                    // Generate a random password if none provided
                    string generatedPassword = PasswordHelper.GenerateRandomPassword(12);
                    newApplicant.Password = PasswordHelper.HashPassword(generatedPassword);
                    // You may want to store or return this generated password
                }

                // Fjern CV-håndtering fra denne metode da det nu håndteres separat
                // CV filename kommer fra den separate upload process
                if (!string.IsNullOrEmpty(newApplicant.CV))
                {
                    _logger.LogInformation($"CV filename received: {newApplicant.CV}");
                }

                // Ensure profile picture URL is preserved if it was uploaded
                if (!string.IsNullOrEmpty(newApplicant.ProfilePicture))
                {
                    _logger.LogInformation($"Profile picture URL received: {newApplicant.ProfilePicture}");
                }
                else
                {
                    _logger.LogInformation("No profile picture URL provided");
                }

                // If a profile picture URL exists in the applicant object, make sure it's preserved
                if (!string.IsNullOrEmpty(newApplicant.ProfilePicture))
                {
                    _logger.LogInformation($"Preserving profile picture URL: {newApplicant.ProfilePicture}");
                }
                else
                {
                    _logger.LogInformation("No profile picture URL to preserve");
                }

                // Add the applicant to the database
                await _userRepository.AddApplicantAsync(newApplicant, region);

                return CreatedAtAction(
                    nameof(GetUser),
                    new { region, userId = newApplicant.UserId },
                    newApplicant
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating applicant: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return StatusCode(500, "An error occurred while processing your request.");
            }
        }

        // Get all consultants from a specific region
        [HttpGet("{region}/applicants")]
        public async Task<IActionResult> GetAllApplicants(string region)
        {
            var applicants = await _userRepository.GetAllApplicants(region); // Hent ansøgere fra repository
            if (applicants == null || applicants.Count == 0)
            {
                return NotFound("No consultants found.");
            }

            return Ok(applicants); // Returner alle ansøgere som JSON
        }

        [HttpPost("{region}/update-rating")]
        public async Task<IActionResult> UpdateRating(
            string region,
            [FromBody] RatingUpdateModel model
        )
        {
            try
            {
                if (model == null || string.IsNullOrEmpty(model.UserId))
                {
                    return BadRequest("Invalid user ID or rating");
                }

                // Use the provided region parameter
                region = RegionConfiguration.NormalizeRegion(region);

                // Get the consultant
                var consultant = await _userRepository.GetConsultantAsync(model.UserId, region);

                if (consultant == null)
                {
                    return NotFound($"Consultant with ID {model.UserId} not found.");
                }

                // Update the correct rating
                if (!string.IsNullOrEmpty(model.Type) && model.Type.ToLower() == "tech")
                {
                    consultant.TechRating = model.Rating;
                }
                else
                {
                    consultant.PrivateRating = model.Rating;
                }

                // Save the updated consultant
                await _userRepository.UpdateConsultantAsync(consultant, region);

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] {ex.Message}");
                return BadRequest($"Error updating rating: {ex.Message}");
            }
        }

        public class RatingUpdateModel
        {
            public string UserId { get; set; }
            public int Rating { get; set; }
            public string Type { get; set; } // Add this property to distinguish between private and tech rating
        }

        [HttpPost("{region}/login")]
        public async Task<IActionResult> Login(string region, [FromBody] LoginModel loginModel)
        {
            _logger.LogInformation(
                $"Login attempt from region: {region} for email: {loginModel.Email}"
            );

            if (
                string.IsNullOrWhiteSpace(loginModel.Email)
                || string.IsNullOrWhiteSpace(loginModel.Password)
            )
            {
                return BadRequest(new { message = "Email and password must be given." });
            }

            var user = await _userRepository.GetUserByEmailAsync(loginModel.Email, region);
            if (user == null || !PasswordHelper.VerifyPassword(loginModel.Password, user.Password))
            {
                return Unauthorized(new { message = "Invalid credentials" });
            }

            // Store user info in session
            HttpContext.Session.SetString("UserId", user.UserId);
            HttpContext.Session.SetString("UserRole", ((int)user.UserRole).ToString());
            HttpContext.Session.SetString("UserEmail", user.Email);
            HttpContext.Session.SetString("UserRegion", region); // Store selected region

            // Get admin initials if applicable and set cookie
            string adminInitials = GetAdminInitials(user);
            if (!string.IsNullOrEmpty(adminInitials))
            {
                SetAdminInitialsCookie(adminInitials);
            }

            // Generate JWT token
            var token = _jwtTokenService.GenerateJwtToken(user);

            // Note: JWT token will be returned in response body only (no cookies)
            // Frontend will handle storing in localStorage

            // Create complete response with numeric role value and always include email
            var response = new
            {
                token = token,
                userId = user.UserId,
                userRole = ((int)user.UserRole).ToString(),
                adminInitials = adminInitials,
                region = region,
                email = user.Email,
                message = "Login successful",
            };

            _logger.LogInformation(
                $"[DEBUG] Login successful for {user.Email}, role: {(int)user.UserRole}"
            );
            return Ok(response);
        }

        private string GetAdminInitials(User user)
        {
            return user switch
            {
                Manager manager => manager.AdminInitials,
                Admin admin => admin.AdminInitials,
                _ => string.Empty,
            };
        }

        private void SetAdminInitialsCookie(string adminInitials)
        {
            Response.Cookies.Append(
                "adminInitials",
                adminInitials,
                new CookieOptions
                {
                    HttpOnly = false,
                    Secure = false,
                    SameSite = SameSiteMode.Lax,
                    Path = "/",
                    Expires = DateTime.UtcNow.AddHours(1),
                }
            );
            _logger.LogInformation($"[DEBUG] Admin initials set in cookies: {adminInitials}");
        }

        private bool ValidateRegionAccess(string requestedRegion)
        {
            var userRegion = HttpContext.Session.GetString("UserRegion");
            return !string.IsNullOrEmpty(userRegion) && userRegion.Equals(requestedRegion, StringComparison.OrdinalIgnoreCase);
        }

        private bool ValidateSession()
        {
            var userId = HttpContext.Session.GetString("UserId");
            var userRole = HttpContext.Session.GetString("UserRole");
            return !string.IsNullOrEmpty(userId) && !string.IsNullOrEmpty(userRole);
        }

        [HttpPost("{region}/update-passwords")] // Fixed name so that its NOT the same as the one below
        public async Task<IActionResult> UpdatePasswords(string region)
        {
            try
            {
                await _userRepository.UpdateUserPasswordsToHashedAsync(region);
                return Ok("Passwords updated to hashed values.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"[ERROR] Failed to update passwords: {ex.Message}");
                return BadRequest($"Error: {ex.Message}");
            }
        }

        [HttpPost("{region}/update-password")]
        public async Task<IActionResult> UpdatePassword(
            string region,
            [FromBody] PasswordUpdateRequest request
        )
        {
            _logger.LogInformation(
                $"[DEBUG] UpdatePassword endpoint called for UserId: {request.UserId}"
            );

            if (string.IsNullOrEmpty(request.UserId) || string.IsNullOrEmpty(request.NewPassword) || string.IsNullOrEmpty(request.CurrentPassword))
            {
                _logger.LogError("[ERROR] UserId, CurrentPassword, or NewPassword is missing.");
                return BadRequest("UserId, CurrentPassword, and NewPassword are required.");
            }

            try
            {
                // First, verify the current password
                _logger.LogInformation("[DEBUG] Verifying current password.");
                var user = await _userRepository.GetUserAsync(request.UserId, region);
                if (user == null)
                {
                    _logger.LogError($"[ERROR] User not found: {request.UserId}");
                    return BadRequest("User not found.");
                }
                
                if (!PasswordHelper.VerifyPassword(request.CurrentPassword, user.Password))
                {
                    _logger.LogError("[ERROR] Current password is incorrect.");
                    return BadRequest("Current password is incorrect.");
                }

                _logger.LogInformation("[DEBUG] Current password verified. Attempting to update password in the repository.");
                await _userRepository.UpdateManagerPasswordAsync(
                    request.UserId,
                    request.NewPassword,
                    region
                );
                _logger.LogInformation("[DEBUG] Password updated successfully.");
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError($"[ERROR] Failed to update password: {ex.Message}");
                return StatusCode(500, "An error occurred while updating the password.");
            }
        }

        [HttpPost("{region}/save-private-note")]
        public async Task<IActionResult> SavePrivateNote(
            string region,
            [FromBody] SaveNoteRequest request
        )
        {
            try
            {
                _logger.LogInformation(
                    $"[DEBUG] Incoming Request: Region={region}, UserId={request.UserId}, Note={request.Note}"
                );

                // Validate the request
                if (string.IsNullOrEmpty(request.UserId) || string.IsNullOrEmpty(request.Note))
                {
                    _logger.LogError("[ERROR] UserId or Note is missing.");
                    return BadRequest("UserId and Note are required.");
                }

                var adminInitials = Request.Cookies["adminInitials"];
                if (string.IsNullOrEmpty(adminInitials))
                {
                    _logger.LogError("[ERROR] Admin initials are missing in the cookies.");
                    return Unauthorized("Admin initials are missing.");
                }

                _logger.LogInformation($"[DEBUG] Retrieved Admin Initials: {adminInitials}");

                await _userRepository.SavePrivateNoteAsync(
                    request.UserId,
                    request.Note,
                    adminInitials // No need to pass region explicitly
                );

                _logger.LogInformation("[DEBUG] Private note saved successfully.");
                return Ok("Private note saved successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"[ERROR] An error occurred: {ex.Message}");
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }

        [HttpPost("{region}/{userId}/private-notes")]
        public async Task<IActionResult> PostPrivateNote(
            string region,
            string userId,
            [FromBody] SaveNoteRequest request
        )
        {
            try
            {
                _logger.LogInformation(
                    $"[DEBUG] Saving private note for UserId: {userId} in Region: {region}"
                );

                // Validate the request
                if (string.IsNullOrEmpty(request.Note))
                {
                    _logger.LogError("[ERROR] Note text is missing.");
                    return BadRequest("Note text is required.");
                }

                var adminInitials = Request.Cookies["adminInitials"];
                if (string.IsNullOrEmpty(adminInitials))
                {
                    _logger.LogError("[ERROR] Admin initials are missing in the cookies.");
                    return Unauthorized("Admin initials are missing.");
                }

                _logger.LogInformation($"[DEBUG] Retrieved Admin Initials: {adminInitials}");

                // Save the private note
                await _userRepository.SavePrivateNoteAsync(
                    userId,
                    request.Note,
                    adminInitials,
                    region
                );

                _logger.LogInformation("[DEBUG] Private note saved successfully.");
                return Ok("Private note saved successfully.");
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning($"[WARNING] {ex.Message}");
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError($"[ERROR] An error occurred: {ex.Message}");
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }

        [HttpGet("{region}/{userId}/private-notes")]
        public async Task<IActionResult> GetPrivateNotes(string region, string userId)
        {
            try
            {
                _logger.LogInformation(
                    $"[DEBUG] Fetching private notes for UserId: {userId} in Region: {region}"
                );

                var privateNotes = await _userRepository.GetPrivateNotesAsync(userId, region);

                _logger.LogInformation(
                    $"[DEBUG] Retrieved {privateNotes.Count} private notes for UserId: {userId}"
                );
                return Ok(privateNotes);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning($"[WARNING] {ex.Message}");
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError($"[ERROR] An error occurred: {ex.Message}");
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }

        [HttpPost("{region}/update-applicant")]
        public async Task<IActionResult> UpdateApplicant(
            string region,
            [FromBody] Applicant updatedApplicant
        )
        {
            try
            {
                if (updatedApplicant == null || string.IsNullOrEmpty(updatedApplicant.UserId))
                {
                    return BadRequest("Applicant data is invalid.");
                }

                // Use the provided region parameter
                region = RegionConfiguration.NormalizeRegion(region);

                // Update the applicant in the repository
                await _userRepository.UpdateApplicantAsync(updatedApplicant, region);

                return Ok("Applicant updated successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"[ERROR] Failed to update applicant: {ex.Message}");
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }

        [HttpPost("{region}/update-languages")]
        public async Task<IActionResult> UpdateLanguages(
            string region,
            [FromBody] LanguageUpdateModel model
        )
        {
            try
            {
                if (model == null || string.IsNullOrEmpty(model.UserId))
                {
                    return BadRequest("Invalid request data.");
                }

                region = RegionConfiguration.NormalizeRegion(region);
                var consultant = await _userRepository.GetConsultantAsync(model.UserId, region);
                if (consultant == null)
                {
                    return NotFound($"Consultant with ID {model.UserId} not found.");
                }

                consultant.Languages = model
                    .Languages.Select(l => new Language
                    {
                        LanguageName = l.LanguageName,
                        LanguageLevel = l.LanguageLevel,
                    })
                    .ToList();

                await _userRepository.UpdateConsultantAsync(consultant, region);
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError($"[ERROR] {ex.Message}");
                return BadRequest($"Error updating languages: {ex.Message}");
            }
        }

        public class LanguageUpdateModel
        {
            public string UserId { get; set; }
            public List<LanguageItemModel> Languages { get; set; }
        }

        public class LanguageItemModel
        {
            public string LanguageName { get; set; }
            public int LanguageLevel { get; set; }
        }

        [HttpPost("{region}/update-education")]
        public async Task<IActionResult> UpdateEducation(
            string region,
            [FromBody] UpdateEducationModel model
        )
        {
            try
            {
                if (model == null || string.IsNullOrEmpty(model.UserId))
                {
                    return BadRequest("Invalid request data.");
                }

                // Use the provided region parameter
                region = RegionConfiguration.NormalizeRegion(region);

                var consultant = await _userRepository.GetConsultantAsync(model.UserId, region);
                if (consultant == null)
                {
                    return NotFound($"Consultant with ID {model.UserId} not found.");
                }

                // Update education list
                consultant.Education = model.Education;

                // Save changes
                await _userRepository.UpdateConsultantAsync(consultant, region);

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError($"[ERROR] {ex.Message}");
                return BadRequest($"Error updating education: {ex.Message}");
            }
        }

        public class UpdateEducationModel
        {
            public string UserId { get; set; }
            public List<Education> Education { get; set; }
        }

        [HttpPost("{region}/update-job-experience")]
        public async Task<IActionResult> UpdateJobExperience(
            string region,
            [FromBody] UpdateJobExperienceModel model
        )
        {
            try
            {
                if (model == null || string.IsNullOrEmpty(model.UserId))
                {
                    return BadRequest("Invalid request data.");
                }

                // Use the provided region parameter
                region = RegionConfiguration.NormalizeRegion(region);

                var consultant = await _userRepository.GetConsultantAsync(model.UserId, region);
                if (consultant == null)
                {
                    return NotFound($"Consultant with ID {model.UserId} not found.");
                }

                // Update job experience list
                consultant.PreviousWorkPlaces = model.JobExperience;

                // Save changes
                await _userRepository.UpdateConsultantAsync(consultant, region);

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError($"[ERROR] {ex.Message}");
                return BadRequest($"Error updating job experience: {ex.Message}");
            }
        }

        public class UpdateJobExperienceModel
        {
            public string UserId { get; set; }
            public List<PreviousWorkPlace> JobExperience { get; set; }
        }

        [HttpPost("{region}/update-competencies")]
        public async Task<IActionResult> UpdateCompetencies(
            string region,
            [FromBody] CompetencyUpdateRequest request
        )
        {
            try
            {
                if (string.IsNullOrEmpty(request.UserId))
                {
                    return BadRequest("UserId is required");
                }

                // Use the provided region parameter
                region = RegionConfiguration.NormalizeRegion(region);

                var consultant = await _userRepository.GetConsultantAsync(request.UserId, region);
                if (consultant == null)
                {
                    return NotFound($"Consultant with ID {request.UserId} not found");
                }

                // Clear existing competencies
                consultant.Competencies = new List<CompetencyCategory>();

                // Add categories with their competencies
                foreach (var categoryUpdate in request.Competencies)
                {
                    var category = new CompetencyCategory
                    {
                        CategoryName = categoryUpdate.CategoryName,
                        CategoryLevel = categoryUpdate.CategoryLevel,
                        SubCategories = new List<SubCategory>(),
                    };

                    // Add subcategories
                    foreach (var subCategoryUpdate in categoryUpdate.SubCategories)
                    {
                        var subCategory = new SubCategory
                        {
                            SubCategoryName = subCategoryUpdate.SubCategoryName,
                            SubCategoryLevel = subCategoryUpdate.SubCategoryLevel,
                            Competencies = subCategoryUpdate
                                .Competencies.Select(c => new Competency
                                {
                                    CompetencyName = c.CompetencyName,
                                    CompetencyLevel = c.CompetencyLevel,
                                })
                                .ToList(),
                        };

                        category.SubCategories.Add(subCategory);
                    }

                    consultant.Competencies.Add(category);
                }

                await _userRepository.UpdateConsultantAsync(consultant, region);
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError($"[ERROR] {ex.Message}");
                return BadRequest($"Error updating competencies: {ex.Message}");
            }
        }

        public class CompetencyUpdateRequest
        {
            public string UserId { get; set; }
            public List<CompetencyUpdateModel> Competencies { get; set; }
        }

        public class CompetencyUpdateModel
        {
            public string CategoryName { get; set; }
            public int CategoryLevel { get; set; }
            public List<SubCategoryUpdateModel> SubCategories { get; set; }
        }

        public class SubCategoryUpdateModel
        {
            public string SubCategoryName { get; set; }
            public int SubCategoryLevel { get; set; }
            public List<CompetencyItemModel> Competencies { get; set; }
        }

        public class CompetencyItemModel
        {
            public string CompetencyName { get; set; }
            public int CompetencyLevel { get; set; }
        }

        [HttpGet("{region}/validate-token")]
        public IActionResult ValidateToken()
        {
            if (!ValidateSession())
            {
                return Unauthorized("Invalid session");
            }
            return Ok();
        }

        [HttpPost("{region}/update-references")]
        public async Task<IActionResult> UpdateReferences(
            string region,
            [FromBody] UpdateReferencesModel model
        )
        {
            try
            {
                if (model == null || string.IsNullOrEmpty(model.UserId))
                {
                    return BadRequest("Invalid request data.");
                }

                // Use the provided region parameter
                region = RegionConfiguration.NormalizeRegion(region);

                var consultant = await _userRepository.GetConsultantAsync(model.UserId, region);
                if (consultant == null)
                {
                    return NotFound($"Consultant with ID {model.UserId} not found.");
                }

                // Update references list
                consultant.References = model.References;

                // Save changes
                await _userRepository.UpdateConsultantAsync(consultant, region);

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError($"[ERROR] {ex.Message}");
                return BadRequest($"Error updating references: {ex.Message}");
            }
        }

        [HttpPost("{region}/upload-cv")]
        public async Task<IActionResult> UploadCv([FromRoute] string region, IFormFile cv)
        {
            try
            {
                Console.WriteLine(
                    $"Received CV upload request. File name: {cv?.FileName}, Size: {cv?.Length} bytes"
                );

                if (cv == null || cv.Length == 0)
                {
                    return BadRequest(new { error = "No file selected" });
                }

                // Using the SharePointUploader with default folder (CV folder)
                var sharePointUrl = await _sharePointUploader.UploadToSharePoint(cv);

                if (string.IsNullOrEmpty(sharePointUrl))
                {
                    return BadRequest(new { error = "Failed to get URL from SharePoint" });
                }

                return Ok(
                    new
                    {
                        success = true,
                        url = sharePointUrl,
                        fileName = cv.FileName,
                    }
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in CV upload: {ex.Message}");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("{region}/upload-profile-picture")]
        public async Task<IActionResult> UploadProfilePicture(
            [FromRoute] string region,
            [FromForm(Name = "profilePicture")] IFormFile file
        )
        {
            _logger.LogInformation($"UploadProfilePicture endpoint hit. Region: {region}");
            try
            {
                if (file == null || file.Length == 0)
                {
                    return BadRequest(new { error = "No file selected" });
                }

                // Validate file type
                if (!file.ContentType.StartsWith("image/"))
                {
                    return BadRequest(new { error = "Only image files are allowed" });
                }

                // Use the dedicated method for profile pictures, just like CV uploads
                var sharePointUrl = await _sharePointUploader.UploadProfilePicture(file);

                if (string.IsNullOrEmpty(sharePointUrl))
                {
                    return BadRequest(new { error = "Failed to get URL from SharePoint" });
                }

                // Return the URL directly, like we do with CV uploads
                return Ok(new
                {
                    success = true,
                    url = sharePointUrl,
                    fileName = file.FileName
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error uploading profile picture: {ex.Message}");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("{region}/{userId}/profile-picture")]
        public async Task<IActionResult> GetProfilePicture(string region, string userId)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                {
                    return BadRequest(new { error = "User ID is required" });
                }

                _logger.LogInformation($"Getting profile picture for user: {userId} in region: {region}");

                // Try to get as consultant first, then as applicant
                var consultant = await _userRepository.GetConsultantAsync(userId, region);
                if (consultant != null)
                {
                    _logger.LogInformation($"Found consultant. ProfilePicture: '{consultant.ProfilePicture}'");
                    if (!string.IsNullOrEmpty(consultant.ProfilePicture))
                    {
                        return Ok(new { profilePictureUrl = consultant.ProfilePicture });
                    }
                    else
                    {
                        return Ok(new { profilePictureUrl = (string?)null, message = "No profile picture found" });
                    }
                }

                var applicant = await _userRepository.GetApplicantAsync(userId, region);
                if (applicant != null)
                {
                    _logger.LogInformation($"Found applicant. ProfilePicture: '{applicant.ProfilePicture}'");
                    if (!string.IsNullOrEmpty(applicant.ProfilePicture))
                    {
                        return Ok(new { profilePictureUrl = applicant.ProfilePicture });
                    }
                    else
                    {
                        return Ok(new { profilePictureUrl = (string?)null, message = "No profile picture found" });
                    }
                }

                return NotFound(new { error = "User not found" });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error retrieving profile picture for user {userId}: {ex.Message}");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        public class SoftDeleteRequest
        {
            public string? UserId { get; set; }
        }

        [HttpPost("{region}/soft-delete")]
        public async Task<IActionResult> SoftDelete(string region, [FromBody] SoftDeleteRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.UserId))
                return BadRequest("UserId is required.");

            var consultant = await _userRepository.GetConsultantAsync(request.UserId, region);
            if (consultant != null)
            {
                consultant.IsDeleted = true;
                consultant.DateAdded = DateTime.UtcNow;
                await _userRepository.UpdateConsultantAsync(consultant, region);
                return Ok(new { success = true });
            }

            var applicant = await _userRepository.GetApplicantAsync(request.UserId, region);
            if (applicant != null)
            {
                applicant.IsDeleted = true;
                applicant.DateAdded = DateTime.UtcNow;
                await _userRepository.UpdateApplicantAsync(applicant, region);
                return Ok(new { success = true });
            }

            return NotFound($"User with ID {request.UserId} not found.");
        }

        [HttpPost("{region}/delete")]
        public async Task<IActionResult> DeleteUser(
            string region,
            [FromBody] SoftDeleteRequest request
        )
        {
            if (request == null || string.IsNullOrEmpty(request.UserId))
                return BadRequest("UserId is required.");

            var consultant = await _userRepository.GetConsultantAsync(request.UserId, region);
            var applicant = await _userRepository.GetApplicantAsync(request.UserId, region);
            if (consultant == null && applicant == null)
                return NotFound($"User with ID {request.UserId} not found.");

            // Actually delete from CosmosDB
            if (consultant != null)
                await _userRepository.DeleteUserAsync(request.UserId, region);
            else if (applicant != null)
                await _userRepository.DeleteApplicantAsync(request.UserId, region);

            return Ok(new { success = true });
        }

        [HttpPost("{region}/restore")]
        public async Task<IActionResult> RestoreUser(
            string region,
            [FromBody] SoftDeleteRequest request
        )
        {
            if (request == null || string.IsNullOrEmpty(request.UserId))
                return BadRequest("UserId is required.");

            var consultant = await _userRepository.GetConsultantAsync(request.UserId, region);
            var applicant = await _userRepository.GetApplicantAsync(request.UserId, region);
            if (consultant == null && applicant == null)
                return NotFound($"User with ID {request.UserId} not found.");

            if (consultant != null)
            {
                consultant.IsDeleted = false;
                await _userRepository.UpdateConsultantAsync(consultant, region);
            }
            else if (applicant != null)
            {
                applicant.IsDeleted = false;
                await _userRepository.UpdateApplicantAsync(applicant, region);
            }

            return Ok(new { success = true });
        }

        [HttpPost("{region}/update-header")]
        public async Task<IActionResult> UpdateHeader(
            string region,
            [FromBody] UpdateHeaderModel model
        )
        {
            try
            {
                if (model == null || string.IsNullOrEmpty(model.UserId))
                {
                    return BadRequest("Invalid request data.");
                }

                region = RegionConfiguration.NormalizeRegion(region);
                var consultant = await _userRepository.GetConsultantAsync(model.UserId, region);
                if (consultant == null)
                {
                    return NotFound($"Consultant with ID {model.UserId} not found.");
                }

                // Update header info
                if (!string.IsNullOrEmpty(model.FirstName))
                    consultant.FirstName = model.FirstName;
                if (!string.IsNullOrEmpty(model.LastName))
                    consultant.LastName = model.LastName;
                if (model.Summary != null)
                    consultant.Summary = model.Summary;
                if (model.UserRole.HasValue)
                {
                    // Only allow Applicant (0) or Consultant (1) roles
                    if (model.UserRole.Value == 0 || model.UserRole.Value == 1)
                    {
                        consultant.UserRole = (Role)model.UserRole.Value;
                    }
                }
                
                // Handle availability updates
                if (model.IsAvailable.HasValue)
                    consultant.IsAvailable = model.IsAvailable.Value;
                if (model.AvailableInterval.HasValue)
                    consultant.AvailableInterval = model.AvailableInterval.Value;
                if (!string.IsNullOrEmpty(model.AvailableWhen))
                {
                    if (DateTime.TryParse(model.AvailableWhen, out DateTime availableDate))
                        consultant.AvailableWhen = availableDate;
                }
                
                // Handle BEPA and Confirmed status updates
                if (model.BepaStatus.HasValue)
                    consultant.Bepa = model.BepaStatus.Value;
                if (model.ConfirmedStatus.HasValue)
                    consultant.Confirmed = model.ConfirmedStatus.Value;
                
                // Handle profile picture update
                if (!string.IsNullOrEmpty(model.ProfilePictureUrl))
                    consultant.ProfilePicture = model.ProfilePictureUrl;

                await _userRepository.UpdateConsultantAsync(consultant, region);
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError($"[ERROR] {ex.Message}");
                return BadRequest($"Error updating header info: {ex.Message}");
            }
        }

        public class UpdateHeaderModel
        {
            public string? UserId { get; set; }
            public string? FirstName { get; set; }
            public string? LastName { get; set; }
            public string? Summary { get; set; }
            public int? UserRole { get; set; }
            public bool? IsAvailable { get; set; }
            public int? AvailableInterval { get; set; }
            public string? AvailableWhen { get; set; }
            public bool? BepaStatus { get; set; }
            public bool? ConfirmedStatus { get; set; }
            public string? ProfilePictureUrl { get; set; }
        }

        [HttpPost("{region}/update-contact")]
        public async Task<IActionResult> UpdateContact(
            string region,
            [FromBody] UpdateContactModel model
        )
        {
            try
            {
                if (model == null || string.IsNullOrEmpty(model.UserId))
                {
                    return BadRequest("Invalid request data.");
                }

                region = RegionConfiguration.NormalizeRegion(region);
                var consultant = await _userRepository.GetConsultantAsync(model.UserId, region);
                if (consultant == null)
                {
                    return NotFound($"Consultant with ID {model.UserId} not found.");
                }

                // Update contact info
                consultant.Email = model.Email;
                consultant.Phone = model.Phone;
                consultant.Address = model.Address;
                consultant.City = model.City;
                consultant.PostalCode = model.PostalCode;
                consultant.Country = model.Country;
                consultant.Linkedin = model.Linkedin;
                consultant.CV = model.CV;

                await _userRepository.UpdateConsultantAsync(consultant, region);
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError($"[ERROR] {ex.Message}");
                return BadRequest($"Error updating contact info: {ex.Message}");
            }
        }

        public class UpdateContactModel
        {
            public string UserId { get; set; }
            public string Email { get; set; }
            public string Phone { get; set; }
            public string Address { get; set; }
            public string City { get; set; }
            public int PostalCode { get; set; }
            public string Country { get; set; }
            public string Linkedin { get; set; }
            public string CV { get; set; }
        }

        public class UpdateReferencesModel
        {
            public string UserId { get; set; }
            public List<References> References { get; set; }
        }

        public class SaveNoteRequest
        {
            public string UserId { get; set; }
            public string Note { get; set; }
        }

        public class PasswordUpdateRequest
        {
            public string UserId { get; set; }
            public string CurrentPassword { get; set; }
            public string NewPassword { get; set; }
        }

        [HttpPost("{region}/update-additional-notes")]
        public async Task<IActionResult> UpdateAdditionalNotes(
            string region,
            [FromBody] UpdateAdditionalNotesModel model
        )
        {
            try
            {
                if (model == null || string.IsNullOrEmpty(model.UserId))
                {
                    return BadRequest("Invalid request data.");
                }

                // Use the provided region parameter
                region = RegionConfiguration.NormalizeRegion(region);

                var consultant = await _userRepository.GetConsultantAsync(model.UserId, region);
                if (consultant == null)
                {
                    return NotFound($"Consultant with ID {model.UserId} not found.");
                }

                // Update additional notes list
                consultant.AdditionalNotes = model.AdditionalNotes ?? new List<string>();

                // Save changes
                await _userRepository.UpdateConsultantAsync(consultant, region);

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError($"[ERROR] {ex.Message}");
                return BadRequest($"Error updating additional notes: {ex.Message}");
            }
        }

        public class UpdateAdditionalNotesModel
        {
            public string UserId { get; set; }
            public List<string> AdditionalNotes { get; set; }
        }

        [HttpPost("{region}/update-interests")]
        public async Task<IActionResult> UpdateInterests(
            string region,
            [FromBody] UpdateInterestsModel model
        )
        {
            try
            {
                if (model == null || string.IsNullOrEmpty(model.UserId))
                {
                    return BadRequest("Invalid request data.");
                }

                // Use the provided region parameter
                region = RegionConfiguration.NormalizeRegion(region);

                var consultant = await _userRepository.GetConsultantAsync(model.UserId, region);
                if (consultant == null)
                {
                    return NotFound($"Consultant with ID {model.UserId} not found.");
                }

                // Update interests
                consultant.Interests = model.Interests;

                // Save changes
                await _userRepository.UpdateConsultantAsync(consultant, region);

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError($"[ERROR] {ex.Message}");
                return BadRequest($"Error updating interests: {ex.Message}");
            }
        }

        [HttpPost("set-region")]
        public IActionResult SetRegion([FromBody] SetRegionModel model)
        {
            if (string.IsNullOrEmpty(model.Region) || (model.Region != "VN" && model.Region != "DK"))
            {
                return BadRequest("Invalid region. Must be 'VN' or 'DK'.");
            }

            try
            {
                HttpContext.Session.SetString("UserRegion", model.Region);
                _logger.LogInformation($"Region set to: {model.Region}");
                
                // Also check if we need to set minimal session info if missing
                var userId = HttpContext.Session.GetString("UserId");
                if (string.IsNullOrEmpty(userId))
                {
                    // Create a temporary session for form submission
                    HttpContext.Session.SetString("UserId", "temp-" + Guid.NewGuid().ToString());
                    HttpContext.Session.SetString("UserRole", "2"); // Consultant role
                    _logger.LogInformation("Created temporary session for region setting");
                }
                
                return Ok(new { message = "Region set successfully", region = model.Region });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error setting region: {ex.Message}");
                return BadRequest("Failed to set region.");
            }
        }

        public class SetRegionModel
        {
            public string Region { get; set; }
        }

        public class UpdateInterestsModel
        {
            public string UserId { get; set; }
            public string Interests { get; set; }
        }
    }
}
