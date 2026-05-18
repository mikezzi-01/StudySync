using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using StudySync.Data;
using StudySync.Models;
using StudySync.Services;
using System.Security.Claims;

namespace StudySync.Pages
{
    public class RequestsModel : StudySyncPageModel
    {
        private readonly StudySyncDbContext _db;
        private readonly JwtService _jwt;

        public RequestsModel(StudySyncDbContext db, JwtService jwt) : base(db, jwt)
        {
            _db = db;
            _jwt = jwt;
        }

        // ── Page properties ──────────────────────────────────────────────
        public string UserInitials { get; set; } = "";
        public string FullName { get; set; } = "";
        public string SuccessMessage { get; set; } = "";

        public List<RequestCardModel> IncomingRequests { get; set; } = new();
        public List<RequestCardModel> SentRequests { get; set; } = new();
        public List<RequestCardModel> ActivePartnerships { get; set; } = new();

        // ── Helper ───────────────────────────────────────────────────────
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
            return dt.ToString("MMM dd, yyyy");
        }

        // ── GET ──────────────────────────────────────────────────────────
        public async Task<IActionResult> OnGetAsync(string? message = null)
        {
            var userId = GetUserIdFromCookie();
            if (userId == null) return RedirectToPage("/Login");

            var user = await _db.Users.FindAsync(userId);
            if (user == null) return RedirectToPage("/Login");

            FullName = $"{user.FirstName} {user.LastName}".Trim();
            UserInitials = $"{user.FirstName[0]}{(user.LastName?.Length > 0 ? user.LastName[0] : ' ')}".Trim().ToUpper();

            await PopulateLayoutAsync(userId.Value);
            ViewData["ActivePage"] = "Requests";
            ViewData["ShowSearch"] = true;
            ViewData["SearchPlaceholder"] = "Search requests by name...";
            ViewData["Title"] = "Partnership Requests";

            if (message == "accepted") SuccessMessage = "Partnership accepted! You can now collaborate.";
            if (message == "declined") SuccessMessage = "Request declined.";

            // Load all partnerships involving this user
            var allPartnerships = await _db.Partnerships
                .Where(p => p.User1ID == userId || p.User2ID == userId)
                .Include(p => p.User1)
                    .ThenInclude(u => u.LearnerProfile)
                        .ThenInclude(lp => lp.LearnerProfileInterests)
                            .ThenInclude(lpi => lpi.Interest)
                .Include(p => p.User2)
                    .ThenInclude(u => u.LearnerProfile)
                        .ThenInclude(lp => lp.LearnerProfileInterests)
                            .ThenInclude(lpi => lpi.Interest)
                .ToListAsync();

            foreach (var p in allPartnerships)
            {
                var isRequester = p.User1ID == userId;
                var partner = isRequester ? p.User2! : p.User1!;
                var profile = partner.LearnerProfile;

                // Get match score from cache
                var cached = await _db.RecommendationCaches
                    .FirstOrDefaultAsync(rc =>
                        rc.UserID == userId && rc.TargetUserID == partner.UserID);

                var card = new RequestCardModel
                {
                    PartnershipId = p.PartnershipID,
                    FullName = $"{partner.FirstName} {partner.LastName}".Trim(),
                    Initials = $"{partner.FirstName[0]}{(partner.LastName?.Length > 0 ? partner.LastName[0] : ' ')}".Trim().ToUpper(),
                    AcademicLevel = partner.AcademicLevel.ToString(),
                    MatchScore = cached != null ? (int)Math.Round(cached.CosineScore * 100) : 0,
                    InterestTags = profile?.LearnerProfileInterests
                        .Select(lpi => lpi.Interest?.InterestName ?? "")
                        .Where(n => !string.IsNullOrEmpty(n))
                        .ToList() ?? new List<string>(),
                    TimeAgo = TimeAgo(p.CreatedAt)
                };

                if (p.Status == "Requested" && !isRequester)
                    IncomingRequests.Add(card);
                else if (p.Status == "Requested" && isRequester)
                    SentRequests.Add(card);
                else if (p.Status == "Active" || p.Status == "Accepted")
                {
                    card.TimeAgo = TimeAgo(p.AcceptedAt ?? p.CreatedAt);
                    ActivePartnerships.Add(card);
                }
            }

            return Page();
        }

        // ── POST: Accept or Decline ───────────────────────────────────────
        public async Task<IActionResult> OnPostRespondAsync([FromBody] RespondBody body)
        {
            var userId = GetUserIdFromCookie();
            if (userId == null) return Unauthorized();

            var partnership = await _db.Partnerships
                .FirstOrDefaultAsync(p => p.PartnershipID == body.PartnershipId &&
                                         p.User2ID == userId &&
                                         p.Status == "Requested");

            if (partnership == null)
                return BadRequest("Partnership not found or you are not the recipient.");

            if (body.Action == "accept")
            {
                partnership.Status = "Active";
                partnership.AcceptedAt = DateTime.UtcNow;
                partnership.LastActivityAt = DateTime.UtcNow;
            }
            else if (body.Action == "decline")
            {
                partnership.Status = "Archived";
                partnership.ClosureReason = "Declined by recipient";
            }
            else
            {
                return BadRequest("Invalid action.");
            }

            await _db.SaveChangesAsync();
            return new OkResult();
        }
    }

    // ── View models ───────────────────────────────────────────────────────
    public class RequestCardModel
    {
        public int PartnershipId { get; set; }
        public string FullName { get; set; } = "";
        public string Initials { get; set; } = "";
        public string AcademicLevel { get; set; } = "";
        public int MatchScore { get; set; }
        public List<string> InterestTags { get; set; } = new();
        public string TimeAgo { get; set; } = "";
    }

    public class RespondBody
    {
        public int PartnershipId { get; set; }
        public string Action { get; set; } = "";
    }
}
