using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StudySync.Models
{
    public class StudySession
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int SessionID { get; set; }

        [Required]
        public int PartnershipID { get; set; }

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public DateTime ScheduledAt { get; set; }

        public int DurationMinutes { get; set; } = 60;

        [Required]
        public int CreatedByUserID { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [ForeignKey("PartnershipID")]
        public Partnership? Partnership { get; set; }

        [ForeignKey("CreatedByUserID")]
        public User? CreatedBy { get; set; }
    }
}