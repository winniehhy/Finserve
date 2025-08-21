using System.ComponentModel.DataAnnotations;
namespace FinserveNew.Models
{
    public class ProcessOCRSubmissionModel
    {
        [Key] // Optional - EF will recognize "Id" as primary key by convention
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string EmployeeID { get; set; } = string.Empty;
        [Required]
        [MaxLength(50)]
        public string ClaimType { get; set; } = string.Empty;
        [Required]
        [DataType(DataType.Date)]
        public DateTime ClaimDate { get; set; }
        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Claim amount must be greater than 0")]
        public decimal ClaimAmount { get; set; }
        public string? Description { get; set; }
        public string? ocrResults { get; set; }
    }
}