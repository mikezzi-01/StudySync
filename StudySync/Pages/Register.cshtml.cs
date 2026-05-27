using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using StudySync.Data;
using StudySync.Models;
using StudySync.Services;
using System.ComponentModel.DataAnnotations;

namespace StudySync.Pages
{
    public class RegisterModel : PageModel
    {
        private readonly StudySyncDbContext _db;
        private readonly JwtService _jwt;

        public RegisterModel(StudySyncDbContext db, JwtService jwt)
        {
            _db = db;
            _jwt = jwt;
        }

        [BindProperty]
        public RegisterInputModel Input { get; set; } = new();

        public string? ErrorMessage { get; set; }

        // ── GET ─────────────────────────────────────────────────────────
        public IActionResult OnGet()
        {
            // If already logged in, redirect to dashboard
            if (Request.Cookies.ContainsKey("ss_token"))
                return RedirectToPage("/Dashboard");

            return Page();
        }

        // ── POST ────────────────────────────────────────────────────────
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
                return Page();

            // Check for duplicate email
            bool emailExists = await _db.Users
                .AnyAsync(u => u.Email == Input.Email.ToLower().Trim());

            if (emailExists)
            {
                ErrorMessage = "An account with this email already exists.";
                return Page();
            }

            // Check for duplicate matric number
            bool matricExists = await _db.Users
                .AnyAsync(u => u.MatriculationNumber == Input.MatriculationNumber.ToUpper().Trim());

            if (matricExists)
            {
                ErrorMessage = "This matriculation number is already registered.";
                return Page();
            }

            // Split FirstName into first + last
            var nameParts = Input.FirstName.Trim().Split(' ', 2);
            var firstName = nameParts[0];
            var lastName = nameParts.Length > 1 ? nameParts[1] : string.Empty;

            // Create the user
            var user = new User
            {
                FirstName = firstName,
                LastName = lastName,
                Email = Input.Email.ToLower().Trim(),
                MatriculationNumber = Input.MatriculationNumber.ToUpper().Trim(),
                AcademicLevel = short.Parse(Input.AcademicLevel),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Input.Password),
                RegistrationDate = DateTime.UtcNow,
                IsActive = true
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            // Create an empty learner profile for the new user
            var profile = new LearnerProfile
            {
                UserID = user.UserID,
                ProfileCompletion = 0,
                LastProfileUpdate = DateTime.UtcNow
            };

            _db.LearnerProfiles.Add(profile);
            await _db.SaveChangesAsync();

            // Generate JWT and store in cookie
            var token = _jwt.GenerateToken(user);
            Response.Cookies.Append("ss_token", token, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTimeOffset.UtcNow.AddHours(24)
            });

            // Redirect to profile setup (Step 1)
            return RedirectToPage("/Profile/Setup");
        }
    }

    // ── Input Model ──────────────────────────────────────────────────────
    public class RegisterInputModel
    {
        [Required(ErrorMessage = "Full name is required.")]
        [MaxLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Matriculation number is required.")]
        [RegularExpression(@"^[A-Za-z]{2,6}/\d{2}/\d{6}$",
            ErrorMessage = "Format must be e.g. FOC/22/000001")]
        public string MatriculationNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email address is required.")]
        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please select your academic level.")]
        public string AcademicLevel { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required.")]
        [MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please confirm your password.")]
        [Compare("Password", ErrorMessage = "Passwords do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;

        //[Range(typeof(bool), "true", "true",
        //    ErrorMessage = "You must agree to the terms to continue.")]
        //public bool AgreeToTerms { get; set; }
    }
}