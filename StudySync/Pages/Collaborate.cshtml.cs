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
        public int MessageId { get; set; }
        public int FileId { get; set; }
        public string Content { get; set; } = "";
        public DateTime SentAt { get; set; }
        public bool IsOwn { get; set; }
        public bool IsFile { get; set; }
        public string FileSize { get; set; } = "";
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

        public CollaborateModel(StudySyncDbContext db, JwtService jwt): base(db, jwt)
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
        public int CurrentUserId { get; set; }

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

        private string FormatBytes(long bytes)
        {
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1048576) return (bytes / 1024.0).ToString("0.#") + " KB";
            return (bytes / 1048576.0).ToString("0.#") + " MB";
        }

        // ── GET ──────────────────────────────────────────────────────────
        public async Task<IActionResult> OnGetAsync(int partnershipId)
        {
            var userId = GetUserIdFromCookie();
            if (userId == null) return RedirectToPage("/Login");

            PartnershipId = partnershipId;

            var user = await _db.Users.FindAsync(userId);
            if (user == null) return RedirectToPage("/Login");

            CurrentUserId = userId.Value;

            await PopulateLayoutAsync(userId.Value);
            ViewData["ActivePage"] = "Collaborate";
            ViewData["ShowSearch"] = false;
            ViewData["Title"] = "Collaborate";

            FullName = $"{user.FirstName} {user.LastName}".Trim();
            UserInitials = MakeInitials(user.FirstName, user.LastName);

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

                Sessions = await _db.StudySessions
                    .Where(s => s.PartnershipID == partnershipId)
                    .OrderBy(s => s.ScheduledAt)
                    .Select(s => new SessionViewModel
                    {
                        Title = s.Title,
                        ScheduledAt = s.ScheduledAt,
                        DurationMinutes = s.DurationMinutes
                    })
                    .ToListAsync();
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

        public async Task<IActionResult> OnGetGetMessagesAsync(int partnershipId)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            var messages = await Db.CollaborationMessages
                .Where(m => m.PartnershipID == partnershipId)
                .OrderBy(m => m.SentAt)
                .ToListAsync();

            var files = await Db.CollaborationFiles
                .Where(f => f.PartnershipID == partnershipId)
                .OrderBy(f => f.UploadedAt)
                .ToListAsync();

            return new JsonResult(new
            {
                messages = messages.Select(m => new
                {
                    messageId = m.MessageID,
                    content = m.Content,
                    sentAt = m.SentAt.ToString("hh:mm tt"),
                    isOwn = m.SenderUserID == userId
                }),
                files = files.Select(f => new
                {
                    fileId = f.FileID,
                    fileName = f.FileName,
                    fileSize = FormatBytes(f.FileSize),
                    uploadedAt = f.UploadedAt.ToString("hh:mm tt"),
                    isOwn = f.UploaderUserID == userId
                })
            });
        }

        // ── Load messages ─────────────────────────────────────────────────
        private async Task<List<MessageViewModel>> LoadMessages(int partnershipId, int userId)
        {
            var textMessages = await _db.CollaborationMessages
                .Where(m => m.PartnershipID == partnershipId)
                .OrderBy(m => m.SentAt)
                .ToListAsync();

            var fileMessages = await _db.CollaborationFiles
                .Where(f => f.PartnershipID == partnershipId)
                .OrderBy(f => f.UploadedAt)
                .ToListAsync();

            var allMessages = new List<MessageViewModel>();

            foreach (var m in textMessages)
            {
                allMessages.Add(new MessageViewModel
                {
                    MessageId = m.MessageID,
                    Content = m.Content,
                    SentAt = m.SentAt,
                    IsOwn = m.SenderUserID == userId,
                    IsFile = false
                });
            }

            foreach (var f in fileMessages)
            {
                allMessages.Add(new MessageViewModel
                {
                    FileId = f.FileID,
                    Content = f.FileName,
                    SentAt = f.UploadedAt,
                    IsOwn = f.UploaderUserID == userId,
                    IsFile = true,
                    FileSize = FormatBytes(f.FileSize)
                });
            }

            return allMessages.OrderBy(m => m.SentAt).ToList();
        }

        // ── POST: Send message ────────────────────────────────────────────
        public async Task<IActionResult> OnPostSendMessageAsync(
            int partnershipId, [FromBody] SendMessageBody body)
        {
            var userId = GetUserIdFromCookie();
            if (userId == null) return Unauthorized();

            if (string.IsNullOrWhiteSpace(body.Content))
                return BadRequest("Message cannot be empty.");

            _db.CollaborationMessages.Add(new CollaborationMessage
            {
                PartnershipID = partnershipId,
                SenderUserID = userId.Value,
                Content = body.Content.Trim(),
                SentAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();

            // Update last activity
            var partnership = await _db.Partnerships.FindAsync(partnershipId);
            if (partnership != null)
            {
                partnership.LastActivityAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }

            return new OkResult();
        }


        // ── POST: upload file ───────────────────────────────────────
        public async Task<IActionResult> OnPostUploadFileAsync(int partnershipId, IFormFile file)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            if (file == null || file.Length == 0)
                return BadRequest("No file provided.");

            if (file.Length > 10 * 1024 * 1024)
                return BadRequest("File size exceeds 10MB limit.");

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);

            var colFile = new CollaborationFile
            {
                PartnershipID = partnershipId,
                UploaderUserID = userId.Value,
                FileName = file.FileName,
                FileSize = file.Length,
                ContentType = file.ContentType,
                FileData = ms.ToArray(),
                UploadedAt = DateTime.UtcNow
            };

            Db.CollaborationFiles.Add(colFile);

            var partnership = await Db.Partnerships.FindAsync(partnershipId);
            if (partnership != null)
                partnership.LastActivityAt = DateTime.UtcNow;

            await Db.SaveChangesAsync();

            return new JsonResult(new { fileId = colFile.FileID, fileName = file.FileName });
        }

        public async Task<IActionResult> OnGetDownloadFileAsync(int partnershipId, int fileId)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            var file = await Db.CollaborationFiles
                .FirstOrDefaultAsync(f => f.FileID == fileId &&
                                          f.PartnershipID == partnershipId);

            if (file == null) return NotFound();

            return File(file.FileData, file.ContentType, file.FileName);
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

            _db.StudySessions.Add(new StudySession
            {
                PartnershipID = partnershipId,
                Title = body.Title.Trim(),
                ScheduledAt = body.ScheduledAt,
                DurationMinutes = body.DurationMinutes,
                CreatedByUserID = userId.Value,
                CreatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();

            return new OkResult();
        }


    }

}