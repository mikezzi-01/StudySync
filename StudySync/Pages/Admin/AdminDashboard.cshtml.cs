using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using StudySync.Data;
using StudySync.Services;

namespace StudySync.Pages.Admin
{
    // ── View models ───────────────────────────────────────────────────────
    public class AdminUserViewModel
    {
        public int UserID { get; set; }
        public string FullName { get; set; } = "";
        public string Initials { get; set; } = "";
        public string Email { get; set; } = "";
        public string MatricNumber { get; set; } = "";
        public int AcademicLevel { get; set; }
        public int ProfileCompletion { get; set; }
        public DateTime RegisteredAt { get; set; }
        public bool IsActive { get; set; }
        public bool IsAdmin { get; set; }
    }

    public class PartnershipStatViewModel
    {
        public string Status { get; set; } = "";
        public int Count { get; set; }
    }

    public class RecentPartnershipViewModel
    {
        public string User1Name { get; set; } = "";
        public string User1Initials { get; set; } = "";
        public string User2Name { get; set; } = "";
        public string User2Initials { get; set; } = "";
        public string Status { get; set; } = "";
        public DateTime CreatedAt { get; set; }
    }

    public class TopMatchViewModel
    {
        public string User1Name { get; set; } = "";
        public string User2Name { get; set; } = "";
        public int Score { get; set; }
    }

    // ── Page model ────────────────────────────────────────────────────────
    public class AdminDashboardModel : StudySyncPageModel
    {
        private readonly MatchingEngineService _engine;

        public AdminDashboardModel(
            StudySyncDbContext db,
            JwtService jwt,
            MatchingEngineService engine)
            : base(db, jwt)
        {
            _engine = engine;
        }

        // ── Page properties ──────────────────────────────────────────────
        public string SuccessMessage { get; set; } = "";
        public string ErrorMessage { get; set; } = "";

        // Metrics
        public int TotalUsers { get; set; }
        public int ActiveUsers { get; set; }
        public int TotalPartnerships { get; set; }
        public int TotalMessages { get; set; }
        public int TotalRecommendations { get; set; }
        public double AverageRating { get; set; }
        public int UsersWithMatches { get; set; }
        public int UsersWithoutMatches { get; set; }
        public double AverageMatchScore { get; set; }
        public int ExpiredCacheEntries { get; set; }
        public int TotalFeedbacks { get; set; }

        // Lists
        public List<AdminUserViewModel> Users { get; set; } = new();
        public List<PartnershipStatViewModel> PartnershipStats { get; set; } = new();
        public List<RecentPartnershipViewModel> RecentPartnerships { get; set; } = new();
        public List<TopMatchViewModel> TopMatchScores { get; set; } = new();

        // ── Helper ───────────────────────────────────────────────────────
        private string MakeInitials(string first, string? last)
            => $"{first[0]}{(last?.Length > 0 ? last[0] : ' ')}".Trim().ToUpper();

        private async Task<bool> IsAdminAsync()
        {
            var userId = GetCurrentUserId();
            if (userId == null) return false;
            var user = await Db.Users.FindAsync(userId);
            return user?.IsAdmin ?? false;
        }

        // ── GET ──────────────────────────────────────────────────────────
        public async Task<IActionResult> OnGetAsync(string? message = null)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return RedirectToPage("/Login");

            if (!await IsAdminAsync())
                return RedirectToPage("/Dashboard");

            await PopulateLayoutAsync(userId.Value);
            ViewData["Title"] = "Admin Dashboard";
            ViewData["ActivePage"] = "";
            ViewData["ShowSearch"] = false;

            if (message == "recomputed")
                SuccessMessage = "Match recomputation triggered for all users.";

            // ── Metrics ───────────────────────────────────────────────────
            TotalUsers = await Db.Users.CountAsync();
            ActiveUsers = await Db.Users.CountAsync(u => u.IsActive);

            TotalPartnerships = await Db.Partnerships.CountAsync();
            TotalMessages = await Db.CollaborationMessages.CountAsync();

            TotalRecommendations = await Db.RecommendationCaches.CountAsync();
            ExpiredCacheEntries = await Db.RecommendationCaches
                .CountAsync(rc => rc.ExpiryAt < DateTime.UtcNow);

            TotalFeedbacks = await Db.PartnershipFeedbacks.CountAsync();
            AverageRating = TotalFeedbacks > 0
                ? await Db.PartnershipFeedbacks.AverageAsync(f => (double)f.Rating)
                : 0;

