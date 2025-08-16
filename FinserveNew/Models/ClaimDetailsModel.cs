using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FinserveNew.Models
{
    public class ClaimDetails
    {
        // ADD Primary Key if you don't have one
        [Key]
        public int Id { get; set; }

        public int ClaimID { get; set; }
        public int ClaimTypeID { get; set; }

        [Required(ErrorMessage = "Comment is required")]
        [Display(Name = "Comment")]
        [MaxLength(500)]
        public string Comment { get; set; } = string.Empty;

        [Required(ErrorMessage = "Document path is required")]
        [Display(Name = "Document Path")]
        [MaxLength(500)] // Increased for longer paths
        public string DocumentPath { get; set; } = string.Empty;

        // NEW: Store original file name
        [Display(Name = "Original File Name")]
        [MaxLength(255)]
        public string? OriginalFileName { get; set; }

        // NEW: Store file size
        [Display(Name = "File Size")]
        public long? FileSize { get; set; }

        // NEW: Upload timestamp
        [Display(Name = "Upload Date")]
        public DateTime UploadDate { get; set; } = DateTime.Now;

        // Navigation Properties
        [ForeignKey("ClaimID")]
        public virtual Claim Claim { get; set; } = null!;

        [ForeignKey("ClaimTypeID")]
        public virtual ClaimType ClaimType { get; set; } = null!;

        // ===== NotMapped HELPER PROPERTIES =====

        [NotMapped]
        public string DisplayFileName
        {
            get
            {
                return !string.IsNullOrEmpty(OriginalFileName) ? OriginalFileName : Path.GetFileName(DocumentPath);
            }
        }

        [NotMapped]
        public string FormattedFileSize
        {
            get
            {
                if (!FileSize.HasValue) return "Unknown";

                var size = FileSize.Value;
                if (size < 1024) return $"{size} B";
                if (size < 1024 * 1024) return $"{size / 1024:F1} KB";
                return $"{size / (1024 * 1024):F1} MB";
            }
        }

        [NotMapped]
        public string FileExtension
        {
            get
            {
                return Path.GetExtension(DocumentPath).ToLower();
            }
        }

        [NotMapped]
        public bool IsImage
        {
            get
            {
                var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp" };
                return imageExtensions.Contains(FileExtension);
            }
        }
    }
}