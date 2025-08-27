using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FinserveNew.Models;

namespace FinserveNew.Models
{
    public class Payroll
    {
        [Key]
        public string PayrollID { get; set; } = string.Empty;

        [Required]
        public string EmployeeID { get; set; } = string.Empty;

        [Required]
        [Range(1, 12, ErrorMessage = "Month must be between 1 and 12")]
        public int Month { get; set; }

        [Required]
        [Range(2000, 2100, ErrorMessage = "Year must be between 2000 and 2100")]
        public int Year { get; set; }

        //[Required]
        [MaxLength(100, ErrorMessage = "Project name cannot exceed 100 characters")]
        public string? ProjectName { get; set; }

        [Required(ErrorMessage = "Basic salary is required")]
        [Range(0.01, 999999.99, ErrorMessage = "Basic salary must be between RM 0.01 and RM 999,999.99")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal BasicSalary { get; set; }

        // Employer contributions
        [Range(0, 999999.99, ErrorMessage = "Employer EPF must be between RM 0 and RM 999,999.99")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal EmployerEpf { get; set; }

        [Range(0, 999999.99, ErrorMessage = "Employer SOCSO must be between RM 0 and RM 999,999.99")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal EmployerSocso { get; set; }

        [Range(0, 999999.99, ErrorMessage = "Employer EIS must be between RM 0 and RM 999,999.99")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal EmployerEis { get; set; }

        [Range(0, 999999.99, ErrorMessage = "Employer Tax must be between RM 0 and RM 999,999.99")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal EmployerTax { get; set; }

        [Range(0, 999999.99, ErrorMessage = "Other contributions must be between RM 0 and RM 999,999.99")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal EmployerOtherContributions { get; set; }

        // Employee deductions
        [Range(0, 999999.99, ErrorMessage = "Employee EPF must be between RM 0 and RM 999,999.99")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal EmployeeEpf { get; set; }

        [Range(0, 999999.99, ErrorMessage = "Employee SOCSO must be between RM 0 and RM 999,999.99")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal EmployeeSocso { get; set; }

        [Range(0, 999999.99, ErrorMessage = "Employee EIS must be between RM 0 and RM 999,999.99")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal EmployeeEis { get; set; }

        [Range(0, 999999.99, ErrorMessage = "Employee Tax must be between RM 0 and RM 999,999.99")]
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
        [RegularExpression("^(Pending|Pending Approval|Approved|Rejected|Completed)$", 
            ErrorMessage = "Payment status must be one of: Pending, Pending Approval, Approved, Rejected, or Completed")]
        public string PaymentStatus { get; set; } = "Pending"; // Default value

        // Navigation property
        [ForeignKey("EmployeeID")]
        public virtual Employee Employee { get; set; } = null!;

        // Navigations to approvals history
        public virtual ICollection<Approval> Approvals { get; set; } = new List<Approval>();

        // Custom validation method
        public bool IsValidPayrollData()
        {
            // Ensure total deductions don't exceed basic salary
            var totalDeductions = EmployeeEpf + EmployeeSocso + EmployeeEis + EmployeeTax;
            if (totalDeductions > BasicSalary)
                return false;

            // Ensure EPF contributions are within reasonable limits (typical max 13% employer, 11% employee)
            if (BasicSalary > 0)
            {
                var employerEpfPercentage = (EmployerEpf / BasicSalary) * 100;
                var employeeEpfPercentage = (EmployeeEpf / BasicSalary) * 100;
                
                // Allow some flexibility but flag if percentages are way off
                if (employerEpfPercentage > 20 || employeeEpfPercentage > 15)
                    return false;
            }

            return true;
        }

        // Calculate net wages after deductions
        [NotMapped]
        public decimal NetWages => BasicSalary - EmployeeEpf - EmployeeSocso - EmployeeEis - EmployeeTax;

        // Calculate total employer cost
        [NotMapped]
        public decimal TotalCost => BasicSalary + EmployerEpf + EmployerSocso + EmployerEis + EmployerTax + EmployerOtherContributions;
    }
}