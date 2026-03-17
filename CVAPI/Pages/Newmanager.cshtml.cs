using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CVAPI.Models;
using CVAPI.Repos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Newtonsoft.Json;

namespace CVAPI.Pages
{
    // Note: Authentication now handled client-side via JavaScript
    // [Authorize] removed to prevent 401 on page navigation
    public class ManagersModel : PageModel
    {
        private readonly UserRepository _userRepository;

        [BindProperty]
        public Manager NewManager { get; set; } = new Manager(); // Initialize to avoid null reference issues

        public List<Manager> ExistingManagers { get; set; } = new List<Manager>(); // Ensure it's initialized
        public List<Admin> ExistingAdmins { get; set; } = new List<Admin>();

        [BindProperty]
        public string SelectedManagerId { get; set; }

        public bool IsSuccess { get; set; }
        public bool IsError { get; set; }
        public string ErrorMessage { get; set; }

        // Property to expose the logged-in user's email
        public string UserEmail => User?.Identity?.IsAuthenticated == true
            ? (User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? User.Identity?.Name ?? string.Empty)
            : string.Empty;

        public ManagersModel(UserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        private string GetRegion() => HttpContext.Session.GetString("UserRegion") ?? "DK";

        // Helper method to check admin authorization
        private bool IsAuthorizedAdmin()
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            return !string.IsNullOrEmpty(userRole) && (userRole == "3" || userRole == "Admin");
        }

        private IActionResult UnauthorizedAdminAccess()
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            Console.WriteLine($"[SECURITY] Non-admin user attempted admin operation. Role: {userRole}");
            TempData["ErrorMessage"] = "Access denied. This operation requires Administrator privileges.";
            return RedirectToPage("/Consultants");
        }

        public async Task<IActionResult> OnGetAsync()
        {
            try
            {
                // Server-side admin authorization check
                var userRole = HttpContext.Session.GetString("UserRole");
                
                if (string.IsNullOrEmpty(userRole) || (userRole != "3" && userRole != "Admin"))
                {
                    Console.WriteLine($"[SECURITY] Non-admin user attempted to access Newmanager page. Role: {userRole}");
                    TempData["ErrorMessage"] = "Access denied. This page is only accessible to Administrators.";
                    return RedirectToPage("/Consultants");
                }
                
                Console.WriteLine($"[NEWMANAGER] Admin access granted for user with role: {userRole}");
                
                // Authentication now handled client-side via JavaScript
                // Load data directly since auth check is done in browser
                var region = GetRegion();
                ExistingManagers = await _userRepository.GetAllManagersAsync(region);
                ExistingAdmins = await _userRepository.GetAllAdminsAsync(region);

                return Page();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Could not load managers and admins: {ex.Message}";
                IsError = true;
                return Page();
            }
        }

        public async Task<IActionResult> OnPostCreateManagerAsync()
        {
            // Check admin authorization
            if (!IsAuthorizedAdmin())
            {
                return UnauthorizedAdminAccess();
            }

            // Extensive logging for debugging
            Console.WriteLine("[DEBUG] OnPostCreateManagerAsync called.");
            Console.WriteLine($"[DEBUG] ModelState.IsValid: {ModelState.IsValid}");

            // Log all model validation errors
            if (!ModelState.IsValid)
            {
                foreach (var modelState in ModelState.Values)
                {
                    foreach (var error in modelState.Errors)
                    {
                        Console.WriteLine($"[DEBUG] Model Error: {error.ErrorMessage}");
                    }
                }

                IsError = true;
                ErrorMessage = "Invalid manager data. Please check all fields.";
                var region = GetRegion();
                ExistingManagers = await _userRepository.GetAllManagersAsync(region);
                ExistingAdmins = await _userRepository.GetAllAdminsAsync(region);
                return Page();
            }

            try
            {
                // Log the incoming manager data
                Console.WriteLine($"[DEBUG] NewManager details:");
                Console.WriteLine($"First Name: {NewManager.FirstName}");
                Console.WriteLine($"Last Name: {NewManager.LastName}");
                Console.WriteLine($"Email: {NewManager.Email}");
                Console.WriteLine($"Phone: {NewManager.Phone}");

                // Validate critical fields manually
                if (string.IsNullOrWhiteSpace(NewManager.Email))
                {
                    throw new ArgumentException("Email is required.");
                }

                if (string.IsNullOrWhiteSpace(NewManager.Password))
                {
                    throw new ArgumentException("Password is required.");
                }

                // Assign a new UserId
                NewManager.UserId = Guid.NewGuid().ToString();


                // Hash the password
                NewManager.Password = PasswordHelper.HashPassword(NewManager.Password);

                // Set the user role
                NewManager.UserRole = Role.Manager;

                // Log before saving
                Console.WriteLine("[DEBUG] Attempting to save manager to database");

                // Save the new manager to the database
                await _userRepository.AddUserAsync(NewManager, GetRegion());

                Console.WriteLine("[DEBUG] Manager successfully saved");

                IsSuccess = true;
                TempData["SuccessMessage"] = "Manager created successfully!";

                // Reload the list of managers and admins
                var region = GetRegion();
                ExistingManagers = await _userRepository.GetAllManagersAsync(region);
                ExistingAdmins = await _userRepository.GetAllAdminsAsync(region);

                // Reset the NewManager object to clear the form
                NewManager = new Manager();

                return Page();
            }
            catch (Exception ex)
            {
                // Log the full exception details
                Console.WriteLine($"[ERROR] Failed to create manager: {ex.Message}");
                Console.WriteLine($"[ERROR] Stack Trace: {ex.StackTrace}");

                IsError = true;
                ErrorMessage = $"Error creating manager: {ex.Message}";
                ExistingManagers = await _userRepository.GetAllManagersAsync(GetRegion());
                ExistingAdmins = await _userRepository.GetAllAdminsAsync(GetRegion());
                return Page();
            }
        }

