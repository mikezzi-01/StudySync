using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StudySync.Models
{
    public class LearnerProfile
    {
        // Primary Key is also the Foreign Key to Users (enforces 1:1)
        [Key]
        [ForeignKey("User")]
        public int UserID { get; set; }

        // VARK Learning Style Scores
        [Column(TypeName = "decimal(5,2)")]
        public decimal VarkVisual { get; set; } = 0;

        [Column(TypeName = "decimal(5,2)")]
        public decimal VarkAuditory { get; set; } = 0;

        [Column(TypeName = "decimal(5,2)")]
        public decimal VarkReadWrite { get; set; } = 0;

        [Column(TypeName = "decimal(5,2)")]
        public decimal VarkKinesthetic { get; set; } = 0;

        // Study Pace: 1=Slow, 2=Moderate-Slow, 3=Moderate, 4=Moderate-Fast, 5=Fast
        public short StudyPace { get; set; } = 3;

        // Collaboration Mode: 1=Solo+Occasional, 2=Pair, 3=SmallGroup, 4=LargeGroup
        public short CollaborationMode { get; set; } = 2;

        // Interaction Type: 1=Synchronous, 2=Asynchronous, 3=Mixed
        public short InteractionType { get; set; } = 3;

        // Availability stored as comma-separated slot flags
        // e.g. "1,0,1,0,1,0,0,1,1,0,1,0,0,0,1,1,1,1,1,0,0,0,0,0,0,0,0,0"
        [MaxLength(200)]
        public string AvailabilityVector { get; set; } = string.Empty;

        // Study Habits
        // StudyConsistency: 1=Very Inconsistent ... 5=Very Consistent
        public short StudyConsistency { get; set; } = 3;

        [MaxLength(50)]
        public string? PreferredEnvironment { get; set; }

        [MaxLength(100)]
        public string? MotivationDriver { get; set; }

        // Profile metadata
        public DateTime LastProfileUpdate { get; set; } = DateTime.UtcNow;

        [Column(TypeName = "decimal(5,2)")]
        public decimal ProfileCompletion { get; set; } = 0;

        // Navigation properties
        public User? User { get; set; }

        public ICollection<LearnerProfileInterest> LearnerProfileInterests { get; set; }
            = new List<LearnerProfileInterest>();
    }
}