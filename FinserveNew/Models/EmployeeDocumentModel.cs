using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System;

namespace FinserveNew.Models
{
    public class EmployeeDocument
    {
        [Key]
        public int DocumentID { get; set; }

        [Required]
        public string EmployeeID { get; set; } = null!;

        [Required]
        [MaxLength(100)]
        public string DocumentType { get; set; } = string.Empty; // e.g. "Appointment Letter", "Salary Slip"

        [Required]
        [MaxLength(255)]
        public string FileName { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        public string FilePath { get; set; } = string.Empty;

        // [Required]
        // public DateTime UploadDate { get; set; } = DateTime.UtcNow;

        [ForeignKey("EmployeeID")]
        public virtual Employee Employee { get; set; } = null!;
    }
}
