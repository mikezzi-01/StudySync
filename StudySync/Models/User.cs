using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StudySync.Models
{
    public class User
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int UserID { get; set; }

        [Required]
        [MaxLength(20)]
        public string MatriculationNumber { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MaxLength(256)]
        public string PasswordHash { get; set; } = string.Empty;

        [Required]
        public short AcademicLevel { get; set; }  // 100, 200, 300, 400

        public DateTime RegistrationDate { get; set; } = DateTime.UtcNow;

        public DateTime? LastLoginDate { get; set; }

        public bool IsActive { get; set; } = true;

        // Navigation properties
        public LearnerProfile? LearnerProfile { get; set; }

        public ICollection<Partnership> PartnershipAsUser1 { get; set; } = new List<Partnership>();
        public ICollection<Partnership> PartnershipAsUser2 { get; set; } = new List<Partnership>();

        public ICollection<PartnershipFeedback> FeedbackGiven { get; set; } = new List<PartnershipFeedback>();

        public ICollection<RecommendationCache> RecommendationsCached { get; set; } = new List<RecommendationCache>();
    }
}

