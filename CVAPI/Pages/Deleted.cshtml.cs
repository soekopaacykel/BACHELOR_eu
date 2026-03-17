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
    public class DeletedModel : PageModel
    {
        private readonly UserRepository _userRepository;
        public List<User> RecentlyDeletedUsers { get; set; } = new List<User>();
        public string UserEmail
        {
            get
            {
                if (User?.Identity?.IsAuthenticated == true)
                {
                    return User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
                        ?? User.Identity?.Name
                        ?? string.Empty;
                }
                return string.Empty;
            }
        }

        public DeletedModel(UserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        private string GetRegion() => HttpContext.Session.GetString("UserRegion") ?? "DK";

        public async Task<IActionResult> OnGetAsync()
        {
            // Server-side admin authorization check
            var userRole = HttpContext.Session.GetString("UserRole");
            
            if (string.IsNullOrEmpty(userRole) || (userRole != "3" && userRole != "Admin"))
            {
                Console.WriteLine($"[SECURITY] Non-admin user attempted to access Deleted page. Role: {userRole}");
                TempData["ErrorMessage"] = "Access denied. This page is only accessible to Administrators.";
                return RedirectToPage("/Consultants");
            }
            
            Console.WriteLine($"[DELETED] Admin access granted for user with role: {userRole}");
            
            var region = GetRegion();
            var allConsultants = await _userRepository.GetAllConsultants(region);
            var allApplicants = await _userRepository.GetAllApplicants(region);
            var oneMonthAgo = DateTime.UtcNow.AddMonths(-1);
            var deletedConsultants = allConsultants
                .Where(c =>
                    c.IsDeleted && c.DateAdded >= oneMonthAgo && c.DateAdded <= DateTime.UtcNow
                )
                .Cast<User>();
            var deletedApplicants = allApplicants
                .Where(a =>
                    a.IsDeleted && a.DateAdded >= oneMonthAgo && a.DateAdded <= DateTime.UtcNow
                )
                .Cast<User>();
            RecentlyDeletedUsers = deletedConsultants
                .Concat(deletedApplicants)
                .OrderByDescending(u =>
                    (
                        u is Consultant c
                            ? c.DateAdded
                            : (u is Applicant a ? a.DateAdded : DateTime.MinValue)
                    )
                )
                .ToList();
            
            return Page();
        }

        public class UserIdRequest
        {
            public string? UserId { get; set; }
        }

        public async Task<IActionResult> OnPostRestoreAsync([FromBody] UserIdRequest req)
        {
            if (string.IsNullOrEmpty(req?.UserId))
            {
                await OnGetAsync();
                return Page();
            }

            var consultant = await _userRepository.GetConsultantAsync(req.UserId, GetRegion());
            if (consultant != null)
            {
                consultant.IsDeleted = false;
                await _userRepository.UpdateConsultantAsync(consultant, GetRegion());
                await OnGetAsync();
                return Page();
            }

            var applicant = await _userRepository.GetApplicantAsync(req.UserId, GetRegion());
            if (applicant != null)
            {
                applicant.IsDeleted = false;
                await _userRepository.UpdateApplicantAsync(applicant, GetRegion());
                await OnGetAsync();
                return Page();
            }

            await OnGetAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostDeleteAsync([FromBody] UserIdRequest req)
        {
            try
            {
                Console.WriteLine($"[DEBUG] OnPostDeleteAsync called");
                var loggedInUserId = User.FindFirst(
                    System.Security.Claims.ClaimTypes.NameIdentifier
                )?.Value;
                Console.WriteLine($"[DEBUG] Logged-in UserId: {loggedInUserId}");
                if (req == null)
                {
                    Console.WriteLine("[DEBUG] Model binding failed: req is null");
                    return BadRequest("Request body is null or invalid JSON");
                }
                Console.WriteLine($"[DEBUG] Received UserIdRequest: {{ UserId = '{req.UserId}' }}");
                if (!string.IsNullOrEmpty(req?.UserId))
                {
                    if (req.UserId == loggedInUserId)
                    {
                        Console.WriteLine("[DEBUG] Attempted to delete currently logged-in user!");
                    }
                    var consultant = await _userRepository.GetConsultantAsync(req.UserId, GetRegion());
                    if (consultant != null)
                    {
                        Console.WriteLine(
                            $"[DEBUG] Deleting consultant: {consultant.UserId} {consultant.FirstName} {consultant.LastName}"
                        );
                        await _userRepository.DeleteUserAsync(req.UserId, GetRegion());
                    }
                    else
                    {
                        var applicant = await _userRepository.GetApplicantAsync(req.UserId, GetRegion());
                        if (applicant != null)
                        {
                            Console.WriteLine(
                                $"[DEBUG] Deleting applicant: {applicant.UserId} {applicant.FirstName} {applicant.LastName}"
                            );
                            await _userRepository.DeleteApplicantAsync(req.UserId, GetRegion());
                        }
                        else
                        {
                            Console.WriteLine($"[DEBUG] User not found for deletion: {req.UserId}");
                        }
                    }
                }
                else
                {
                    Console.WriteLine("[DEBUG] UserId is null or empty on delete");
                }
                await OnGetAsync();
                return Page();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Exception in OnPostDeleteAsync: {ex}");
                return new JsonResult(new { error = ex.Message }) { StatusCode = 500 };
            }
        }
    }
}
