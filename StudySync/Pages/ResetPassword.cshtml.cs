using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using StudySync.Data;
using System.ComponentModel.DataAnnotations;

namespace StudySync.Pages
{
    public class ResetPasswordModel : PageModel
    {
        private readonly StudySyncDbContext _db;

        public ResetPasswordModel(StudySyncDbContext db)
        {
            _db = db;
        }

        [BindProperty]
        public ResetPasswordInput Input { get; set; } = new();

        public bool ResetSuccess { get; set; }
        public string ErrorMessage { get; set; } = "";

        public IActionResult OnGet() => Page();

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            var email = Input.Email.ToLower().Trim();
            var token = Input.Token.ToUpper().Trim();

            // Find the user
            var user = await _db.Users
                .FirstOrDefaultAsync(u => u.Email == email);

            if (user == null)
            {
                ErrorMessage = "Invalid email address or reset token.";
                return Page();
            }

            // Find a valid unused token
            var resetToken = await _db.PasswordResetTokens
                .FirstOrDefaultAsync(t =>
                    t.UserID == user.UserID &&
                    t.Token == token &&
                    !t.IsUsed &&
                    t.ExpiryAt > DateTime.UtcNow);

            if (resetToken == null)
            {
                ErrorMessage = "Invalid or expired reset token. " +
                               "Please request a new one.";
                return Page();
            }

            // Update the password
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(Input.NewPassword);

            // Mark token as used
            resetToken.IsUsed = true;

            await _db.SaveChangesAsync();

            ResetSuccess = true;
            return Page();
        }
    }

    public class ResetPasswordInput
    {
        [Required(ErrorMessage = "Please enter your email address.")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please enter your reset token.")]
        [MinLength(8, ErrorMessage = "Token must be 8 characters.")]
        [MaxLength(8, ErrorMessage = "Token must be 8 characters.")]
        public string Token { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please enter a new password.")]
        [MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
        public string NewPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please confirm your new password.")]
        [Compare("NewPassword", ErrorMessage = "Passwords do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
} 