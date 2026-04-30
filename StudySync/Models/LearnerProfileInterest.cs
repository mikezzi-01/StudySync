using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StudySync.Models
{
    public class LearnerProfileInterest
    {
        [Required]
        public int ProfileID { get; set; }

        [Required]
        public int InterestID { get; set; }

        // Navigation properties
        [ForeignKey("ProfileID")]
        public LearnerProfile? LearnerProfile { get; set; }

        [ForeignKey("InterestID")]
        public Interest? Interest { get; set; }
    }
}
