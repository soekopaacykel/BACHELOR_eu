using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using CVAPI.Models;
using CVAPI.Repos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace CVAPI.Pages
{
    [IgnoreAntiforgeryToken]
    // Note: Authentication now handled client-side via JavaScript
    // [Authorize] removed to prevent 401 on page navigation
    public class PersonModel : PageModel
    {
        private readonly UserRepository _userRepository;
        private readonly HttpClient _httpClient;
        private readonly ExperienceRepository _experienceRepository;
        private readonly CompetenciesRepository _competenciesRepository;

        public PersonModel(
            UserRepository userRepository,
            HttpClient httpClient,
            ExperienceRepository experienceRepository,
            CompetenciesRepository competenciesRepository
        )
        {
            _userRepository = userRepository;
            _httpClient = httpClient; // Inject HttpClient for API calls
            _experienceRepository = experienceRepository; // Inject ExperienceRepository
            _competenciesRepository = competenciesRepository; // Inject CompetenciesRepository
        }

        public Consultant? Consultant { get; set; }
        public string? UserRole { get; set; }
        public string? Region { get; set; } // Store the current region
        public List<CompetencyCategory>? Competencies { get; set; } // Kompetencerne til visning
        public List<Language>? Languages { get; set; } // Sprog til visning
        public string? AdminInitials { get; set; } // Store initials from JWT
        public List<PrivateNote>? PrivateNotes { get; set; } // Add PrivateNotes property
        public List<string>? Degrees { get; set; } // List to hold degrees
        public List<string>? Fields { get; set; } // List to hold fields
        public List<CompetencyCategory>? Categories { get; set; } // Add Categories property
        public List<string>? AvailableLanguages { get; set; } // Add AvailableLanguages property

        // Property to expose the logged-in user's email
        public string UserEmail => User?.Identity?.IsAuthenticated == true ? (User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? User.Identity?.Name ?? string.Empty) : string.Empty;

        public async Task<IActionResult> OnGetAsync(string region, string UserId)
        {
            // Store the region for use in the view
            Region = region;
            
            // Hent brugeren som User, og forsøg derefter at caste til Consultant
            var user = await _userRepository.GetConsultantAsync(UserId, region);

            if (user == null)
            {
                return RedirectToPage("/Consultants");
            }

            Consultant = user as Consultant;

            if (Consultant == null)
            {
                return RedirectToPage("/Consultants");
            }

            UserRole = Consultant.UserRole.ToString(); // Hent UserRole som en string

            // Hent kompetencerne for denne bruger (her bruges allerede dit repository)
            Competencies = Consultant.Competencies; // Vi bruger den eksisterende list fra Consultant

            // Hent sprogene for denne bruger (her antager vi, at Consultant har en Languages property)
            Languages = Consultant.Languages; // Hvis Languages er en property af Consultant

            // Extract initials from JWT and assign to AdminInitials
            var userClaims = User.Identity as ClaimsIdentity;
            AdminInitials = userClaims?.FindFirst("initials")?.Value;

            PrivateNotes = Consultant.PrivateNotes; // Populate PrivateNotes for Consultant

            // Fetch degrees and fields from ExperienceRepository
            Degrees = await _experienceRepository.GetDegreesAsync(region);
            Fields = await _experienceRepository.GetFieldsAsync(region);

            // Fetch predefined competency categories
            var predefinedData = await _competenciesRepository.GetPredefinedDataAsync(region);
            Categories = predefinedData?.Competencies ?? new List<CompetencyCategory>();

            // Fetch predefined languages
            AvailableLanguages =
                predefinedData?.Languages?.Select(l => l.LanguageName).ToList()
                ?? new List<string>();

            return Page();
        }

        public async Task<IActionResult> OnPostSaveNoteAsync(string userId, string note)
        {
            try
            {
                // If userId is not provided in the form, get it from the route data
                if (string.IsNullOrEmpty(userId))
                {
                    userId = RouteData.Values["UserId"]?.ToString();

                    if (string.IsNullOrEmpty(userId))
                    {
                        Console.WriteLine(
                            "[ERROR] UserId is missing in both form data and route data."
                        );
                        ModelState.AddModelError(string.Empty, "UserId is required");
                        return Page();
                    }

                    Console.WriteLine($"[DEBUG] Retrieved UserId from route: {userId}");
                }

                // Use the region from the route
                var region = RouteData.Values["Region"]?.ToString();
                if (string.IsNullOrEmpty(region))
                {
                    return BadRequest("Region is required in the route.");
                }

                // Extract the JWT token from the Authorization header
                var jwtToken = HttpContext
                    .Request.Headers["Authorization"]
                    .ToString()
                    .Replace("Bearer ", "");

                // Ensure Authorization header is present
                if (string.IsNullOrEmpty(jwtToken))
                {
                    Console.WriteLine("[ERROR] Authorization token is missing from header.");
                    throw new UnauthorizedAccessException("Authorization token is missing from header.");
                }

                Console.WriteLine($"[DEBUG] Retrieved JWT Token: {jwtToken}");

                // Save the private note
                await _userRepository.SavePrivateNoteAsync(userId, note, jwtToken, region);

                // Reload the page with updated data
                return RedirectToPage(new { region, UserId = userId });
            }
            catch (Exception ex)
            {
                // Log the error and return an error message
                Console.WriteLine($"[ERROR] {ex.Message}");
                ModelState.AddModelError(string.Empty, $"Error saving note: {ex.Message}");
                return Page();
            }
        }

        public async Task<IActionResult> OnPostUpdateUserRoleAsync(string userId)
        {
            try
            {
                // Update the user role to Consultant
                await _userRepository.UpdateUserRoleAsync(userId, (int)Role.Consultant);

                // Redirect back to the same page with updated data
                if (Consultant == null)
                {
                    return RedirectToPage("/Consultants");
                }
                
                return RedirectToPage(
                    new { Region = Consultant.Country, UserId = Consultant.UserId }
                );
            }
            catch (Exception ex)
            {
                // Log the error and return an error message
                ModelState.AddModelError(string.Empty, $"Error updating user role: {ex.Message}");
                return Page();
            }
        }

        public async Task<IActionResult> OnPostUpdateRatingAsync(string userId, int rating)
        {
            try
            {
                // If userId is not provided in the form, get it from the route data
                if (string.IsNullOrEmpty(userId))
                {
                    userId = RouteData.Values["UserId"]?.ToString();

                    if (string.IsNullOrEmpty(userId))
                    {
                        Console.WriteLine(
                            "[ERROR] UserId is missing in both form data and route data."
                        );
                        return BadRequest("UserId is required");
                    }
                }

                // Set region from route data or fallback to "DK"
                var region = RouteData.Values["Region"]?.ToString();
                if (string.IsNullOrEmpty(region))
                {
                    return BadRequest("Region is required in the route.");
                }

                // Get the consultant
                var consultant = await _userRepository.GetConsultantAsync(userId, region);

                if (consultant == null)
                {
                    return NotFound($"Consultant with ID {userId} not found.");
                }

                // Update the rating
                consultant.PrivateRating = rating;

                // Save the updated consultant
                await _userRepository.UpdateConsultantAsync(consultant, region);

                return RedirectToPage(new { region, UserId = userId });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] {ex.Message}");
                return BadRequest($"Error updating rating: {ex.Message}");
            }
        }

        public async Task<IActionResult> OnPostUpdateTechRatingAsync(string userId, int rating)
        {
            try
            {
                // If userId is not provided in the form, get it from the route data
                if (string.IsNullOrEmpty(userId))
                {
                    userId = RouteData.Values["UserId"]?.ToString() ?? string.Empty;
                    if (string.IsNullOrEmpty(userId))
                    {
                        Console.WriteLine("[ERROR] UserId is missing in both form data and route data.");
                        return BadRequest("UserId is required");
                    }
                }
                // Use the region from the route
                var region = RouteData.Values["Region"]?.ToString();
                // Get the consultant
                var consultant = await _userRepository.GetConsultantAsync(userId, region);
                if (consultant == null)
                {
                    return NotFound($"Consultant with ID {userId} not found.");
                }
                // Update the tech rating
                // If TechRating is not present or is negative (legacy DB), initialize it
                if (consultant.TechRating < 0)
                {
                    consultant.TechRating = 0;
                }
                consultant.TechRating = rating;
                // Save the updated consultant
                await _userRepository.UpdateConsultantAsync(consultant, region);
                return RedirectToPage(new { region, UserId = userId });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] {ex.Message}");
                return BadRequest($"Error updating tech rating: {ex.Message}");
            }
        }

        public async Task<IActionResult> OnPostUpdateHeaderAsync(
            string userId,
            string FirstName,
            string LastName,
            string Summary
        )
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                {
                    userId = RouteData.Values["UserId"]?.ToString();
                    if (string.IsNullOrEmpty(userId))
                    {
                        Console.WriteLine("[ERROR] UserId is missing in both form data and route data.");
                        return BadRequest("UserId is required");
                    }
                }

                var region = RouteData.Values["Region"]?.ToString();
                var consultant = await _userRepository.GetConsultantAsync(userId, region);
                
                if (consultant == null)
                {
                    return NotFound($"Consultant with ID {userId} not found.");
                }

                // Update the consultant properties
                consultant.FirstName = FirstName;
                consultant.LastName = LastName;
                consultant.Summary = Summary;

                // Save the updated consultant
                await _userRepository.UpdateConsultantAsync(consultant, region);

                // Redirect back to the page with updated data
                return RedirectToPage(new { region, UserId = userId });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] {ex.Message}");
                ModelState.AddModelError(string.Empty, $"Error updating consultant header info: {ex.Message}");
                return Page();
            }
        }

        public async Task<IActionResult> OnPostUpdateConsultantInfoAsync(
            string userId,
            string region,
            string Email,
            string Phone,
            string Address,
            string Linkedin
        )
        {
            try
            {
                // If userId is not provided in the form, get it from the route data
                if (string.IsNullOrEmpty(userId))
                {
                    userId = RouteData.Values["UserId"]?.ToString();

                    if (string.IsNullOrEmpty(userId))
                    {
                        Console.WriteLine(
                            "[ERROR] UserId is missing in both form data and route data."
                        );
                        return BadRequest("UserId is required");
                    }
                }

                // Use the region from the route
                region = RouteData.Values["Region"]?.ToString();

                // Get the consultant
                var consultant = await _userRepository.GetConsultantAsync(userId, region);

                if (consultant == null)
                {
                    return NotFound($"Consultant with ID {userId} not found.");
                }

                // Update the consultant properties
                consultant.Email = Email;
                consultant.Phone = Phone;
                consultant.Address = Address;
                consultant.Linkedin = Linkedin;

                // Save the updated consultant
                await _userRepository.UpdateConsultantAsync(consultant, region);

                // Redirect back to the page with updated data
                return RedirectToPage(new { region, UserId = userId });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] {ex.Message}");
                ModelState.AddModelError(
                    string.Empty,
                    $"Error updating consultant info: {ex.Message}"
                );
                return Page();
            }
        }

        public async Task<IActionResult> OnPostUpdateEducationAsync(
            string userId,
            string region,
            Education updatedEducation
        )
        {
            try
            {
                // Log the incoming data for debugging
                Console.WriteLine($"[DEBUG] UserId: {userId}");
                Console.WriteLine($"[DEBUG] Region: {region}");
                Console.WriteLine(
                    $"[DEBUG] Updated Education Data: {JsonSerializer.Serialize(updatedEducation)}"
                );

                // Validate userId
                if (string.IsNullOrEmpty(userId))
                {
                    Console.WriteLine("[ERROR] UserId is missing.");
                    return BadRequest("UserId is required.");
                }

                // Use the region from the route
                region = RouteData.Values["Region"]?.ToString();

                // Fetch the consultant
                var consultant = await _userRepository.GetConsultantAsync(userId, region);
                if (consultant == null)
                {
                    Console.WriteLine($"[ERROR] Consultant with ID {userId} not found.");
                    return NotFound($"Consultant with ID {userId} not found.");
                }

                // Find the specific education item to update
                var educationToUpdate = consultant.Education.FirstOrDefault(e =>
                    e.Id == updatedEducation.Id
                );
                if (educationToUpdate == null)
                {
                    Console.WriteLine("[ERROR] Education item not found.");
                    return NotFound("Education item not found.");
                }

                // Update the education item
                educationToUpdate.Degree = updatedEducation.Degree;
                educationToUpdate.Field = updatedEducation.Field;
                educationToUpdate.Institution = updatedEducation.Institution;
                educationToUpdate.StartDate = updatedEducation.StartDate;
                educationToUpdate.EndDate = updatedEducation.EndDate;

                // Save the updated consultant
                await _userRepository.UpdateConsultantAsync(consultant, region);

                Console.WriteLine("[INFO] Education updated successfully.");
                return RedirectToPage(new { region, UserId = userId });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] {ex.Message}");
                ModelState.AddModelError(string.Empty, $"Error updating education: {ex.Message}");
                return Page();
            }
        }

        public async Task<IActionResult> OnPostUpdateJobExperienceAsync(
            string userId,
            string region,
            PreviousWorkPlace updatedJob
        )
        {
            try
            {
                // Log the incoming data for debugging
                Console.WriteLine($"[DEBUG] UserId: {userId}");
                Console.WriteLine($"[DEBUG] Region: {region}");
                Console.WriteLine(
                    $"[DEBUG] Updated Job Data: {JsonSerializer.Serialize(updatedJob)}"
                );

                // Validate userId
                if (string.IsNullOrEmpty(userId))
                {
                    Console.WriteLine("[ERROR] UserId is missing.");
                    return BadRequest("UserId is required.");
                }

                // Use the region from the route
                region = RouteData.Values["Region"]?.ToString();

                // Fetch the consultant
                var consultant = await _userRepository.GetConsultantAsync(userId, region);
                if (consultant == null)
                {
                    Console.WriteLine($"[ERROR] Consultant with ID {userId} not found.");
                    return NotFound($"Consultant with ID {userId} not found.");
                }

                // Find the specific job experience item to update
                var jobToUpdate = consultant.PreviousWorkPlaces.FirstOrDefault(j =>
                    j.Id == updatedJob.Id
                );
                if (jobToUpdate == null)
                {
                    Console.WriteLine("[ERROR] Job experience item not found.");
                    return NotFound("Job experience item not found.");
                }

                // Update the job experience item
                jobToUpdate.Position = updatedJob.Position;
                jobToUpdate.Company = updatedJob.Company;
                jobToUpdate.StartDate = updatedJob.StartDate;
                jobToUpdate.EndDate = updatedJob.EndDate;
                jobToUpdate.Description = updatedJob.Description;

                // Save the updated consultant
                await _userRepository.UpdateConsultantAsync(consultant, region);

                Console.WriteLine("[INFO] Job experience updated successfully.");
                return RedirectToPage(new { region, UserId = userId });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] {ex.Message}");
                ModelState.AddModelError(
                    string.Empty,
                    $"Error updating job experience: {ex.Message}"
                );
                return Page();
            }
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

        public class CompetencyUpdateRequest
        {
            public string UserId { get; set; }
            public string Region { get; set; }
            public List<CompetencyUpdateModel> Competencies { get; set; }
        }

        public async Task<IActionResult> OnPostUpdateCompetenciesAsync(
            [FromBody] CompetencyUpdateRequest request
        )
        {
            try
            {
                if (string.IsNullOrEmpty(request.UserId))
                {
                    return BadRequest("UserId is required.");
                }

                var region = string.IsNullOrEmpty(request.Region) 
                    ? RouteData.Values["Region"]?.ToString()
                    : request.Region;
                var consultant = await _userRepository.GetConsultantAsync(request.UserId, region);

                if (consultant == null)
                {
                    return NotFound($"Consultant with ID {request.UserId} not found.");
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
                return new JsonResult(new { success = true });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] {ex.Message}");
                return BadRequest($"Error updating competencies: {ex.Message}");
            }
        }

        public class UpdateReferencesModel
        {
            public string UserId { get; set; }
            public List<References> References { get; set; }
        }

        public async Task<IActionResult> OnPostUpdateReferencesAsync(
            string userId,
            List<References> references
        )
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                {
                    userId = RouteData.Values["UserId"]?.ToString();
                    if (string.IsNullOrEmpty(userId))
                    {
                        return BadRequest("UserId is required.");
                    }
                }

                var region = RouteData.Values["Region"]?.ToString();
                var consultant = await _userRepository.GetConsultantAsync(userId, region);
                if (consultant == null)
                {
                    return NotFound($"Consultant with ID {userId} not found.");
                }

                // Initialize References list if null
                if (consultant.References == null)
                {
                    consultant.References = new List<References>();
                }

                // Update references with new values, preserving Id
                consultant.References = references
                    .Where(r => !string.IsNullOrEmpty(r.Name))
                    .Select(r => new References
                    {
                        Id = r.Id,
                        Name = r.Name,
                        Company = r.Company ?? "",
                        Phone = r.Phone ?? "",
                        Email = r.Email ?? "",
                    })
                    .ToList();

                // Save the updated consultant
                await _userRepository.UpdateConsultantAsync(consultant, region);

                return RedirectToPage(new { region = region, UserId = userId });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to update references: {ex.Message}");
                Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
                var region = RouteData.Values["Region"]?.ToString();
                await OnGetAsync(region, userId);
                ModelState.AddModelError(string.Empty, "Failed to update references");
                return Page();
            }
        }

        public async Task<IActionResult> OnPostUpdateAdditionalNotesAsync(
            string userId,
            List<string> additionalNotes
        )
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                {
                    userId = RouteData.Values["UserId"]?.ToString();
                    if (string.IsNullOrEmpty(userId))
                    {
                        return BadRequest("UserId is required.");
                    }
                }

                var region = RouteData.Values["Region"]?.ToString();
                var consultant = await _userRepository.GetConsultantAsync(userId, region);
                if (consultant == null)
                {
                    return NotFound($"Consultant with ID {userId} not found.");
                }

                // Initialize AdditionalNotes list if null
                if (consultant.AdditionalNotes == null)
                {
                    consultant.AdditionalNotes = new List<string>();
                }

                // Update additional notes with new values, filtering out empty notes
                consultant.AdditionalNotes = additionalNotes
                    .Where(note => !string.IsNullOrWhiteSpace(note))
                    .ToList();

                // Save the updated consultant
                await _userRepository.UpdateConsultantAsync(consultant, region);

                return RedirectToPage(new { region = region, UserId = userId });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to update additional notes: {ex.Message}");
                Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
                var region = RouteData.Values["Region"]?.ToString();
                await OnGetAsync(region, userId);
                ModelState.AddModelError(string.Empty, "Failed to update additional notes");
                return Page();
            }
        }

        public async Task<IActionResult> OnPostUpdateInterestsAsync(string userId, string interests)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                {
                    userId = RouteData.Values["UserId"]?.ToString();
                    if (string.IsNullOrEmpty(userId))
                    {
                        return BadRequest("UserId is required.");
                    }
                }

                var region = RouteData.Values["Region"]?.ToString();
                var consultant = await _userRepository.GetConsultantAsync(userId, region);
                if (consultant == null)
                {
                    return NotFound($"Consultant with ID {userId} not found.");
                }

                // Update interests
                consultant.Interests = interests;

                // Save the updated consultant
                await _userRepository.UpdateConsultantAsync(consultant, region);

                return RedirectToPage(new { region = region, UserId = userId });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to update interests: {ex.Message}");
                Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
                var region = RouteData.Values["Region"]?.ToString();
                await OnGetAsync(region, userId);
                ModelState.AddModelError(string.Empty, "Failed to update interests");
                return Page();
            }
        }

        [BindProperty]
        public string? UserId { get; set; }

        public async Task<IActionResult> OnPostSoftDeleteProfileAsync()
        {
            Console.WriteLine($"[DEBUG] OnPostSoftDeleteProfileAsync called. UserId: {UserId}");
            if (string.IsNullOrEmpty(UserId))
            {
                return BadRequest("UserId is required.");
            }

            var region = RouteData.Values["Region"]?.ToString();
            
            // Fetch the consultant
            var consultant = await _userRepository.GetConsultantAsync(UserId, region);
            if (consultant == null)
            {
                return NotFound($"Consultant with ID {UserId} not found.");
            }

            // Mark as deleted (soft delete)
            consultant.IsDeleted = true;
            consultant.DateAdded = DateTime.UtcNow; // Use DateAdded as deletion date
            await _userRepository.UpdateConsultantAsync(consultant, region);

            // Return JSON so JS can handle redirect and preserve auth
            return new JsonResult(new { success = true });
        }
    }
}
