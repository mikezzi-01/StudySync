using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StudySync.Models
{
    public class CollaborationMessage
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int MessageID { get; set; }

        [Required]
        public int PartnershipID { get; set; }

        [Required]
        public int SenderUserID { get; set; }

        [Required]
        public string Content { get; set; } = string.Empty;

        public DateTime SentAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [ForeignKey("PartnershipID")]
        public Partnership? Partnership { get; set; }

        [ForeignKey("SenderUserID")]
        public User? Sender { get; set; }
    }
}