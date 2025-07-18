
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FinserveNew.Models
{
    public class LeaveDetailsModel
    {
        [Key]
        public int LeaveDetailID { get; set; } // Add primary key

        public int LeaveID { get; set; }

        public int LeaveTypeID { get; set; }

        [Required(ErrorMessage = "Comment is required")]
        [Display(Name = "Comment")]
        [MaxLength(500)]
        public string Comment { get; set; } = string.Empty;

        [Required(ErrorMessage = "Document path is required")]
        [Display(Name = "Document Path")]
        [MaxLength(255)]
        public string DocumentPath { get; set; } = string.Empty;

        [Display(Name = "Upload Date")]
        public DateTime UploadDate { get; set; } = DateTime.Now;

        // Navigation Properties
        [ForeignKey("LeaveID")]
        public virtual LeaveModel? Leave { get; set; } 

        [ForeignKey("LeaveTypeID")]
        public virtual LeaveTypeModel? LeaveType { get; set; }
    }
}