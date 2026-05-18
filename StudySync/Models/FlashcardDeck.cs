using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StudySync.Models
{
    public class FlashcardDeck
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int DeckID { get; set; }

        [Required]
        public int UserID { get; set; }

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? Topic { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("UserID")]
        public User? User { get; set; }

        public ICollection<FlashcardItem> Items { get; set; } = new List<FlashcardItem>();
    }
}
