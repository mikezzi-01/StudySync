using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using StudySync.Data;
using StudySync.Models;
using StudySync.Services;
using System.Security.Claims;

namespace StudySync.Pages.Profile
{
    public class SetupModel : PageModel
    {
        private readonly StudySyncDbContext _db;
        private readonly JwtService _jwt;
        private readonly MatchingEngineService _engine;

        public SetupModel(StudySyncDbContext db, JwtService jwt, MatchingEngineService engine)
        {
            _db = db;
            _jwt = jwt;
            _engine = engine;
        }

        // ── Page state ───────────────────────────────────────────────────
        public int CurrentStep { get; set; } = 1;
        public string UserInitials { get; set; } = "?";
        public string ErrorMessage { get; set; } = string.Empty;

        public List<Interest> AvailableInterests { get; set; } = new();

        [BindProperty]
        public SetupInputModel Input { get; set; } = new();

        // ── Helpers ──────────────────────────────────────────────────────
        private int? GetUserIdFromCookie()
        {
            var token = Request.Cookies["ss_token"];
            if (string.IsNullOrEmpty(token)) return null;

            var principal = _jwt.ValidateToken(token);
            var idClaim = principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(idClaim, out var id) ? id : null;
        }

        // ── GET ──────────────────────────────────────────────────────────
        public async Task<IActionResult> OnGetAsync(int step = 1)
        {
            var userId = GetUserIdFromCookie();
            if (userId == null) return RedirectToPage("/Login");

            var user = await _db.Users.FindAsync(userId);
            if (user == null) return RedirectToPage("/Login");

            UserInitials = $"{user.FirstName[0]}{user.LastName?.FirstOrDefault() ?? ' '}".Trim().ToUpper();
            CurrentStep = step;
            AvailableInterests = await _db.Interests.OrderBy(i => i.Category).ThenBy(i => i.InterestName).ToListAsync();

            return Page();
        }

        // ── POST ─────────────────────────────────────────────────────────
        public async Task<IActionResult> OnPostAsync(string Action, int CurrentStep)
        {
            var userId = GetUserIdFromCookie();
            if (userId == null) return RedirectToPage("/Login");

            var user = await _db.Users.FindAsync(userId);
            if (user == null) return RedirectToPage("/Login");

            UserInitials = $"{user.FirstName[0]}{user.LastName?.FirstOrDefault() ?? ' '}".Trim().ToUpper();
            AvailableInterests = await _db.Interests.OrderBy(i => i.Category).ThenBy(i => i.InterestName).ToListAsync();

            // Navigate back without saving
            if (Action == "Back")
            {
                this.CurrentStep = CurrentStep - 1;
                return Page();
            }

            // Save current step data
            var profile = await _db.LearnerProfiles.FindAsync(userId);
            if (profile == null)
            {
                profile = new LearnerProfile { UserID = userId.Value };
                _db.LearnerProfiles.Add(profile);
            }

            switch (CurrentStep)
            {
                case 1:
                    profile.PreferredEnvironment = Input.PreferredEnvironment;
                    profile.MotivationDriver = Input.MotivationDriver;
                    break;

                case 2:
                    // Convert VARK radio to scores
                    profile.VarkVisual = Input.VarkStyle == "Visual" ? 100 : 0;
                    profile.VarkAuditory = Input.VarkStyle == "Auditory" ? 100 : 0;
                    profile.VarkKinesthetic = Input.VarkStyle == "Kinesthetic" ? 100 : 0;
                    profile.VarkReadWrite = Input.VarkStyle == "ReadWrite" ? 100 : 0;
                    profile.StudyPace = short.TryParse(Input.StudyPace, out var pace) ? pace : (short)3;
                    break;

                case 3:
                    profile.StudyConsistency = short.TryParse(Input.StudyConsistencyStr, out var cons) ? cons : (short)3;
                    profile.CollaborationMode = short.TryParse(Input.CollaborationMode, out var coll) ? coll : (short)2;
                    profile.InteractionType = short.TryParse(Input.InteractionType, out var inter) ? inter : (short)3;
                    break;

                case 4:
                    if (Input.SelectedInterestIds.Count < 3)
                    {
                        ErrorMessage = "Please select at least 3 interests.";
                        this.CurrentStep = 4;
                        return Page();
                    }

                    // Remove old interests and replace
                    var oldInterests = _db.LearnerProfileInterests.Where(x => x.ProfileID == userId);
                    _db.LearnerProfileInterests.RemoveRange(oldInterests);

                    foreach (var interestId in Input.SelectedInterestIds)
                    {
                        _db.LearnerProfileInterests.Add(new LearnerProfileInterest
                        {
                            ProfileID = userId.Value,
                            InterestID = interestId
                        });
                    }
                    break;

                case 5:
                    // Store availability as comma-separated slot keys
                    profile.AvailabilityVector = string.Join(",", Input.AvailabilitySlots);
                    profile.MotivationDriver = Input.PreferredSession; // reuse field for session pref
                    break;
            }

            // Update completion percentage
            profile.ProfileCompletion = CurrentStep * 20m; // 20% per step
            profile.LastProfileUpdate = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            // Move to next step or finish
            if (Action == "Complete")
            {
                profile.ProfileCompletion = 100;
                await _db.SaveChangesAsync();

                // Wait for trigger to complete before redirecting
                await _engine.TriggerMatchesForUserAsync(userId.Value);

                // Small delay to allow Python engine to finish computing
                await Task.Delay(3000);

                return RedirectToPage("/Dashboard");
            }

            this.CurrentStep = CurrentStep + 1;
            return Page();
        }
    }

    // ── Input Model ──────────────────────────────────────────────────────
    public class SetupInputModel
    {
        // Step 1
        public string? Department { get; set; }
        public string? Bio { get; set; }
        public string? PreferredEnvironment { get; set; }
        public string? MotivationDriver { get; set; }

        // Step 2
        public string VarkStyle { get; set; } = "Visual";
        public string StudyPace { get; set; } = "3";

        // Step 3
        public string StudyConsistencyStr { get; set; } = "3";
        public string CollaborationMode { get; set; } = "2";
        public string InteractionType { get; set; } = "3";

        // Step 4
        public List<int> SelectedInterestIds { get; set; } = new();

        // Step 5
        public List<string> AvailabilitySlots { get; set; } = new();
        public string PreferredSession { get; set; } = "DeepWork";
    }
}
