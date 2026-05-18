using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using StudySync.Data;
using StudySync.Models;
using StudySync.Services;
using System.Security.Claims;

namespace StudySync.Pages
{
    public class FeedbackModel : StudySyncPageModel
    {
        private readonly StudySyncDbContext _db;
        private readonly JwtService _jwt;

        public FeedbackModel(StudySyncDbContext db, JwtService jwt) : base(db, jwt)
        {
            _db = db;
            _jwt = jwt;
        }

        // ── Page properties ──────────────────────────────────────────────
        public int PartnershipId { get; set; }
        public string? PartnerName { get; set; }
        public string PartnerInitials { get; set; } = "";
        public bool AlreadySubmitted { get; set; }
        public string ErrorMessage { get; set; } = "";

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
        public async Task<IActionResult> OnGetAsync(int partnershipId)
        {
            var userId = GetUserIdFromCookie();
            if (userId == null) return RedirectToPage("/Login");

            await PopulateLayoutAsync(userId.Value);
            ViewData["ActivePage"] = "";
            ViewData["ShowSearch"] = false;
            ViewData["Title"] = "Partnership Feedback";

            PartnershipId = partnershipId;

            // Load the partnership
            var partnership = await _db.Partnerships
                .Include(p => p.User1)
                .Include(p => p.User2)
                .FirstOrDefaultAsync(p => p.PartnershipID == partnershipId &&
                                         (p.User1ID == userId || p.User2ID == userId));

            if (partnership == null)
            {
                PartnerName = null;
                return Page();
            }

            // Identify the partner
            var partner = partnership.User1ID == userId
                ? partnership.User2!
                : partnership.User1!;

            PartnerName = $"{partner.FirstName} {partner.LastName}".Trim();
            PartnerInitials = $"{partner.FirstName[0]}{(partner.LastName?.Length > 0 ? partner.LastName[0] : ' ')}".Trim().ToUpper();

            // Check if feedback already submitted
            AlreadySubmitted = await _db.PartnershipFeedbacks
                .AnyAsync(f => f.PartnershipID == partnershipId && f.GiverUserID == userId);

            return Page();
        }

        // ── POST ─────────────────────────────────────────────────────────
        public async Task<IActionResult> OnPostAsync(
            int PartnershipId,
            int OverallRating,
            int LsaRating,
            int CqRating,
            int TpRating,
            string? Comment)
        {
            var userId = GetUserIdFromCookie();
            if (userId == null) return RedirectToPage("/Login");

            // Re-load page data in case we return Page()
            await OnGetAsync(PartnershipId);

            if (OverallRating < 1 || OverallRating > 5)
            {
                ErrorMessage = "Please select an overall rating between 1 and 5.";
                return Page();
            }

            // Check not already submitted
            var already = await _db.PartnershipFeedbacks
                .AnyAsync(f => f.PartnershipID == PartnershipId && f.GiverUserID == userId);

            if (already)
            {
                AlreadySubmitted = true;
                return Page();
            }

            // Save feedback
            var feedback = new PartnershipFeedback
            {
                PartnershipID = PartnershipId,
                GiverUserID = userId.Value,
                Rating = (short)OverallRating,
                LearningStyleAlignment = LsaRating > 0 ? (short?)LsaRating : null,
                CommunicationQuality = CqRating > 0 ? (short?)CqRating : null,
                TechnicalProficiency = TpRating > 0 ? (short?)TpRating : null,
                Comment = Comment?.Trim(),
                SubmittedAt = DateTime.UtcNow
            };

            _db.PartnershipFeedbacks.Add(feedback);

            // Update partnership status to Ended if both partners have submitted
            var bothSubmitted = await _db.PartnershipFeedbacks
                .CountAsync(f => f.PartnershipID == PartnershipId);

            if (bothSubmitted >= 1)
            {
                var partnership = await _db.Partnerships.FindAsync(PartnershipId);
                if (partnership != null && partnership.Status == "Active")
                {
                    partnership.Status = "Ended";
                }
            }

            await _db.SaveChangesAsync();

            // Redirect to dashboard with success message
            return RedirectToPage("/Dashboard", new { feedback = "submitted" });
        }
    }
}