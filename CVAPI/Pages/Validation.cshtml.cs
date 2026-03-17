using System;
using System.Threading.Tasks;
using CVAPI.Models;
using CVAPI.Repos;
using CVAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CVAPI.Pages
{
    public class ValidateLinkModel : PageModel
    {
        private readonly OneTimeLinkService _oneTimeLinkService;
        private readonly UserRepository _userRepository;

        public ValidateLinkModel(OneTimeLinkService oneTimeLinkService, UserRepository userRepository)
        {
            _oneTimeLinkService = oneTimeLinkService;
            _userRepository = userRepository;
        }

        [BindProperty(SupportsGet = true)]
        public string Token { get; set; }

        public string Email { get; set; }
        public string UserId { get; set; }
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; }
/*
        public async Task<IActionResult> OnGetAsync()
        {
            if (string.IsNullOrEmpty(Token))
            {
                IsValid = false;
                ErrorMessage = "Invalid token. Please request a new link.";
                return Page();
            }

            try
            {
                // Validate the token
                IsValid = await _oneTimeLinkService.ValidateOneTimeLink(Token);

                if (IsValid)
                {
                    // If token is valid, get the email associated with it
                    var linkInfo = _oneTimeLinkService.GetLinkInfo(Token);
                    if (linkInfo != null)
                    {
                        Email = linkInfo.Email;

                        // Find the user by email
                        var user = await _userRepository.GetUserByEmailAsync(Email, "DK");
                        if (user != null)
                        {
                            UserId = user.UserId;
                            
                            // Redirect to edit profile page with the user ID
                            return RedirectToPage("/EditProfile", new { userId = UserId });
                        }
                        else
                        {
                            IsValid = false;
                            ErrorMessage = "User not found with the provided email.";
                        }
                    }
                    else
                    {
                        IsValid = false;
                        ErrorMessage = "Link information could not be retrieved.";
                    }
                }
                else
                {
                    ErrorMessage = "This link has expired or has already been used. Please request a new link.";
                }
            }
            catch (Exception ex)
            {
                IsValid = false;
                ErrorMessage = $"An error occurred: {ex.Message}";
            }

            return Page();
        }
        */
    }
}