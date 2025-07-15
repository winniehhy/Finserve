using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FinserveNew.Models
{
    public class NewPayrollRecord
    {
        [Key]
        public int PayrollId { get; set; }

        [Required]
        public string EmployeeID { get; set; }

        [Required]
        public int Month { get; set; }

        [Required]
        public int Year { get; set; }

        [Required]
        public string ProjectName { get; set; }

        [Required]
        public decimal BasicSalary { get; set; }

        // Employer contributions
        public decimal EmployerEpf { get; set; }
        public decimal EmployerSocso { get; set; }
        public decimal EmployerEis { get; set; }
        public decimal EmployerTax { get; set; }
        public decimal EmployerOtherContributions { get; set; }

        // Employee deductions
        public decimal EmployeeEpf { get; set; }
        public decimal EmployeeSocso { get; set; }
        public decimal EmployeeEis { get; set; }
        public decimal EmployeeTax { get; set; }

        // Calculated fields
        public decimal TotalWages { get; set; }
        public decimal TotalEmployerCost { get; set; }

        // Navigation property
        [ForeignKey("EmployeeID")]
        public virtual Employee Employee { get; set; }
    }
}
