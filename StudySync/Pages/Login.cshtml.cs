using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using StudySync.Data;
using StudySync.Services;
using System.ComponentModel.DataAnnotations;

namespace StudySync.Pages
{
    public class LoginModel : PageModel
    {
        private readonly StudySyncDbContext _db;
        private readonly JwtService _jwt;

        public LoginModel(StudySyncDbContext db, JwtService jwt)
        {
            _db = db;
            _jwt = jwt;
        }

        [BindProperty]
        public LoginInputModel Input { get; set; } = new();

        public string? ErrorMessage { get; set; }

        // ── GET ─────────────────────────────────────────────────────────
        public IActionResult OnGet()
        {
            if (Request.Cookies.ContainsKey("ss_token"))
                return RedirectToPage("/Dashboard");

            return Page();
        }

        // ── POST ────────────────────────────────────────────────────────
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
                return Page();

            var identifier = Input.EmailOrMatric.Trim();

            // Find user by email or matric number
            var user = await _db.Users.FirstOrDefaultAsync(u =>
                u.Email == identifier.ToLower() ||
                u.MatriculationNumber == identifier.ToUpper());

            if (user == null || !BCrypt.Net.BCrypt.Verify(Input.Password, user.PasswordHash))
            {
                ErrorMessage = "Invalid credentials. Please check your matric number and password.";
                return Page();
            }

            if (!user.IsActive)
            {
                ErrorMessage = "Your account has been deactivated. Please contact support.";
                return Page();
            }

            // Update last login timestamp
            user.LastLoginDate = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            // Generate JWT and store in cookie
            var token = _jwt.GenerateToken(user);

            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = Input.RememberMe
                    ? DateTimeOffset.UtcNow.AddDays(7)
                    : DateTimeOffset.UtcNow.AddHours(24)
            };

            Response.Cookies.Append("ss_token", token, cookieOptions);

            // Redirect to dashboard
            return RedirectToPage("/Dashboard");
        }
    }

    // ── Input Model ──────────────────────────────────────────────────────
    public class LoginInputModel
    {
        [Required(ErrorMessage = "Please enter your email or matric number.")]
        public string EmailOrMatric { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please enter your password.")]
        public string Password { get; set; } = string.Empty;

        public bool RememberMe { get; set; }
    }
}