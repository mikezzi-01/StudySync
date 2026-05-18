using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using StudySync.Data;
using StudySync.Models;
using StudySync.Services;
using System.Security.Claims;

namespace StudySync.Pages
{
    public class DashboardModel : StudySyncPageModel
    {
        private readonly StudySyncDbContext _db;
        private readonly JwtService _jwt;

        public DashboardModel(StudySyncDbContext db, JwtService jwt) : base(db, jwt)
        {
            _db = db;
            _jwt = jwt;
        }

        // ── Page properties ──────────────────────────────────────────────
        public string UserInitials { get; set; } = "";
        public string FullName { get; set; } = "";
        public string FirstName { get; set; } = "";
        public string Department { get; set; } = "CS DEPT.";
        public string TimeOfDay { get; set; } = "morning";
        public int NewMatchCount { get; set; } = 0;
        public int ActivePartnerships { get; set; } = 0;
        public int UnreadMessages { get; set; } = 0;
        public int ProfileCompletion { get; set; } = 0;

        public List<MatchCardModel> TopMatches { get; set; } = new();
        public List<CollabRowModel> ActiveCollaborations { get; set; } = new();

        // ── Helper ───────────────────────────────────────────────────────
        private int? GetUserIdFromCookie()
        {
            var token = Request.Cookies["ss_token"];
            if (string.IsNullOrEmpty(token)) return null;
            var principal = _jwt.ValidateToken(token);
            var idClaim = principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(idClaim, out var id) ? id : null;
        }

        // ── GET ──────────────────────────────────────────────────────────
        public async Task<IActionResult> OnGetAsync()
        {
            var userId = GetUserIdFromCookie();
            if (userId == null) return RedirectToPage("/Login");

            var user = await _db.Users
                .Include(u => u.LearnerProfile)
                .FirstOrDefaultAsync(u => u.UserID == userId);

            if (user == null) return RedirectToPage("/Login");

            // Basic user info
            FullName = $"{user.FirstName} {user.LastName}".Trim();
            FirstName = user.FirstName;
            UserInitials = $"{user.FirstName[0]}{(user.LastName?.Length > 0 ? user.LastName[0] : ' ')}".Trim().ToUpper();
            Department = user.LearnerProfile?.PreferredEnvironment ?? "CS DEPT.";
            ProfileCompletion = (int)(user.LearnerProfile?.ProfileCompletion ?? 0);

            await PopulateLayoutAsync(userId.Value);
            ViewData["ActivePage"] = "Dashboard";
            ViewData["ShowSearch"] = false;
            ViewData["Title"] = "Dashboard";

            // Time of day greeting
            var hour = DateTime.Now.Hour;
            TimeOfDay = hour < 12 ? "morning" : hour < 17 ? "afternoon" : "evening";

            // Active partnerships count
            var partnerships = await _db.Partnerships
                .Where(p => (p.User1ID == userId || p.User2ID == userId)
                         && p.Status == "Active")
                .ToListAsync();

            ActivePartnerships = partnerships.Count;

            // Top matches from recommendation cache
            var cached = await _db.RecommendationCaches
                .Where(rc => rc.UserID == userId && rc.ExpiryAt > DateTime.UtcNow)
                .OrderByDescending(rc => rc.CosineScore)
                .Take(3)
                .Include(rc => rc.TargetUser)
                    .ThenInclude(u => u!.LearnerProfile)
                        .ThenInclude(lp => lp!.LearnerProfileInterests)
                            .ThenInclude(lpi => lpi.Interest)
                .ToListAsync();

            NewMatchCount = cached.Count;

            TopMatches = cached.Select(rc => new MatchCardModel
            {
                UserID = rc.TargetUserID,
                FullName = $"{rc.TargetUser!.FirstName} {rc.TargetUser.LastName}".Trim(),
                Initials = $"{rc.TargetUser.FirstName[0]}{(rc.TargetUser.LastName?.Length > 0 ? rc.TargetUser.LastName[0] : ' ')}".Trim().ToUpper(),
                Department = rc.TargetUser.LearnerProfile?.MotivationDriver ?? "Computer Science",
                Score = (int)Math.Round(rc.CosineScore * 100),
                InterestTags = rc.TargetUser.LearnerProfile?.LearnerProfileInterests
                    .Take(3)
                    .Select(lpi => lpi.Interest?.InterestName ?? "")
                    .Where(n => !string.IsNullOrEmpty(n))
                    .ToList() ?? new List<string>()
            }).ToList();

            // Active collaborations
            var collabs = await _db.Partnerships
                .Where(p => (p.User1ID == userId || p.User2ID == userId)
                         && (p.Status == "Active" || p.Status == "Accepted"))
                .Include(p => p.User1)
                .Include(p => p.User2)
                .Take(5)
                .ToListAsync();

            ActiveCollaborations = collabs.Select(p =>
            {
                var partner = p.User1ID == userId ? p.User2! : p.User1!;
                return new CollabRowModel
                {
                    FullName = $"{partner.FirstName} {partner.LastName}".Trim(),
                    Initials = $"{partner.FirstName[0]}{(partner.LastName?.Length > 0 ? partner.LastName[0] : ' ')}".Trim().ToUpper(),
                    ProjectLabel = $"Active since {p.AcceptedAt?.ToString("MMM dd, yyyy") ?? p.CreatedAt.ToString("MMM dd, yyyy")}",
                    Status = p.Status == "Active" ? "Active Discussion" : "Waiting for Review",
                    IsActive = p.Status == "Active"
                };
            }).ToList();

            return Page();
        }

        // ── POST: Send partnership request ───────────────────────────────
        public async Task<IActionResult> OnPostSendRequestAsync([FromBody] SendRequestBody body)
        {
            var userId = GetUserIdFromCookie();
            if (userId == null) return Unauthorized();

            // Check not already partners
            var exists = await _db.Partnerships.AnyAsync(p =>
                (p.User1ID == userId && p.User2ID == body.TargetUserId) ||
                (p.User1ID == body.TargetUserId && p.User2ID == userId));

            if (exists) return BadRequest("Partnership already exists.");

            _db.Partnerships.Add(new Partnership
            {
                User1ID = userId.Value,
                User2ID = body.TargetUserId,
                Status = "Requested",
                CreatedAt = DateTime.UtcNow,
                LastActivityAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();
            return new OkResult();
        }
    }

    // ── View models ───────────────────────────────────────────────────────
    public class MatchCardModel
    {
        public int UserID { get; set; }
        public string FullName { get; set; } = "";
        public string Initials { get; set; } = "";
        public string Department { get; set; } = "";
        public int Score { get; set; }
        public List<string> InterestTags { get; set; } = new();
    }

    public class CollabRowModel
    {
        public string FullName { get; set; } = "";
        public string Initials { get; set; } = "";
        public string ProjectLabel { get; set; } = "";
        public string Status { get; set; } = "";
        public bool IsActive { get; set; }
    }
}
