using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CVAPI.Pages
{
    public class IndexModel : PageModel
    {
        [BindProperty]
        public string Email { get; set; }

        [BindProperty]
        public string Password { get; set; }

        public string ErrorMessage { get; set; }

        public void OnGet()
        {
            // Initial GET-request håndtering, hvis nødvendigt
        }

       public IActionResult OnPost()
{
    // Simpel login-validering
    if (Email == "admin" && Password == "password123")
    {
        // Redirect til en anden side efter succesfuld login
        return Redirect("/Consultants"); // Sørg for at have en Consultants-side
    }
    else
    {
        ErrorMessage = "Ugyldigt brugernavn eller kodeord.";
        return Page(); // Retur til samme side og viser fejlmeddelelse
    }
}

    }
}
