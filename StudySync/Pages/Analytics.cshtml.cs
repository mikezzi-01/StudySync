using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using StudySync.Data;
using StudySync.Services;
using System.Security.Claims;

namespace StudySync.Pages
{
    // ── Supporting view models ────────────────────────────────────────────
    public class BreakdownItem
    {
        public string Label { get; set; } = "";
        public int Percentage { get; set; }
    }

    public class PartnershipHistoryItem
    {
        public string PartnerName { get; set; } = "";
        public string Initials { get; set; } = "";
        public int MatchScore { get; set; }
        public string StartDate { get; set; } = "";
        public string Status { get; set; } = "";
    }

    public class MatchScoreItem
    {
        public string FullName { get; set; } = "";
        public string Initials { get; set; } = "";
        public string AcademicLevel { get; set; } = "";
        public int Score { get; set; }
    }

    // ── Page model ────────────────────────────────────────────────────────
    public class AnalyticsModel : StudySyncPageModel
    {
        private readonly StudySyncDbContext _db;
        private readonly JwtService _jwt;

        public AnalyticsModel(StudySyncDbContext db, JwtService jwt) : base(db, jwt)
        {
            _db = db;
            _jwt = jwt;
        }

        // ── Page properties ──────────────────────────────────────────────
        public string UserInitials { get; set; } = "";
        public string FullName { get; set; } = "";
        public int PendingRequestCount { get; set; }
        public int ProfileCompletion { get; set; }

        // Metrics
        public int TotalPartnerships { get; set; }
        public int ActivePartnerships { get; set; }
        public int TotalMatches { get; set; }
        public int TotalMessages { get; set; }
        public int TotalSessions { get; set; }
        public double AverageRating { get; set; }

        // Learning style
        public string StudyPaceLabel { get; set; } = "";
        public string CollaborationLabel { get; set; } = "";
        public string InteractionLabel { get; set; } = "";

        // Lists
        public List<BreakdownItem> ProfileBreakdown { get; set; } = new();
        public List<BreakdownItem> VarkBreakdown { get; set; } = new();
        public List<string> UserInterests { get; set; } = new();
        public List<PartnershipHistoryItem> PartnershipHistory { get; set; } = new();
        public List<MatchScoreItem> TopMatchScores { get; set; } = new();

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

            var user = await _db.Users
                .Include(u => u.LearnerProfile)
                    .ThenInclude(lp => lp.LearnerProfileInterests)
                        .ThenInclude(lpi => lpi.Interest)
                .FirstOrDefaultAsync(u => u.UserID == userId);

            if (user == null) return RedirectToPage("/Login");

            FullName = $"{user.FirstName} {user.LastName}".Trim();
            UserInitials = MakeInitials(user.FirstName, user.LastName);

            var lp = user.LearnerProfile;
            ProfileCompletion = (int)(lp?.ProfileCompletion ?? 0);

            await PopulateLayoutAsync(userId.Value);
            ViewData["ActivePage"] = "Analytics";
            ViewData["ShowSearch"] = false;
            ViewData["Title"] = "Analytics";

            // ── Pending requests ──────────────────────────────────────────
            PendingRequestCount = await _db.Partnerships
                .CountAsync(p => p.User2ID == userId && p.Status == "Requested");

            // ── Metrics ───────────────────────────────────────────────────
            var partnerships = await _db.Partnerships
                .Where(p => p.User1ID == userId || p.User2ID == userId)
                .ToListAsync();

            TotalPartnerships = partnerships.Count;
            ActivePartnerships = partnerships.Count(p => p.Status == "Active");

            TotalMatches = await _db.RecommendationCaches
                .CountAsync(rc => rc.UserID == userId && rc.ExpiryAt > DateTime.UtcNow);

            TotalMessages = await _db.CollaborationMessages
                .CountAsync(m => m.SenderUserID == userId);

            TotalSessions = await _db.StudySessions
                .CountAsync(s => s.CreatedByUserID == userId);

            var feedbacks = await _db.PartnershipFeedbacks
                .Where(f => f.GiverUserID == userId)
                .ToListAsync();

            AverageRating = feedbacks.Count > 0
                ? feedbacks.Average(f => f.Rating)
                : 0;

