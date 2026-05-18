using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using StudySync.Data;
using StudySync.Models;
using StudySync.Services;
using System.Security.Claims;

namespace StudySync.Pages
{
    public class ProfileModel : StudySyncPageModel
    {
        private readonly StudySyncDbContext _db;
        private readonly JwtService _jwt;
        private readonly MatchingEngineService _engine;

        public ProfileModel(StudySyncDbContext db, JwtService jwt): base(db, jwt)
        {
            _db = db;
            _jwt = jwt;
        }

        // ── Page properties ──────────────────────────────────────────────
        public string UserInitials { get; set; } = "";
        public string FullName { get; set; } = "";
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string Email { get; set; } = "";
        public string MatriculationNumber { get; set; } = "";
        public int AcademicLevel { get; set; }
        public int ProfileCompletion { get; set; }
        public int ActivePartnerships { get; set; }
        public int TotalMatches { get; set; }
        public int PendingRequestCount { get; set; }

        // Learner profile fields
        public string VarkStyle { get; set; } = "Visual";
        public int StudyPace { get; set; } = 3;
        public int CollaborationMode { get; set; } = 2;
        public int InteractionType { get; set; } = 3;
        public string PreferredEnvironment { get; set; } = "";
        public string MotivationDriver { get; set; } = "";
        public List<string> AvailabilitySlots { get; set; } = new();

        public List<Interest> AllInterests { get; set; } = new();
        public List<int> SelectedInterestIds { get; set; } = new();

        public string SuccessMessage { get; set; } = "";
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

        private string MakeInitials(string first, string? last)
            => $"{first[0]}{(last?.Length > 0 ? last[0] : ' ')}".Trim().ToUpper();

        private async Task LoadPageDataAsync(int userId)
        {
            var user = await _db.Users
                .Include(u => u.LearnerProfile)
                    .ThenInclude(lp => lp.LearnerProfileInterests)
                .FirstOrDefaultAsync(u => u.UserID == userId);

            if (user == null) return;

            FullName = $"{user.FirstName} {user.LastName}".Trim();
            FirstName = user.FirstName;
            LastName = user.LastName ?? "";
            Email = user.Email;
            MatriculationNumber = user.MatriculationNumber;
            AcademicLevel = user.AcademicLevel;
            UserInitials = MakeInitials(user.FirstName, user.LastName);
            ProfileCompletion = (int)(user.LearnerProfile?.ProfileCompletion ?? 0);

            var lp = user.LearnerProfile;
            if (lp != null)
            {
                // Determine dominant VARK
                VarkStyle = lp.VarkVisual >= lp.VarkAuditory &&
                            lp.VarkVisual >= lp.VarkKinesthetic &&
                            lp.VarkVisual >= lp.VarkReadWrite ? "Visual" :
                            lp.VarkAuditory >= lp.VarkKinesthetic &&
                            lp.VarkAuditory >= lp.VarkReadWrite ? "Auditory" :
                            lp.VarkKinesthetic >= lp.VarkReadWrite ? "Kinesthetic" : "ReadWrite";

                StudyPace = lp.StudyPace;
                CollaborationMode = lp.CollaborationMode;
                InteractionType = lp.InteractionType;
                PreferredEnvironment = lp.PreferredEnvironment ?? "";
                MotivationDriver = lp.MotivationDriver ?? "";
                AvailabilitySlots = string.IsNullOrEmpty(lp.AvailabilityVector)
                    ? new List<string>()
                    : lp.AvailabilityVector.Split(',').ToList();

                SelectedInterestIds = lp.LearnerProfileInterests
                    .Select(lpi => lpi.InterestID).ToList();
            }

            AllInterests = await _db.Interests
                .OrderBy(i => i.Category).ThenBy(i => i.InterestName)
                .ToListAsync();

            ActivePartnerships = await _db.Partnerships
                .CountAsync(p => (p.User1ID == userId || p.User2ID == userId)
                              && p.Status == "Active");

            TotalMatches = await _db.RecommendationCaches
                .CountAsync(rc => rc.UserID == userId && rc.ExpiryAt > DateTime.UtcNow);

            PendingRequestCount = await _db.Partnerships
                .CountAsync(p => p.User2ID == userId && p.Status == "Requested");
        }

        // ── GET ──────────────────────────────────────────────────────────
        public async Task<IActionResult> OnGetAsync(string? success = null)
        {
            var userId = GetUserIdFromCookie();
            if (userId == null) return RedirectToPage("/Login");

            await PopulateLayoutAsync(userId.Value);
            ViewData["ActivePage"] = "Profile";
            ViewData["ShowSearch"] = false;
            ViewData["Title"] = "My Profile";

            await LoadPageDataAsync(userId.Value);

            if (success == "personal") SuccessMessage = "Personal information updated successfully.";
            if (success == "learning") SuccessMessage = "Learning preferences updated successfully.";
            if (success == "interests") SuccessMessage = "Interests updated successfully.";
            if (success == "availability") SuccessMessage = "Availability updated successfully.";
            if (success == "password") SuccessMessage = "Password changed successfully.";

            return Page();
        }

