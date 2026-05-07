using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using StudySync.Data;
using StudySync.Services;
using System.Security.Claims;

namespace StudySync.Pages
{
    public class CollaborateHomeModel : PageModel
    {
        private readonly StudySyncDbContext _db;
        private readonly JwtService _jwt;

        public CollaborateHomeModel(StudySyncDbContext db, JwtService jwt)
        {
            _db = db;
            _jwt = jwt;
        }

        // ── Page properties ──────────────────────────────────────────────
        public string UserInitials { get; set; } = "";
        public string FullName { get; set; } = "";
        public int PendingRequestCount { get; set; } = 0;

        public List<CollabCardModel> ActivePartnerships { get; set; } = new();
        public List<CollabCardModel> EndedPartnerships { get; set; } = new();

        // ── Helper ───────────────────────────────────────────────────────
        private int? GetUserIdFromCookie()
        {
            var token = Request.Cookies["ss_token"];
            if (string.IsNullOrEmpty(token)) return null;
            var principal = _jwt.ValidateToken(token);
            var idClaim = principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(idClaim, out var id) ? id : null;
        }

        private string MakeInitials(string first, string? last)
            => $"{first[0]}{(last?.Length > 0 ? last[0] : ' ')}".Trim().ToUpper();

        // ── GET ──────────────────────────────────────────────────────────
        public async Task<IActionResult> OnGetAsync()
        {
            var userId = GetUserIdFromCookie();
            if (userId == null) return RedirectToPage("/Login");

            var user = await _db.Users.FindAsync(userId);
            if (user == null) return RedirectToPage("/Login");

            FullName = $"{user.FirstName} {user.LastName}".Trim();
            UserInitials = MakeInitials(user.FirstName, user.LastName);

            // Pending incoming requests count for notification badge
            PendingRequestCount = await _db.Partnerships
                .CountAsync(p => p.User2ID == userId && p.Status == "Requested");

            // Load all partnerships
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
                .OrderByDescending(p => p.LastActivityAt)
                .ToListAsync();

            foreach (var p in allPartnerships)
            {
                var partner = p.User1ID == userId ? p.User2! : p.User1!;
                var profile = partner.LearnerProfile;

                var cached = await _db.RecommendationCaches
                    .FirstOrDefaultAsync(rc =>
                        rc.UserID == userId && rc.TargetUserID == partner.UserID);

                var card = new CollabCardModel
                {
                    PartnershipId = p.PartnershipID,
                    FullName = $"{partner.FirstName} {partner.LastName}".Trim(),
                    Initials = MakeInitials(partner.FirstName, partner.LastName),
                    AcademicLevel = partner.AcademicLevel.ToString(),
                    MatchScore = cached != null ? (int)Math.Round(cached.CosineScore * 100) : 0,
                    InterestTags = profile?.LearnerProfileInterests
                        .Select(lpi => lpi.Interest?.InterestName ?? "")
                        .Where(n => !string.IsNullOrEmpty(n))
                        .ToList() ?? new List<string>(),
                    PartnerSince = (p.AcceptedAt ?? p.CreatedAt).ToString("MMM dd, yyyy")
                };

                if (p.Status == "Active" || p.Status == "Accepted")
                    ActivePartnerships.Add(card);
                else if (p.Status == "Ended")
                    EndedPartnerships.Add(card);
            }

            return Page();
        }
    }

    // ── View model ────────────────────────────────────────────────────────
    public class CollabCardModel
    {
        public int PartnershipId { get; set; }
        public string FullName { get; set; } = "";
        public string Initials { get; set; } = "";
        public string AcademicLevel { get; set; } = "";
        public int MatchScore { get; set; }
        public List<string> InterestTags { get; set; } = new();
        public string PartnerSince { get; set; } = "";
    }
}
