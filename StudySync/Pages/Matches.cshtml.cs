using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using StudySync.Data;
using StudySync.Models;
using StudySync.Services;
using System.Security.Claims;
using System.Text.Json;

namespace StudySync.Pages
{
    public class MatchesModel : PageModel
    {
        private readonly StudySyncDbContext _db;
        private readonly JwtService _jwt;

        public MatchesModel(StudySyncDbContext db, JwtService jwt)
        {
            _db = db;
            _jwt = jwt;
        }

        // ── Page properties ──────────────────────────────────────────────
        public string UserInitials { get; set; } = "";
        public string FullName { get; set; } = "";
        public int MinScore { get; set; } = 30;

        public List<MatchViewModel> Matches { get; set; } = new();
        public List<string> AllInterests { get; set; } = new();
        public string MatchesJson { get; set; } = "[]";

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

            var user = await _db.Users.FindAsync(userId);
            if (user == null) return RedirectToPage("/Login");

            FullName = $"{user.FirstName} {user.LastName}".Trim();
            UserInitials = $"{user.FirstName[0]}{(user.LastName?.Length > 0 ? user.LastName[0] : ' ')}".Trim().ToUpper();

            // Load all interests for filter panel
            AllInterests = await _db.Interests
                .OrderBy(i => i.InterestName)
                .Select(i => i.InterestName)
                .ToListAsync();

            // Load existing partnership statuses to mark already-requested matches
            var existingPartnerIds = await _db.Partnerships
                .Where(p => p.User1ID == userId || p.User2ID == userId)
                .Select(p => p.User1ID == userId ? p.User2ID : p.User1ID)
                .ToListAsync();

            // Load cached recommendations
            var cached = await _db.RecommendationCaches
                .Where(rc => rc.UserID == userId && rc.ExpiryAt > DateTime.UtcNow)
                .OrderByDescending(rc => rc.CosineScore)
                .Include(rc => rc.TargetUser)
                    .ThenInclude(u => u!.LearnerProfile)
                        .ThenInclude(lp => lp!.LearnerProfileInterests)
                            .ThenInclude(lpi => lpi.Interest)
                .ToListAsync();

            Matches = cached.Select(rc =>
            {
                var target = rc.TargetUser!;
                var profile = target.LearnerProfile;
                var interestTags = profile?.LearnerProfileInterests
                    .Select(lpi => lpi.Interest?.InterestName ?? "")
                    .Where(n => !string.IsNullOrEmpty(n))
                    .ToList() ?? new List<string>();

                return new MatchViewModel
                {
                    UserID = target.UserID,
                    FullName = $"{target.FirstName} {target.LastName}".Trim(),
                    Initials = $"{target.FirstName[0]}{(target.LastName?.Length > 0 ? target.LastName[0] : ' ')}".Trim().ToUpper(),
                    AcademicLevel = target.AcademicLevel.ToString(),
                    Score = (int)Math.Round(rc.CosineScore * 100),
                    InterestTags = interestTags,
                    Bio = profile?.MotivationDriver ?? "",
                    Availability = profile?.AvailabilityVector ?? "",
                    AlreadyRequested = existingPartnerIds.Contains(target.UserID)
                };
            }).ToList();

            // Serialise for JavaScript
            MatchesJson = JsonSerializer.Serialize(Matches.Select(m => new
            {
                userID = m.UserID,
                fullName = m.FullName,
                initials = m.Initials,
                academicLevel = m.AcademicLevel,
                score = m.Score,
                interestTags = m.InterestTags,
                bio = m.Bio,
                availability = m.Availability,
                alreadyRequested = m.AlreadyRequested
            }));

            return Page();
        }

        // ── POST: Send partnership request ───────────────────────────────
        public async Task<IActionResult> OnPostSendRequestAsync([FromBody] SendRequestBody body)
        {
            var userId = GetUserIdFromCookie();
            if (userId == null) return Unauthorized();

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

    // ── View model ────────────────────────────────────────────────────────
    public class MatchViewModel
    {
        public int UserID { get; set; }
        public string FullName { get; set; } = "";
        public string Initials { get; set; } = "";
        public string AcademicLevel { get; set; } = "";
        public int Score { get; set; }
        public List<string> InterestTags { get; set; } = new();
        public string Bio { get; set; } = "";
        public string Availability { get; set; } = "";
        public bool AlreadyRequested { get; set; }
    }

    public class SendRequestBody
    {
        public int TargetUserId { get; set; }
    }
}
