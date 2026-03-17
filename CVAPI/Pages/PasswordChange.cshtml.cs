using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CVAPI.Pages
{
    public class PasswordChangeModel : PageModel
    {
        public IActionResult OnGet()
        {
            // Authentication now handled client-side via JavaScript
            return Page();
        }
    }
}