            // Match stats
            var cacheUserIds = await Db.RecommendationCaches
                .Where(rc => rc.ExpiryAt > DateTime.UtcNow)
                .Select(rc => rc.UserID)
                .Distinct()
                .ToListAsync();

            UsersWithMatches = cacheUserIds.Count;
            UsersWithoutMatches = TotalUsers - UsersWithMatches;

            AverageMatchScore = await Db.RecommendationCaches
                .Where(rc => rc.ExpiryAt > DateTime.UtcNow)
                .AverageAsync(rc => (double?)rc.CosineScore * 100) ?? 0;

            // ── Users list ────────────────────────────────────────────────
            var users = await Db.Users
                .Include(u => u.LearnerProfile)
                .OrderByDescending(u => u.RegistrationDate)
                .ToListAsync();

            Users = users.Select(u => new AdminUserViewModel
            {
                UserID = u.UserID,
                FullName = $"{u.FirstName} {u.LastName}".Trim(),
                Initials = MakeInitials(u.FirstName, u.LastName),
                Email = u.Email,
                MatricNumber = u.MatriculationNumber,
                AcademicLevel = u.AcademicLevel,
                ProfileCompletion = (int)(u.LearnerProfile?.ProfileCompletion ?? 0),
                RegisteredAt = u.RegistrationDate,
                IsActive = u.IsActive,
                IsAdmin = u.IsAdmin
            }).ToList();

            // ── Partnership stats ─────────────────────────────────────────
            var pStats = await Db.Partnerships
                .GroupBy(p => p.Status)
                .Select(g => new PartnershipStatViewModel
                {
                    Status = g.Key,
                    Count = g.Count()
                })
                .ToListAsync();

            PartnershipStats = pStats;

            // ── Recent partnerships ───────────────────────────────────────
            var recent = await Db.Partnerships
                .Include(p => p.User1)
                .Include(p => p.User2)
                .OrderByDescending(p => p.CreatedAt)
                .Take(8)
                .ToListAsync();

            RecentPartnerships = recent.Select(p => new RecentPartnershipViewModel
            {
                User1Name = $"{p.User1!.FirstName} {p.User1.LastName}".Trim(),
                User1Initials = MakeInitials(p.User1.FirstName, p.User1.LastName),
                User2Name = $"{p.User2!.FirstName} {p.User2.LastName}".Trim(),
                User2Initials = MakeInitials(p.User2.FirstName, p.User2.LastName),
                Status = p.Status,
                CreatedAt = p.CreatedAt
            }).ToList();

            // ── Top match scores ──────────────────────────────────────────
            var topMatches = await Db.RecommendationCaches
                .Where(rc => rc.ExpiryAt > DateTime.UtcNow)
                .OrderByDescending(rc => rc.CosineScore)
                .Take(5)
                .Include(rc => rc.User)
                .Include(rc => rc.TargetUser)
                .ToListAsync();

            TopMatchScores = topMatches.Select(rc => new TopMatchViewModel
            {
                User1Name = $"{rc.User!.FirstName} {rc.User.LastName}".Trim(),
                User2Name = $"{rc.TargetUser!.FirstName} {rc.TargetUser.LastName}".Trim(),
                Score = (int)Math.Round(rc.CosineScore * 100)
            }).ToList();

            return Page();
        }

        // ── POST: Toggle user active/inactive ─────────────────────────────
        public async Task<IActionResult> OnPostToggleUserAsync(int userId)
        {
            if (!await IsAdminAsync()) return RedirectToPage("/Dashboard");

            var user = await Db.Users.FindAsync(userId);
            if (user == null || user.IsAdmin)
                return RedirectToPage("/Admin/AdminDashboard");

            user.IsActive = !user.IsActive;
            await Db.SaveChangesAsync();

            return RedirectToPage("/Admin/AdminDashboard");
        }

        // ── POST: Trigger full recomputation ──────────────────────────────
        public async Task<IActionResult> OnPostRecomputeAllAsync()
        {
            if (!await IsAdminAsync()) return RedirectToPage("/Dashboard");

            var isRunning = await _engine.IsEngineRunningAsync();
            if (!isRunning)
            {
                ErrorMessage = "The AI Matching Engine is not running. " +
                               "Please start main.py first.";
                return await OnGetAsync();
            }

            // Trigger recomputation for all users
            var userIds = await Db.Users
                .Where(u => u.IsActive)
                .Select(u => u.UserID)
                .ToListAsync();

            foreach (var uid in userIds)
            {
                await _engine.TriggerMatchesForUserAsync(uid);
            }

            return RedirectToPage("/Admin/AdminDashboard",
                new { message = "recomputed" });
        }
    }
}
