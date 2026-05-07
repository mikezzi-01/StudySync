using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using StudySync.Data;
using StudySync.Services;
using System.Security.Claims;

namespace StudySync.Pages.Api
{
    public class NotificationsModel : PageModel
    {
        private readonly StudySyncDbContext _db;
        private readonly JwtService _jwt;

        public NotificationsModel(StudySyncDbContext db, JwtService jwt)
        {
            _db = db;
            _jwt = jwt;
        }

        private int? GetUserIdFromCookie()
        {
            var token = Request.Cookies["ss_token"];
            if (string.IsNullOrEmpty(token)) return null;
            var principal = _jwt.ValidateToken(token);
            var idClaim = principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(idClaim, out var id) ? id : null;
        }

        private string TimeAgo(DateTime dt)
        {
            var diff = DateTime.UtcNow - dt;
            if (diff.TotalMinutes < 1) return "Just now";
            if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
            if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
            if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d ago";
            return dt.ToString("MMM dd");
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var userId = GetUserIdFromCookie();
            if (userId == null) return new JsonResult(new { count = 0, notifications = new List<object>() });

            // Fetch incoming pending requests
            var requests = await _db.Partnerships
                .Where(p => p.User2ID == userId && p.Status == "Requested")
                .Include(p => p.User1)
                .OrderByDescending(p => p.CreatedAt)
                .Take(10)
                .ToListAsync();

            var notifications = requests.Select(p =>
            {
                var sender = p.User1!;
                var initials = $"{sender.FirstName[0]}{(sender.LastName?.Length > 0 ? sender.LastName[0] : ' ')}".Trim().ToUpper();
                return new
                {
                    partnershipId = p.PartnershipID,
                    senderName = $"{sender.FirstName} {sender.LastName}".Trim(),
                    initials,
                    timeAgo = TimeAgo(p.CreatedAt)
                };
            }).ToList();

            return new JsonResult(new
            {
                count = notifications.Count,
                notifications
            });
        }
    }
}
