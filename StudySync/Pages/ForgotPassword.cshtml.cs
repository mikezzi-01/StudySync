using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using StudySync.Data;
using StudySync.Models;
using StudySync.Services;

namespace StudySync.Pages
{
    public class ForgotPasswordModel : PageModel
    {
        private readonly StudySyncDbContext _db;
        private readonly EmailService _email;
        private readonly ILogger<ForgotPasswordModel> _logger;

        public ForgotPasswordModel(
            StudySyncDbContext db,
            EmailService email,
            ILogger<ForgotPasswordModel> logger)
        {
            _db = db;
            _email = email;
            _logger = logger;
        }

        [BindProperty]
        public ForgotPasswordInput Input { get; set; } = new();

        public bool EmailSent { get; set; }
        public string SubmittedEmail { get; set; } = "";
        public string ResetToken { get; set; } = "";
        public string ErrorMessage { get; set; } = "";

        public IActionResult OnGet() => Page();

        // Post
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            SubmittedEmail = Input.Email.ToLower().Trim();

            var user = await _db.Users
                .FirstOrDefaultAsync(u => u.Email == SubmittedEmail);

            if (user != null)
            {
                // Invalidate any existing unused tokens for this user
                var existingTokens = await _db.PasswordResetTokens
                    .Where(t => t.UserID == user.UserID && !t.IsUsed)
                    .ToListAsync();

                foreach (var t in existingTokens)
                    t.IsUsed = true;

                // Generate a new secure token
                var token = GenerateToken();

                _db.PasswordResetTokens.Add(new PasswordResetToken
                {
                    UserID = user.UserID,
                    Token = token,
                    ExpiryAt = DateTime.UtcNow.AddMinutes(30),
                    IsUsed = false,
                    CreatedAt = DateTime.UtcNow
                });

                await _db.SaveChangesAsync();

                try
                {
                    await _email.SendPasswordResetEmailAsync(
                        recipientEmail: user.Email,
                        recipientName: $"{user.FirstName} {user.LastName}".Trim(),
                        resetToken: token
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        "[ForgotPassword] Email send failed for {Email}: {Message}",
                        user.Email, ex.Message);
                    // Still show success to prevent email enumeration
                    EmailSent = true;
                    return Page();
                }
            }

            // Always show success to prevent email enumeration
            EmailSent = true;
            return Page();
        }

        private static string GenerateToken()
        {
            // Generate a readable 8-character alphanumeric token
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, 8)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }
    }

    public class ForgotPasswordInput
    {
        [Required(ErrorMessage = "Please enter your email address.")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
        public string Email { get; set; } = string.Empty;
    }
}