        public async Task<IActionResult> OnPostResetPasswordAsync()
        {
            // Check admin authorization
            if (!IsAuthorizedAdmin())
            {
                return UnauthorizedAdminAccess();
            }

            if (string.IsNullOrEmpty(SelectedManagerId))
            {
                IsError = true;
                ErrorMessage = "No manager selected for password reset.";
                // Reload the list of managers to ensure the page is consistent
                ExistingManagers = await _userRepository.GetAllManagersAsync(GetRegion());
                ExistingAdmins = await _userRepository.GetAllAdminsAsync(GetRegion());
                return Page();
            }

            try
            {
                // Generate a new random password
                string newPassword = PasswordHelper.GenerateRandomPassword(12);
                string hashedPassword = PasswordHelper.HashPassword(newPassword);

                // Update the manager's password in the database
                await _userRepository.UpdateUserPasswordAsync(SelectedManagerId, hashedPassword, GetRegion());

                // Store the new password in TempData to display it in the view
                TempData["NewPassword"] = newPassword;

                IsSuccess = true;

                // Reload the list of managers and admins
                ExistingManagers = await _userRepository.GetAllManagersAsync(GetRegion());
                ExistingAdmins = await _userRepository.GetAllAdminsAsync(GetRegion());

                return Page();
            }
            catch (Exception ex)
            {
                IsError = true;
                ErrorMessage = $"Error resetting password: {ex.Message}";
                // Reload the list of managers to ensure the page is consistent
                ExistingManagers = await _userRepository.GetAllManagersAsync(GetRegion());
                ExistingAdmins = await _userRepository.GetAllAdminsAsync(GetRegion());
                return Page();
            }
        }

        public async Task<IActionResult> OnPostDeleteManagerAsync()
        {
            // Check admin authorization
            if (!IsAuthorizedAdmin())
            {
                return UnauthorizedAdminAccess();
            }

            if (string.IsNullOrEmpty(SelectedManagerId))
            {
                IsError = true;
                ErrorMessage = "No manager selected for deletion.";
                // Reload the list of managers to ensure the page is consistent
                ExistingManagers = await _userRepository.GetAllManagersAsync(GetRegion());
                ExistingAdmins = await _userRepository.GetAllAdminsAsync(GetRegion());
                return Page();
            }

            try
            {
                // Delete the manager from the database
                await _userRepository.DeleteUserAsync(SelectedManagerId, GetRegion());

                IsSuccess = true;

                // Reload the list of managers and admins
                ExistingManagers = await _userRepository.GetAllManagersAsync(GetRegion());
                ExistingAdmins = await _userRepository.GetAllAdminsAsync(GetRegion());

                return Page();
            }
            catch (Exception ex)
            {
                IsError = true;
                ErrorMessage = $"Error deleting manager: {ex.Message}";
                // Reload the list of managers to ensure the page is consistent
                ExistingManagers = await _userRepository.GetAllManagersAsync(GetRegion());
                ExistingAdmins = await _userRepository.GetAllAdminsAsync(GetRegion());
                return Page();
            }
        }

