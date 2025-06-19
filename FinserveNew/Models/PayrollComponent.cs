using System.ComponentModel.DataAnnotations;

namespace FinserveNew.Models
{
    public class PayrollComponent
    {
        [Key]
        public int PayrollComponentId { get; set; }
        [Required]
        public int PayrollRecordId { get; set; }
        public virtual PayrollRecord PayrollRecord { get; set; }

        [Required]
        public string Type { get; set; } // Allowance, Deduction, Contribution
        [Required]
        public string Name { get; set; } // e.g., "EPF", "SOCSO", "Housing Allowance"
        public decimal Amount { get; set; }
        public bool IsAutoCalculated { get; set; } = true;
    }
} 