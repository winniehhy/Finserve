using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FinserveNew.Models
{
    public class Payroll
    {
        [Key]
        public int PayrollID { get; set; }

        [Required]
        public string EmployeeID { get; set; }

        [Required]
        public int Month { get; set; }

        [Required]
        public int Year { get; set; }

        [Required]
        public string ProjectName { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal BasicSalary { get; set; }

        // Employer contributions
        [Column(TypeName = "decimal(18,2)")]
        public decimal EmployerEpf { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal EmployerSocso { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal EmployerEis { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal EmployerTax { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal EmployerOtherContributions { get; set; }

        // Employee deductions
        [Column(TypeName = "decimal(18,2)")]
        public decimal EmployeeEpf { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal EmployeeSocso { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal EmployeeEis { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal EmployeeTax { get; set; }

        // Calculated fields
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalWages { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalEmployerCost { get; set; }

        [Required]
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        [Required]
        [MaxLength(30)]
        public string PaymentStatus { get; set; } = "Pending"; // Default value


        // Navigation property
        [ForeignKey("EmployeeID")]
        public virtual Employee Employee { get; set; }
    }

}