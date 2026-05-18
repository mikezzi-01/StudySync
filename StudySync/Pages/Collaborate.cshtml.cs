using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using StudySync.Data;
using StudySync.Models;
using StudySync.Services;
using System.Security.Claims;

namespace StudySync.Pages
{
    // ── Supporting models ─────────────────────────────────────────────────
    public class MessageViewModel
    {
        public string Content { get; set; } = "";
        public DateTime SentAt { get; set; }
        public bool IsOwn { get; set; }
    }

    public class PartnerSummary
    {
        public int PartnershipId { get; set; }
        public string FullName { get; set; } = "";
        public string Initials { get; set; } = "";
        public int MatchScore { get; set; }
        public string LastMessage { get; set; } = "";
    }

    public class SessionViewModel
    {
        public string Title { get; set; } = "";
        public DateTime ScheduledAt { get; set; }
        public int DurationMinutes { get; set; }
    }

    public class SendMessageBody { public string Content { get; set; } = ""; }
    public class SaveNotesBody { public string Notes { get; set; } = ""; }
    public class CreateSessionBody
    {
        public string Title { get; set; } = "";
        public DateTime ScheduledAt { get; set; }
        public int DurationMinutes { get; set; }
    }

    // ── Page model ────────────────────────────────────────────────────────
    public class CollaborateModel : StudySyncPageModel
    {
        private readonly StudySyncDbContext _db;
        private readonly JwtService _jwt;

        public CollaborateModel(StudySyncDbContext db, JwtService jwt) : base(db, jwt)
        {
            _db = db;
            _jwt = jwt;
        }

        // ── Page properties ──────────────────────────────────────────────
        public string UserInitials { get; set; } = "";
        public string FullName { get; set; } = "";
        public int PartnershipId { get; set; }
        public string PartnerName { get; set; } = "";
        public string PartnerInitials { get; set; } = "";
        public string PartnerRole { get; set; } = "";
        public string SharedNotes { get; set; } = "";

        public Partnership? Partnership { get; set; }
        public List<MessageViewModel> Messages { get; set; } = new();
        public List<PartnerSummary> AllPartnerships { get; set; } = new();
        public List<SessionViewModel> Sessions { get; set; } = new();

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
        public async Task<IActionResult> OnGetAsync(int partnershipId)
        {
            var userId = GetUserIdFromCookie();
            if (userId == null) return RedirectToPage("/Login");

            PartnershipId = partnershipId;

            var user = await _db.Users.FindAsync(userId);
            if (user == null) return RedirectToPage("/Login");

            FullName = $"{user.FirstName} {user.LastName}".Trim();
            UserInitials = MakeInitials(user.FirstName, user.LastName);

            await PopulateLayoutAsync(userId.Value);
            ViewData["ActivePage"] = "Collaborate";
            ViewData["ShowSearch"] = false;
            ViewData["Title"] = "Collaborate";

            // Load this specific partnership
            Partnership = await _db.Partnerships
                .Include(p => p.User1)
                .Include(p => p.User2)
                .FirstOrDefaultAsync(p => p.PartnershipID == partnershipId &&
                                         (p.User1ID == userId || p.User2ID == userId) &&
                                         (p.Status == "Active" || p.Status == "Accepted"));

            if (Partnership != null)
            {
                var partner = Partnership.User1ID == userId ? Partnership.User2! : Partnership.User1!;
                PartnerName = $"{partner.FirstName} {partner.LastName}".Trim();
                PartnerInitials = MakeInitials(partner.FirstName, partner.LastName);
                PartnerRole = "Study Partner";

                // Load messages from shared notes field (stored as JSON in ClosureReason temporarily)
                // In a real system this would be a separate Messages table
                // For now we use a simple in-memory list from the DB notes field
                SharedNotes = Partnership.ClosureReason ?? "";

                // Load messages — stored as pipe-delimited in a separate approach
                // We use a simple message store via the AvailabilityVector concept
                // For the project we'll create simple text-based messages
                Messages = await LoadMessages(partnershipId, userId.Value);

                // Load sessions
                Sessions = await LoadSessions(partnershipId);
            }

            // Load all active partnerships for the partner list sidebar
            var allActive = await _db.Partnerships
                .Where(p => (p.User1ID == userId || p.User2ID == userId) &&
                            (p.Status == "Active" || p.Status == "Accepted"))
                .Include(p => p.User1)
                .Include(p => p.User2)
                .ToListAsync();

            foreach (var p in allActive)
            {
                var partner = p.User1ID == userId ? p.User2! : p.User1!;
                var cached = await _db.RecommendationCaches
                    .FirstOrDefaultAsync(rc => rc.UserID == userId && rc.TargetUserID == partner.UserID);

                AllPartnerships.Add(new PartnerSummary
                {
                    PartnershipId = p.PartnershipID,
                    FullName = $"{partner.FirstName} {partner.LastName}".Trim(),
                    Initials = MakeInitials(partner.FirstName, partner.LastName),
                    MatchScore = cached != null ? (int)Math.Round(cached.CosineScore * 100) : 0,
                    LastMessage = "Click to open chat"
                });
            }

            return Page();
        }