        // ── POST: Update personal info ────────────────────────────────────
        public async Task<IActionResult> OnPostUpdatePersonalAsync(
            string FirstName, string LastName, string Email,
            string AcademicLevel, string PreferredEnvironment, string MotivationDriver)
        {
            var userId = GetUserIdFromCookie();
            if (userId == null) return RedirectToPage("/Login");

            var user = await _db.Users.FindAsync(userId);
            if (user == null) return RedirectToPage("/Login");

            // Check email not taken by another user
            var emailTaken = await _db.Users
                .AnyAsync(u => u.Email == Email.ToLower().Trim() && u.UserID != userId);

            if (emailTaken)
            {
                await LoadPageDataAsync(userId.Value);
                ErrorMessage = "This email is already in use by another account.";
                return Page();
            }

            user.FirstName = FirstName.Trim();
            user.LastName = LastName.Trim();
            user.Email = Email.ToLower().Trim();
            user.AcademicLevel = short.Parse(AcademicLevel);

            var profile = await _db.LearnerProfiles.FindAsync(userId);
            if (profile != null)
            {
                profile.PreferredEnvironment = PreferredEnvironment;
                profile.MotivationDriver = MotivationDriver;
                profile.LastProfileUpdate = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();
            _ = Task.Run(() => _engine.TriggerMatchesForUserAsync(userId.Value));
            return RedirectToPage(new { success = "personal" });
        }

        // ── POST: Update learning style ───────────────────────────────────
        public async Task<IActionResult> OnPostUpdateLearningAsync(
            string VarkStyle, string StudyPace,
            string CollaborationMode, string InteractionType)
        {
            var userId = GetUserIdFromCookie();
            if (userId == null) return RedirectToPage("/Login");

            var profile = await _db.LearnerProfiles.FindAsync(userId);
            if (profile == null) return RedirectToPage("/Login");

            profile.VarkVisual = VarkStyle == "Visual" ? 100 : 0;
            profile.VarkAuditory = VarkStyle == "Auditory" ? 100 : 0;
            profile.VarkKinesthetic = VarkStyle == "Kinesthetic" ? 100 : 0;
            profile.VarkReadWrite = VarkStyle == "ReadWrite" ? 100 : 0;
            profile.StudyPace = short.Parse(StudyPace);
            profile.CollaborationMode = short.Parse(CollaborationMode);
            profile.InteractionType = short.Parse(InteractionType);
            profile.LastProfileUpdate = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            _ = Task.Run(() => _engine.TriggerMatchesForUserAsync(userId.Value));
            return RedirectToPage(new { success = "learning" });
        }

        // ── POST: Update interests ────────────────────────────────────────
        public async Task<IActionResult> OnPostUpdateInterestsAsync(
            List<int> SelectedInterestIds)
        {
            var userId = GetUserIdFromCookie();
            if (userId == null) return RedirectToPage("/Login");

            if (SelectedInterestIds.Count < 3)
            {
                await LoadPageDataAsync(userId.Value);
                ErrorMessage = "Please select at least 3 interests.";
                return Page();
            }

            var oldInterests = _db.LearnerProfileInterests
                .Where(lpi => lpi.ProfileID == userId);
            _db.LearnerProfileInterests.RemoveRange(oldInterests);

            foreach (var id in SelectedInterestIds)
            {
                _db.LearnerProfileInterests.Add(new LearnerProfileInterest
                {
                    ProfileID = userId.Value,
                    InterestID = id
                });
            }

            await _db.SaveChangesAsync();
            _ = Task.Run(() => _engine.TriggerMatchesForUserAsync(userId.Value));
            return RedirectToPage(new { success = "interests" });
        }

        // ── POST: Update availability ─────────────────────────────────────
        public async Task<IActionResult> OnPostUpdateAvailabilityAsync(
            List<string> AvailabilitySlots)
        {
            var userId = GetUserIdFromCookie();
            if (userId == null) return RedirectToPage("/Login");

            var profile = await _db.LearnerProfiles.FindAsync(userId);
            if (profile == null) return RedirectToPage("/Login");

            profile.AvailabilityVector = string.Join(",", AvailabilitySlots);
            profile.LastProfileUpdate = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            _ = Task.Run(() => _engine.TriggerMatchesForUserAsync(userId.Value));
            return RedirectToPage(new { success = "availability" });
        }

        // ── POST: Update password ─────────────────────────────────────────
        public async Task<IActionResult> OnPostUpdatePasswordAsync(
            string CurrentPassword, string NewPassword, string ConfirmNewPassword)
        {
            var userId = GetUserIdFromCookie();
            if (userId == null) return RedirectToPage("/Login");

            var user = await _db.Users.FindAsync(userId);
            if (user == null) return RedirectToPage("/Login");

            await LoadPageDataAsync(userId.Value);

            if (!BCrypt.Net.BCrypt.Verify(CurrentPassword, user.PasswordHash))
            {
                ErrorMessage = "Current password is incorrect.";
                return Page();
            }

            if (NewPassword != ConfirmNewPassword)
            {
                ErrorMessage = "New passwords do not match.";
                return Page();
            }

            if (NewPassword.Length < 8)
            {
                ErrorMessage = "New password must be at least 8 characters.";
                return Page();
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(NewPassword);
            await _db.SaveChangesAsync();
        
            return RedirectToPage(new { success = "password" });
        }
    }
}