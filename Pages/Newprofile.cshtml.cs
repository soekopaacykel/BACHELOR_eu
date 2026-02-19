using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CVAPI.Models;
using CVAPI.Repos;
using CVAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Newtonsoft.Json;

namespace CVAPI.Pages
{
    // Note: Authentication now handled client-side via JavaScript
    // [Authorize] removed to prevent 401 on page navigation
    public class NewProfileModel : PageModel
    {
        private readonly CompetenciesRepository _competenciesRepository;
        private readonly UserRepository _userRepository;
        private readonly ExperienceRepository _experienceRepository;
        private readonly OneTimeLinkService _oneTimeLinkService; // Add this service

        // Constructor for dependency injection
        public NewProfileModel(
            CompetenciesRepository competenciesRepository,
            UserRepository userRepository,
            ExperienceRepository experienceRepository,
            OneTimeLinkService oneTimeLinkService // Inject the service
        )
        {
            _competenciesRepository = competenciesRepository;
            _userRepository = userRepository;
            _experienceRepository = experienceRepository;
            _oneTimeLinkService = oneTimeLinkService; // Assign the service
        }

        private string GetRegion() => HttpContext.Session.GetString("UserRegion") ?? "DK";

        // Property to hold fetched data
        public List<CompetencyCategory> Categories { get; set; }

        // Property to hold the applicant's data from the form
        [BindProperty]
        public Applicant NewApplicant { get; set; }

        // Property to handle the option to send invitation email
        [BindProperty]
        public bool SendInvitationEmail { get; set; } = true;

        // Property to expose the logged-in user's email
        public string UserEmail => User?.Identity?.IsAuthenticated == true ? (User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? User.Identity?.Name ?? string.Empty) : string.Empty;

        
        // Get data for the form on GET request
        public async Task OnGetAsync()
        {
            // Get region from session
            var region = GetRegion();
            
            // Get predefined data from repository
            var predefinedData = await _competenciesRepository.GetPredefinedDataAsync(region);
            Categories = predefinedData?.Competencies;
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                Console.WriteLine("Error: ModelState is not valid.");
                foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
                {
                    Console.WriteLine($"ModelState error: {error.ErrorMessage}");
                }
                
                // Get predefined data again when returning to the page due to validation error
                var predefinedData = await _competenciesRepository.GetPredefinedDataAsync(GetRegion());
                Categories = predefinedData?.Competencies;
                
                return Page();
            }

            // Ensure the newApplicant field is always set to true
            ModelState.Remove("newApplicant"); // Remove validation error if it exists
            NewApplicant = NewApplicant ?? new Applicant(); // Ensure NewApplicant is not null

            // Ensure the applicant has an ID if not already assigned
            if (string.IsNullOrEmpty(NewApplicant.Id))
            {
                NewApplicant.Id = Guid.NewGuid().ToString();
            }
            
            if (string.IsNullOrEmpty(NewApplicant.UserId))
            {
                NewApplicant.UserId = Guid.NewGuid().ToString();
            }

            Console.WriteLine(
                $"Saving applicant: {NewApplicant.FirstName} {NewApplicant.LastName}"
            );

            // Generate a random password for the applicant
            string generatedPassword = PasswordHelper.GenerateRandomPassword(12);
            // Hash the password
            string hashedPassword = PasswordHelper.HashPassword(generatedPassword);
            // Assign the hashed password to the applicant
            NewApplicant.Password = hashedPassword;

            // Set the UserRole to Applicant
            NewApplicant.UserRole = Role.Applicant;
            
            // Set DateAdded to current time
            NewApplicant.DateAdded = DateTime.UtcNow;

            // Save the applicant
            await _userRepository.AddApplicantAsync(NewApplicant, GetRegion());

            Console.WriteLine("Applicant successfully saved in Cosmos DB!");

            // Save the generated password in TempData (for possible display on the success page)
            TempData["GeneratedPassword"] = generatedPassword;

            // Generate and send one-time link if requested
            if (SendInvitationEmail && !string.IsNullOrEmpty(NewApplicant.Email))
            {
                try
                {
                    await _oneTimeLinkService.GenerateAndSendOneTimeLink(NewApplicant.Email);
                    TempData["EmailSent"] = true;
                    TempData["EmailAddress"] = NewApplicant.Email;
                }
                catch (Exception ex)
                {
                    TempData["EmailError"] = ex.Message;
                    Console.WriteLine($"Error sending email: {ex.Message}");
                }
            }

            return RedirectToPage("SuccessPage");
        }
    }
}