            // ── Profile breakdown ─────────────────────────────────────────
            ProfileBreakdown = new List<BreakdownItem>
            {
                new() { Label = "Personal Info",    Percentage = lp != null ? 100 : 0 },
                new() { Label = "Learning Style",   Percentage = lp?.VarkVisual > 0 || lp?.VarkAuditory > 0 ? 100 : 0 },
                new() { Label = "Study Habits",     Percentage = lp?.StudyConsistency > 0 ? 100 : 0 },
                new() { Label = "Interests",        Percentage = lp?.LearnerProfileInterests.Count > 0 ? 100 : 0 },
                new() { Label = "Availability",     Percentage = !string.IsNullOrEmpty(lp?.AvailabilityVector) ? 100 : 0 }
            };

            // ── VARK breakdown ────────────────────────────────────────────
            if (lp != null)
            {
                var total = (double)(lp.VarkVisual + lp.VarkAuditory +
                                     lp.VarkReadWrite + lp.VarkKinesthetic);

                VarkBreakdown = new List<BreakdownItem>
                {
                    new() { Label = "Visual",      Percentage = total > 0 ? (int)(lp.VarkVisual      / (decimal)total * 100) : 25 },
                    new() { Label = "Auditory",    Percentage = total > 0 ? (int)(lp.VarkAuditory    / (decimal)total * 100) : 25 },
                    new() { Label = "Read & Write",Percentage = total > 0 ? (int)(lp.VarkReadWrite   / (decimal)total * 100) : 25 },
                    new() { Label = "Kinesthetic", Percentage = total > 0 ? (int)(lp.VarkKinesthetic / (decimal)total * 100) : 25 }
                };

                StudyPaceLabel = lp.StudyPace switch
                {
                    1 => "Slow",
                    2 => "Moderate-Slow",
                    3 => "Moderate",
                    4 => "Moderate-Fast",
                    5 => "Fast",
                    _ => "Not set"
                };

                CollaborationLabel = lp.CollaborationMode switch
                {
                    1 => "Solo + Occasional Help",
                    2 => "Pair Study",
                    3 => "Small Group (3–5)",
                    4 => "Large Group",
                    _ => "Not set"
                };

                InteractionLabel = lp.InteractionType switch
                {
                    1 => "Synchronous",
                    2 => "Asynchronous",
                    3 => "Mixed",
                    _ => "Not set"
                };

                // ── Interests ─────────────────────────────────────────────
                UserInterests = lp.LearnerProfileInterests
                    .Select(lpi => lpi.Interest?.InterestName ?? "")
                    .Where(n => !string.IsNullOrEmpty(n))
                    .ToList();
            }

            // ── Partnership history ───────────────────────────────────────
            var allPartnerships = await _db.Partnerships
                .Where(p => p.User1ID == userId || p.User2ID == userId)
                .Include(p => p.User1)
                .Include(p => p.User2)
                .OrderByDescending(p => p.CreatedAt)
                .Take(5)
                .ToListAsync();

            foreach (var p in allPartnerships)
            {
                var partner = p.User1ID == userId ? p.User2! : p.User1!;
                var cached = await _db.RecommendationCaches
                    .FirstOrDefaultAsync(rc =>
                        rc.UserID == userId && rc.TargetUserID == partner.UserID);

                PartnershipHistory.Add(new PartnershipHistoryItem
                {
                    PartnerName = $"{partner.FirstName} {partner.LastName}".Trim(),
                    Initials = MakeInitials(partner.FirstName, partner.LastName),
                    MatchScore = cached != null ? (int)Math.Round(cached.CosineScore * 100) : 0,
                    StartDate = p.CreatedAt.ToString("MMM dd, yyyy"),
                    Status = p.Status
                });
            }

            // ── Top match scores ──────────────────────────────────────────
            var topMatches = await _db.RecommendationCaches
                .Where(rc => rc.UserID == userId && rc.ExpiryAt > DateTime.UtcNow)
                .OrderByDescending(rc => rc.CosineScore)
                .Take(5)
                .Include(rc => rc.TargetUser)
                .ToListAsync();

            TopMatchScores = topMatches.Select(rc => new MatchScoreItem
            {
                FullName = $"{rc.TargetUser!.FirstName} {rc.TargetUser.LastName}".Trim(),
                Initials = MakeInitials(rc.TargetUser.FirstName, rc.TargetUser.LastName),
                AcademicLevel = rc.TargetUser.AcademicLevel.ToString(),
                Score = (int)Math.Round(rc.CosineScore * 100)
            }).ToList();

            return Page();
        }
    }
}
