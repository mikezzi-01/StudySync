using System.ComponentModel.DataAnnotations;

namespace StudySync.Models
{
    public class Interest
    {
        [Key]
        public int InterestID { get; set; }

        [Required]
        [MaxLength(100)]
        public string InterestName { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? Category { get; set; }

        [MaxLength(255)]
        public string? Description { get; set; }

        // Navigation property
        public ICollection<LearnerProfileInterest> LearnerProfileInterests { get; set; }
            = new List<LearnerProfileInterest>();
    }
}
