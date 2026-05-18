using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StudySync.Data;
using StudySync.Services;

namespace StudySync.Pages
{
    public class HelpCenterModel : StudySyncPageModel
    {
        public HelpCenterModel(StudySyncDbContext db, JwtService jwt)
            : base(db, jwt) { }

        public async Task<IActionResult> OnGetAsync()
        {
            var userId = GetCurrentUserId();
            if (userId == null) return RedirectToPage("/Login");

            await PopulateLayoutAsync(userId.Value);
            ViewData["ActivePage"] = "HelpCenter";
            ViewData["ShowSearch"] = false;
            ViewData["Title"] = "Help Center";

            return Page();
        }
    }
}
