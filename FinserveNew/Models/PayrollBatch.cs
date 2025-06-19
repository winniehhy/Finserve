using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FinserveNew.Models
{
    public class PayrollBatch
    {
        [Key]
        public int PayrollBatchId { get; set; }
        [Required]
        public int Year { get; set; }
        [Required]
        public int Month { get; set; }
        [Required]
        public string Status { get; set; } = "Draft"; // Draft, Pending, Approved, Processed

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public virtual ICollection<PayrollRecord> PayrollRecords { get; set; } = new List<PayrollRecord>();
    }
} 