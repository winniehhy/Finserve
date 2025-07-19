using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FinserveNew.Models
{
    public class PayrollRecord
    {
        [Key]
        public int PayrollRecordId { get; set; }
        [Required]
        public int PayrollBatchId { get; set; }
        public virtual PayrollBatch PayrollBatch { get; set; }

        [Required]
        public string? EmployeeID { get; set; }
        public virtual Employee Employee { get; set; }

        public decimal BasicSalary { get; set; }
        public decimal TotalAllowances { get; set; }
        public decimal TotalContributions { get; set; }
        public decimal TotalDeductions { get; set; }
        public decimal NetSalary { get; set; }

        public string Status { get; set; } = "Draft"; // Draft, Finalized, Approved

        public virtual ICollection<PayrollComponent> Components { get; set; } = new List<PayrollComponent>();
    }
} 