        public async Task<IActionResult> OnPostResetAdminPasswordAsync()
        {
            // Check admin authorization
            if (!IsAuthorizedAdmin())
            {
                return UnauthorizedAdminAccess();
            }

            if (string.IsNullOrEmpty(SelectedManagerId))
            {
                IsError = true;
                ErrorMessage = "No admin selected for password reset.";
                ExistingManagers = await _userRepository.GetAllManagersAsync(GetRegion());
                ExistingAdmins = await _userRepository.GetAllAdminsAsync(GetRegion());
                return Page();
            }

            try
            {
                string newPassword = PasswordHelper.GenerateRandomPassword(12);
                string hashedPassword = PasswordHelper.HashPassword(newPassword);

                await _userRepository.UpdateUserPasswordAsync(SelectedManagerId, hashedPassword, GetRegion());

                TempData["NewPassword"] = newPassword;

                IsSuccess = true;
                ExistingManagers = await _userRepository.GetAllManagersAsync(GetRegion());
                ExistingAdmins = await _userRepository.GetAllAdminsAsync(GetRegion());

                return Page();
            }
            catch (Exception ex)
            {
                IsError = true;
                ErrorMessage = $"Error resetting admin password: {ex.Message}";
                ExistingManagers = await _userRepository.GetAllManagersAsync(GetRegion());
                ExistingAdmins = await _userRepository.GetAllAdminsAsync(GetRegion());
                return Page();
            }
        }

        public async Task<IActionResult> OnPostDeleteAdminAsync()
        {
            // Check admin authorization
            if (!IsAuthorizedAdmin())
            {
                return UnauthorizedAdminAccess();
            }

            if (string.IsNullOrEmpty(SelectedManagerId))
            {
                IsError = true;
                ErrorMessage = "No admin selected for deletion.";
                ExistingManagers = await _userRepository.GetAllManagersAsync(GetRegion());
                ExistingAdmins = await _userRepository.GetAllAdminsAsync(GetRegion());
                return Page();
            }

            try
            {
                await _userRepository.DeleteUserAsync(SelectedManagerId, GetRegion());

                IsSuccess = true;
                ExistingManagers = await _userRepository.GetAllManagersAsync(GetRegion());
                ExistingAdmins = await _userRepository.GetAllAdminsAsync(GetRegion());

                return Page();
            }
            catch (Exception ex)
            {
                IsError = true;
                ErrorMessage = $"Error deleting admin: {ex.Message}";
                ExistingManagers = await _userRepository.GetAllManagersAsync(GetRegion());
                ExistingAdmins = await _userRepository.GetAllAdminsAsync(GetRegion());
                return Page();
            }
        }

        public async Task<IActionResult> OnPostUpdatePasswordAsync([FromBody] PasswordUpdateRequest request)
        {
            // Check admin authorization
            if (!IsAuthorizedAdmin())
            {
                Console.WriteLine("[SECURITY] Non-admin user attempted password update operation");
                return StatusCode(403, new { error = "Access denied", message = "This operation requires Administrator privileges." });
            }

            Console.WriteLine("[DEBUG] OnPostUpdatePasswordAsync called.");
            Console.WriteLine($"[DEBUG] UserId: {request.UserId}, NewPassword: {request.NewPassword}");

            if (string.IsNullOrEmpty(request.UserId) || string.IsNullOrEmpty(request.NewPassword))
            {
                Console.WriteLine("[ERROR] UserId or NewPassword is missing.");
                return BadRequest("UserId and NewPassword are required.");
            }

            try
            {
                Console.WriteLine("[DEBUG] Attempting to update password in the repository.");
                await _userRepository.UpdateManagerPasswordAsync(request.UserId, request.NewPassword, GetRegion());
                Console.WriteLine("[DEBUG] Password updated successfully.");
                return new JsonResult(new { success = true });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to update password: {ex.Message}");
                Console.WriteLine($"[ERROR] Stack Trace: {ex.StackTrace}");
                return StatusCode(500, "An error occurred while updating the password.");
            }
        }

        public class PasswordUpdateRequest
        {
            public string UserId { get; set; } = string.Empty;
            public string NewPassword { get; set; } = string.Empty;
        }
    }
}
