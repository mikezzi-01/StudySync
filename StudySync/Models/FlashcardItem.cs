using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StudySync.Models
{
    public class FlashcardItem
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int CardID { get; set; }

        [Required]
        public int DeckID { get; set; }

        [Required]
        public string Question { get; set; } = string.Empty;

        [Required]
        public string Answer { get; set; } = string.Empty;

        public bool IsMastered { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("DeckID")]
        public FlashcardDeck? Deck { get; set; }
    }
}
