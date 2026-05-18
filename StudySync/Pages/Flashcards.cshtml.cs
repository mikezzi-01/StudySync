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
    // ── View models ───────────────────────────────────────────────────────
    public class DeckViewModel
    {
        public int DeckId { get; set; }
        public string Title { get; set; } = "";
        public string? Topic { get; set; }
        public int CardCount { get; set; }
        public int MasteredCount { get; set; }
    }

    // ── Request bodies ────────────────────────────────────────────────────
    public class CreateDeckBody { public string Title { get; set; } = ""; public string? Topic { get; set; } }
    public class DeleteDeckBody { public int DeckId { get; set; } }
    public class AddCardBody { public int DeckId { get; set; } public string Question { get; set; } = ""; public string Answer { get; set; } = ""; }
    public class MarkCardBody { public int CardId { get; set; } public bool Mastered { get; set; } }

    // ── Page model ────────────────────────────────────────────────────────
    public class FlashcardsModel : StudySyncPageModel
    {
        private readonly StudySyncDbContext _db;
        private readonly JwtService _jwt;

        public FlashcardsModel(StudySyncDbContext db, JwtService jwt) : base(db, jwt)
        {
            _db = db;
            _jwt = jwt;
        }

        // ── Page properties ──────────────────────────────────────────────
        public string UserInitials { get; set; } = "";
        public string FullName { get; set; } = "";
        public int PendingRequestCount { get; set; }
        public string SuccessMessage { get; set; } = "";

        public int TotalDecks { get; set; }
        public int TotalCards { get; set; }
        public int MasteredCards { get; set; }

        public List<DeckViewModel> Decks { get; set; } = new();

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
        public async Task<IActionResult> OnGetAsync(string? success = null)
        {
            var userId = GetUserIdFromCookie();
            if (userId == null) return RedirectToPage("/Login");

            var user = await _db.Users.FindAsync(userId);
            if (user == null) return RedirectToPage("/Login");

            FullName = $"{user.FirstName} {user.LastName}".Trim();
            UserInitials = MakeInitials(user.FirstName, user.LastName);

            await PopulateLayoutAsync(userId.Value);
            ViewData["ActivePage"] = "Flashcards";
            ViewData["ShowSearch"] = true;
            ViewData["SearchPlaceholder"] = "Search decks...";
            ViewData["Title"] = "Flashcards";

            PendingRequestCount = await _db.Partnerships
                .CountAsync(p => p.User2ID == userId && p.Status == "Requested");

            if (success == "created") SuccessMessage = "Deck created successfully.";
            if (success == "added") SuccessMessage = "Card added successfully.";
            if (success == "deleted") SuccessMessage = "Deck deleted.";

            // Load decks with card counts
            var decks = await _db.FlashcardDecks
                .Where(d => d.UserID == userId)
                .Include(d => d.Items)
                .OrderByDescending(d => d.CreatedAt)
                .ToListAsync();

            Decks = decks.Select(d => new DeckViewModel
            {
                DeckId = d.DeckID,
                Title = d.Title,
                Topic = d.Topic,
                CardCount = d.Items.Count,
                MasteredCount = d.Items.Count(i => i.IsMastered)
            }).ToList();

            TotalDecks = Decks.Count;
            TotalCards = Decks.Sum(d => d.CardCount);
            MasteredCards = Decks.Sum(d => d.MasteredCount);

            return Page();
        }

        // ── POST: Create deck ─────────────────────────────────────────────
        public async Task<IActionResult> OnPostCreateDeckAsync([FromBody] CreateDeckBody body)
        {
            var userId = GetUserIdFromCookie();
            if (userId == null) return Unauthorized();

            if (string.IsNullOrWhiteSpace(body.Title))
                return BadRequest("Deck title is required.");

            _db.FlashcardDecks.Add(new FlashcardDeck
            {
                UserID = userId.Value,
                Title = body.Title.Trim(),
                Topic = body.Topic?.Trim(),
                CreatedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();
            return new OkResult();
        }

        // ── POST: Delete deck ─────────────────────────────────────────────
        public async Task<IActionResult> OnPostDeleteDeckAsync([FromBody] DeleteDeckBody body)
        {
            var userId = GetUserIdFromCookie();
            if (userId == null) return Unauthorized();

            var deck = await _db.FlashcardDecks
                .FirstOrDefaultAsync(d => d.DeckID == body.DeckId && d.UserID == userId);

            if (deck == null) return NotFound();

            _db.FlashcardDecks.Remove(deck);
            await _db.SaveChangesAsync();
            return new OkResult();
        }

        // ── POST: Add card ────────────────────────────────────────────────
        public async Task<IActionResult> OnPostAddCardAsync([FromBody] AddCardBody body)
        {
            var userId = GetUserIdFromCookie();
            if (userId == null) return Unauthorized();

            // Verify deck belongs to this user
            var deck = await _db.FlashcardDecks
                .FirstOrDefaultAsync(d => d.DeckID == body.DeckId && d.UserID == userId);

            if (deck == null) return NotFound();

            if (string.IsNullOrWhiteSpace(body.Question) || string.IsNullOrWhiteSpace(body.Answer))
                return BadRequest("Question and answer are required.");

            _db.FlashcardItems.Add(new FlashcardItem
            {
                DeckID = body.DeckId,
                Question = body.Question.Trim(),
                Answer = body.Answer.Trim(),
                IsMastered = false,
                CreatedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();
            return new OkResult();
        }

        // ── POST: Mark card mastered ──────────────────────────────────────
        public async Task<IActionResult> OnPostMarkCardAsync([FromBody] MarkCardBody body)
        {
            var userId = GetUserIdFromCookie();
            if (userId == null) return Unauthorized();

            var card = await _db.FlashcardItems
                .Include(c => c.Deck)
                .FirstOrDefaultAsync(c => c.CardID == body.CardId &&
                                         c.Deck!.UserID == userId);

            if (card == null) return NotFound();

            card.IsMastered = body.Mastered;
            await _db.SaveChangesAsync();
            return new OkResult();
        }

        // ── GET: Get cards for study mode ─────────────────────────────────
        public async Task<IActionResult> OnGetGetCardsAsync(int deckId)
        {
            var userId = GetUserIdFromCookie();
            if (userId == null) return Unauthorized();

            var deck = await _db.FlashcardDecks
                .Include(d => d.Items)
                .FirstOrDefaultAsync(d => d.DeckID == deckId && d.UserID == userId);

            if (deck == null) return NotFound();

            var cards = deck.Items
                .OrderBy(_ => Guid.NewGuid()) // shuffle
                .Select(c => new
                {
                    cardId = c.CardID,
                    question = c.Question,
                    answer = c.Answer,
                    mastered = c.IsMastered
                })
                .ToList();

            return new JsonResult(cards);
        }
    }
}
