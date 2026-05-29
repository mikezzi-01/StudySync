using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using StudySync.Data;
using StudySync.Services;
using System.Security.Claims;

namespace StudySync.Pages
{
    /// <summary>
    /// Base class for all authenticated Razor Pages.
    /// Populates ViewData needed by _Layout.cshtml:
    ///   - UserInitials, FullName, PendingCount
    /// Each page sets:
    ///   - ViewData["ActivePage"]       e.g. "Dashboard"
    ///   - ViewData["ShowSearch"]       true/false
    ///   - ViewData["SearchPlaceholder"] e.g. "Search partners..."
    /// </summary>
    public abstract class StudySyncPageModel : PageModel
    {
        protected readonly StudySyncDbContext Db;
        protected readonly JwtService Jwt;

        protected StudySyncPageModel(StudySyncDbContext db, JwtService jwt)
        {
            Db = db;
            Jwt = jwt;
        }

        // ── Get current user ID from JWT cookie ───────────────────────────
        protected int? GetCurrentUserId()
        {
            var token = Request.Cookies["ss_token"];
            if (string.IsNullOrEmpty(token)) return null;
            var principal = Jwt.ValidateToken(token);
            var idClaim = principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(idClaim, out var id) ? id : null;
        }

        // ── Populate layout ViewData ──────────────────────────────────────
        protected async Task PopulateLayoutAsync(int userId)
        {
            var user = await Db.Users.FindAsync(userId);
            if (user == null) return;

            var fullName = $"{user.FirstName} {user.LastName}".Trim();
            var userInitials = $"{user.FirstName[0]}{(user.LastName?.Length > 0 ? user.LastName[0] : ' ')}".Trim().ToUpper();

            var pendingCount = await Db.Partnerships
                .CountAsync(p => p.User2ID == userId && p.Status == "Requested");

            ViewData["FullName"] = fullName;
            ViewData["UserInitials"] = userInitials;
            ViewData["PendingCount"] = pendingCount;
            ViewData["IsAdmin"] = user.IsAdmin;
        }
    }
}
