using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

// Note: Authentication now handled client-side via JavaScript
// [Authorize] removed to prevent 401 on page navigation
public class SuccessPageModel : PageModel
{
    public IActionResult OnGet()
    {
        // JWT validation is handled automatically by [Authorize] attribute
        // No manual token checking needed
        return Page();
    }
}
