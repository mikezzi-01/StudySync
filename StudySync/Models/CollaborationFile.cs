using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StudySync.Models
{
    public class CollaborationFile
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int FileID { get; set; }

        [Required]
        public int PartnershipID { get; set; }

        [Required]
        public int UploaderUserID { get; set; }

        [Required]
        [MaxLength(255)]
        public string FileName { get; set; } = string.Empty;

        public long FileSize { get; set; }

        [MaxLength(100)]
        public string ContentType { get; set; } = string.Empty;

        public byte[] FileData { get; set; } = Array.Empty<byte>();

        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("PartnershipID")]
        public Partnership? Partnership { get; set; }

        [ForeignKey("UploaderUserID")]
        public User? Uploader { get; set; }
    }
}