        // ── Load messages ─────────────────────────────────────────────────
        private async Task<List<MessageViewModel>> LoadMessages(int partnershipId, int userId)
        {
            // Messages stored in RecommendationCache table repurposed temporarily
            // In production this would be a dedicated Messages table
            // For the project scope we use a simple approach via the DB
            var raw = await _db.Database
                .SqlQueryRaw<MessageRaw>(
                    $"SELECT Content, SentAt, SenderUserID FROM CollaborationMessages WHERE PartnershipID = {partnershipId} ORDER BY SentAt ASC"
                ).ToListAsync();

            return raw.Select(r => new MessageViewModel
            {
                Content = r.Content,
                SentAt = r.SentAt,
                IsOwn = r.SenderUserID == userId
            }).ToList();
        }

        // ── Load sessions ─────────────────────────────────────────────────
        private async Task<List<SessionViewModel>> LoadSessions(int partnershipId)
        {
            var raw = await _db.Database
                .SqlQueryRaw<SessionRaw>(
                    $"SELECT Title, ScheduledAt, DurationMinutes FROM StudySessions WHERE PartnershipID = {partnershipId} ORDER BY ScheduledAt ASC"
                ).ToListAsync();

            return raw.Select(r => new SessionViewModel
            {
                Title = r.Title,
                ScheduledAt = r.ScheduledAt,
                DurationMinutes = r.DurationMinutes
            }).ToList();
        }

        // ── POST: Send message ────────────────────────────────────────────
        public async Task<IActionResult> OnPostSendMessageAsync(
            int partnershipId, [FromBody] SendMessageBody body)
        {
            var userId = GetUserIdFromCookie();
            if (userId == null) return Unauthorized();

            if (string.IsNullOrWhiteSpace(body.Content))
                return BadRequest("Message cannot be empty.");

            await _db.Database.ExecuteSqlRawAsync(
                "INSERT INTO CollaborationMessages (PartnershipID, SenderUserID, Content, SentAt) VALUES ({0}, {1}, {2}, {3})",
                partnershipId, userId, body.Content.Trim(), DateTime.UtcNow);

            // Update last activity
            var partnership = await _db.Partnerships.FindAsync(partnershipId);
            if (partnership != null)
            {
                partnership.LastActivityAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }

            return new OkResult();
        }

        // ── POST: Save shared notes ───────────────────────────────────────
        public async Task<IActionResult> OnPostSaveNotesAsync(
            int partnershipId, [FromBody] SaveNotesBody body)
        {
            var userId = GetUserIdFromCookie();
            if (userId == null) return Unauthorized();

            var partnership = await _db.Partnerships
                .FirstOrDefaultAsync(p => p.PartnershipID == partnershipId &&
                                         (p.User1ID == userId || p.User2ID == userId));

            if (partnership == null) return NotFound();

            // Store notes in ClosureReason field temporarily
            // In production this would be a dedicated SharedNotes table
            partnership.ClosureReason = body.Notes;
            partnership.LastActivityAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return new OkResult();
        }

        // ── POST: Create session ──────────────────────────────────────────
        public async Task<IActionResult> OnPostCreateSessionAsync(
            int partnershipId, [FromBody] CreateSessionBody body)
        {
            var userId = GetUserIdFromCookie();
            if (userId == null) return Unauthorized();

            if (string.IsNullOrWhiteSpace(body.Title) || body.DurationMinutes <= 0)
                return BadRequest("Invalid session data.");

            await _db.Database.ExecuteSqlRawAsync(
                "INSERT INTO StudySessions (PartnershipID, Title, ScheduledAt, DurationMinutes, CreatedByUserID) VALUES ({0}, {1}, {2}, {3}, {4})",
                partnershipId, body.Title.Trim(), body.ScheduledAt, body.DurationMinutes, userId);

            return new OkResult();
        }
    }

    // ── Raw query result types ────────────────────────────────────────────
    public class MessageRaw
    {
        public string Content { get; set; } = "";
        public DateTime SentAt { get; set; }
        public int SenderUserID { get; set; }
    }

    public class SessionRaw
    {
        public string Title { get; set; } = "";
        public DateTime ScheduledAt { get; set; }
        public int DurationMinutes { get; set; }
    }
}
