using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StudySync.Models
{
    public class PartnershipFeedback
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int FeedbackID { get; set; }

        [Required]
        public int PartnershipID { get; set; }

        [Required]
        public int GiverUserID { get; set; }

        // SMALLINT in SQL Server — must be short in C#
        [Required]
        [Range(1, 5)]
        public short Rating { get; set; }

        [Range(1, 5)]
        public short? LearningStyleAlignment { get; set; }

        [Range(1, 5)]
        public short? CommunicationQuality { get; set; }

        [Range(1, 5)]
        public short? TechnicalProficiency { get; set; }

        public string? Comment { get; set; }

        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [ForeignKey("PartnershipID")]
        public Partnership? Partnership { get; set; }

        [ForeignKey("GiverUserID")]
        public User? Giver { get; set; }
    }
}