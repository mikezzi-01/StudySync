using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StudySync.Models
{
    public class Partnership
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int PartnershipID { get; set; }

        [Required]
        public int User1ID { get; set; }

        [Required]
        public int User2ID { get; set; }

        // Status lifecycle:
        // Suggested > Viewed > Requested > Accepted > Active > Ended > Archived
        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "Suggested";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? AcceptedAt { get; set; }

        public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;

        [MaxLength(255)]
        public string? ClosureReason { get; set; }

        // Navigation properties
        [ForeignKey("User1ID")]
        public User? User1 { get; set; }

        [ForeignKey("User2ID")]
        public User? User2 { get; set; }

        public ICollection<PartnershipFeedback> Feedbacks { get; set; }
            = new List<PartnershipFeedback>();
    }
}
