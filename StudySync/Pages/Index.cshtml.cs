using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StudySync.Services;

namespace StudySync.Pages
{
    public class IndexModel : PageModel
    {
        private readonly JwtService _jwt;

        public IndexModel(JwtService jwt)
        {
            _jwt = jwt;
        }

        public IActionResult OnGet()
        {
            // If already logged in redirect straight to dashboard
            var token = Request.Cookies["ss_token"];
            if (!string.IsNullOrEmpty(token))
            {
                var principal = _jwt.ValidateToken(token);
                if (principal != null)
                    return RedirectToPage("/Dashboard");
            }

            return Page();
        }
    }
}