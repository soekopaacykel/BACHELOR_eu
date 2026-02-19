using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CVAPI.Models;
using CVAPI.Repos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CVAPI.Pages
{
    // Note: Authentication now handled client-side via JavaScript
    // [Authorize] removed to prevent 401 on page navigation
    public class ApplicantsModel : PageModel
    {
        private readonly UserRepository _userRepository;

        // List of Applicants to display
        public List<Applicant> Applicants { get; set; } = new List<Applicant>();

        // Filter/search properties
        public string? SearchQuery { get; set; }
        public bool? IsAvailableFilter { get; set; }
        public string RegionFilter { get; set; } = "DK"; // Default region is DK

        // Helper method to get region with authorization check
        private string GetEffectiveRegion(string? urlRegion = null)
        {
            // Get user's authorized region from session
            var userRegion = HttpContext.Session.GetString("UserRegion");
            
            // If no region in session, user is not properly authenticated
            if (string.IsNullOrEmpty(userRegion))
            {
                throw new UnauthorizedAccessException("User region not found in session. Please log in again.");
            }
            
            // If URL region is specified, validate that user has access to it
            if (!string.IsNullOrEmpty(urlRegion))
            {
                // User can only access their own region
                if (urlRegion != userRegion)
                {
                    throw new UnauthorizedAccessException($"Access denied. User from {userRegion} region cannot access {urlRegion} region data.");
                }
                return urlRegion;
            }
            
            // Return user's authorized region
            return userRegion;
        }
        public List<string> AllCompetencies { get; set; } = new List<string>();
        public List<string> AllApplicants { get; set; } = new List<string>();

        // Add properties for private note and user role update
        public string? PrivateNote { get; set; }
        public string? AdminInitials { get; set; }

        // Property to expose the logged-in user's email
        public string UserEmail => User?.Identity?.IsAuthenticated == true ? (User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? User.Identity?.Name ?? string.Empty) : string.Empty;

        // Constructor to inject UserRepository
        public ApplicantsModel(UserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        // OnGetAsync is an asynchronous method to load the page data
        public async Task<IActionResult> OnGetAsync(string? region)
        {
            try
            {
                // Use helper method to determine effective region with authorization check
                RegionFilter = GetEffectiveRegion(region);

                // Get applicants from repository based on authorized region
                var applicantsRaw = await _userRepository.GetAllApplicants(RegionFilter) ?? new List<Applicant>();
                Applicants = applicantsRaw.Where(a => !a.IsDeleted).ToList();
            }
            catch (UnauthorizedAccessException ex)
            {
                // Log the unauthorized access attempt
                Console.WriteLine($"[SECURITY] Unauthorized region access attempt: {ex.Message}");
                
                // Set error message for display
                TempData["ErrorMessage"] = "Access Denied: You do not have permission to view this region's data.";
                TempData["ErrorDetails"] = ex.Message;
                
                // Redirect to an error page or back to their authorized region
                var userRegion = HttpContext.Session.GetString("UserRegion");
                if (!string.IsNullOrEmpty(userRegion))
                {
                    return RedirectToPage("/Applicants", new { region = userRegion });
                }
                return RedirectToPage("/Index");
            }
            catch (Exception ex)
            {
                // Log any other errors
                Console.WriteLine($"[ERROR] Error loading applicants: {ex.Message}");
                
                // Return to login or show error
                return RedirectToPage("/Index");
            }

            foreach (var applicant in Applicants)
            {
                Console.WriteLine($"Applicant: {applicant.FirstName} {applicant.LastName}");
                Console.WriteLine($"Competencies count: {applicant.Competencies?.Count ?? 0}");

                if (applicant.Competencies != null)
                {
                    foreach (var category in applicant.Competencies)
                    {
                        Console.WriteLine(
                            $"Category: {category.CategoryName}, Level: {category.CategoryLevel}"
                        );
                        foreach (var sub in category.SubCategories)
                        {
                            Console.WriteLine(
                                $"  Subcategory: {sub.SubCategoryName}, Level: {sub.SubCategoryLevel}"
                            );
                            foreach (var comp in sub.Competencies)
                            {
                                Console.WriteLine(
                                    $"    Competency: {comp.CompetencyName}, Level: {comp.CompetencyLevel}"
                                );
                            }
                        }
                    }
                }
            }

            // Get all unique competencies (including categories, subcategories, and individual competencies)
            AllCompetencies = (Applicants ?? new List<Applicant>())
                .Where(c => c.Competencies != null)
                .SelectMany(c => c.Competencies ?? new List<CompetencyCategory>())
                .SelectMany(cat =>
                    new List<string> { cat.CategoryName }
                        .Concat(
                            cat.SubCategories?.Select(sub => sub.SubCategoryName)
                                ?? new List<string>()
                        )
                        .Concat(
                            cat.SubCategories?.SelectMany(sub =>
                                sub.Competencies?.Select(comp => comp.CompetencyName)
                                ?? new List<string>()
                            ) ?? new List<string>()
                        )
                )
                .Distinct()
                .ToList();

            AllApplicants = (Applicants ?? new List<Applicant>())
                .Select(c => $"{c.FirstName} {c.LastName} , {c.Email} , {c.Phone}")
                .Distinct()
                .ToList();

            // Apply filters
            ApplyFilters();

            // Send RegionFilter to the view using ViewData
            ViewData["Region"] = RegionFilter;
            
            return Page();
        }

        // OnPostFilter handles POST requests for advanced filtering
        public async Task<IActionResult> OnPostFilterAsync()
        {
            try
            {
                // Use helper method to determine effective region with authorization check
                RegionFilter = GetEffectiveRegion(null); // No URL region for POST

                // Get applicants based on region and filters
                var applicantsRaw = await _userRepository.GetAllApplicants(RegionFilter) ?? new List<Applicant>();
                Applicants = applicantsRaw.Where(a => !a.IsDeleted).ToList();

                // Apply filters
                ApplyFilters();

                return Page();
            }
            catch (UnauthorizedAccessException ex)
            {
                // Log the unauthorized access attempt
                Console.WriteLine($"[SECURITY] Unauthorized region access attempt: {ex.Message}");
                
                // Set error message for display
                TempData["ErrorMessage"] = "Access Denied: You do not have permission to access this region's data.";
                TempData["ErrorDetails"] = ex.Message;
                
                // Redirect back to their authorized region
                var userRegion = HttpContext.Session.GetString("UserRegion");
                if (!string.IsNullOrEmpty(userRegion))
                {
                    return RedirectToPage("/Applicants", new { region = userRegion });
                }
                return RedirectToPage("/Index");
            }
            catch (Exception ex)
            {
                // Log any other errors
                Console.WriteLine($"[ERROR] Error filtering applicants: {ex.Message}");
                
                // Return to login or show error
                return RedirectToPage("/Index");
            }
        }

        // Method to handle saving the private note
        public async Task<IActionResult> OnPostSaveNoteAsync(
            string userId,
            string note,
            string initials
        )
        {
            await _userRepository.SavePrivateNoteAsync(userId, note, initials);
            return RedirectToPage();
        }

        // Method to handle updating the user role
        public async Task<IActionResult> OnPostUpdateUserRoleAsync(string userId)
        {
            await _userRepository.UpdateUserRoleAsync(userId, 1); // Change role to Consultant (1)
            return RedirectToPage();
        }

        private void ApplyFilters()
        {
            // If a search query is provided, filter the consultants based on it
            if (!string.IsNullOrEmpty(SearchQuery))
            {
                var searchTerms = SearchQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                Applicants = Applicants
                    .Where(c =>
                        searchTerms.All(term =>
                            (
                                c.FirstName?.Contains(term, StringComparison.OrdinalIgnoreCase)
                                ?? false
                            )
                            || (
                                c.LastName?.Contains(term, StringComparison.OrdinalIgnoreCase)
                                ?? false
                            )
                            || (
                                c.Competencies?.Any(cat =>
                                    cat.CategoryName.Contains(
                                        term,
                                        StringComparison.OrdinalIgnoreCase
                                    )
                                    || (
                                        cat.SubCategories?.Any(sub =>
                                            sub.SubCategoryName.Contains(
                                                term,
                                                StringComparison.OrdinalIgnoreCase
                                            )
                                            || (
                                                sub.Competencies?.Any(comp =>
                                                    comp.CompetencyName.Contains(
                                                        term,
                                                        StringComparison.OrdinalIgnoreCase
                                                    )
                                                ) ?? false
                                            )
                                        ) ?? false
                                    )
                                ) ?? false
                            )
                        )
                    )
                    .ToList();
            }

            // Apply availability filter if it's provided
            if (IsAvailableFilter.HasValue)
            {
                Applicants = Applicants
                    .Where(c => c.IsAvailable == IsAvailableFilter.Value)
                    .ToList();
            }

            // Sort consultants by last name
            Applicants = Applicants.OrderBy(c => c.LastName).ToList();
        }
    }
}
