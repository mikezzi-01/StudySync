using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StudySync.Models
{
    public class RecommendationCache
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int CacheID { get; set; }

        [Required]
        public int UserID { get; set; }

        [Required]
        public int TargetUserID { get; set; }

        // Cosine similarity score between 0.0000 and 1.0000
        [Required]
        [Column(TypeName = "decimal(5,4)")]
        public decimal CosineScore { get; set; }

        public DateTime ComputedAt { get; set; } = DateTime.UtcNow;

        [Required]
        public DateTime ExpiryAt { get; set; }

        // Navigation properties
        [ForeignKey("UserID")]
        public User? User { get; set; }

        [ForeignKey("TargetUserID")]
        public User? TargetUser { get; set; }
    }
}
