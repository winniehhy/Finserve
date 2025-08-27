using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using FinserveNew.Models;
using FinserveNew.Models.ValidationAttributes;

namespace FinserveNew.Models.ViewModels
{
    // Models/ViewModels/PayrollProcessViewModel.cs
    [PayrollValidation(ErrorMessage = "Payroll data validation failed. Please check the contribution amounts.")]
    public class PayrollProcessViewModel
    {
        // Selection fields
        [Required(ErrorMessage = "Please select an employee")]
        public string EmployeeID { get; set; }

        [Required(ErrorMessage = "Month is required")]
        [Range(1, 12, ErrorMessage = "Please select a valid month")]
        public int Month { get; set; }

        [Required(ErrorMessage = "Year is required")]
        [Range(2000, 2100, ErrorMessage = "Please enter a valid year")]
        public int Year { get; set; }

        public bool SameAsPreviousMonth { get; set; }

        //[Required(ErrorMessage = "Project name is required")]
        [MaxLength(100, ErrorMessage = "Project name cannot exceed 100 characters")]
        public string? ProjectName { get; set; }

        // Salary details
        [Required(ErrorMessage = "Basic salary is required")]
        [Range(1000.00, 999999.99, ErrorMessage = "Basic salary must be between RM 1,000.00 and RM 999,999.99")]
        [Display(Name = "Basic Salary")]
        public decimal BasicSalary { get; set; }

        // Employer contributions
        [Display(Name = "Employer EPF (13%)")]
        [Range(0, 999999.99, ErrorMessage = "Must be between RM 0 and RM 999,999.99")]
        public decimal EmployerEpf { get; set; }

        [Display(Name = "Employer SOCSO")]
        [Range(0, 999999.99, ErrorMessage = "Must be between RM 0 and RM 999,999.99")]
        public decimal EmployerSocso { get; set; }

        [Display(Name = "Employer EIS")]
        [Range(0, 999999.99, ErrorMessage = "Must be between RM 0 and RM 999,999.99")]
        public decimal EmployerEis { get; set; }

        [Display(Name = "Employer Tax")]
        [Range(0, 999999.99, ErrorMessage = "Must be between RM 0 and RM 999,999.99")]
        public decimal EmployerTax { get; set; }

        [Display(Name = "Other Contributions")]
        [Range(0, 999999.99, ErrorMessage = "Must be between RM 0 and RM 999,999.99")]
        public decimal EmployerOtherContributions { get; set; }

        // Employee deductions
        [Display(Name = "Employee EPF (11%)")]
        [Range(0, 999999.99, ErrorMessage = "Must be between RM 0 and RM 999,999.99")]
        public decimal EmployeeEpf { get; set; }

        [Display(Name = "Employee SOCSO")]
        [Range(0, 999999.99, ErrorMessage = "Must be between RM 0 and RM 999,999.99")]
        public decimal EmployeeSocso { get; set; }

        [Display(Name = "Employee EIS")]
        [Range(0, 999999.99, ErrorMessage = "Must be between RM 0 and RM 999,999.99")]
        public decimal EmployeeEis { get; set; }

        [Display(Name = "Employee Tax")]
        [Range(0, 999999.99, ErrorMessage = "Must be between RM 0 and RM 999,999.99")]
        public decimal EmployeeTax { get; set; }

        // Calculated fields (for display only)
        [Display(Name = "Total Wages")]
        public decimal TotalWages => BasicSalary - EmployeeEpf - EmployeeSocso - EmployeeEis - EmployeeTax;

        [Display(Name = "Total Employer Cost")]
        public decimal TotalEmployerCost =>
            BasicSalary + EmployerEpf + EmployerSocso + EmployerEis + EmployerTax + EmployerOtherContributions;

        // List for display on payroll list view
        public List<Payroll> Payrolls { get; set; } = new();

        // Lookup data for dropdowns
        public IEnumerable<Employee> Employees { get; set; } = new List<Employee>();

        // For displaying month name
        public string MonthName => System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(Month);

        // Custom validation methods
        public bool IsValidPayrollPeriod()
        {
            var payrollDate = new DateTime(Year, Month, 1);
            var currentDate = DateTime.Now;
            var maxFutureDate = currentDate.AddMonths(1);

            return payrollDate <= maxFutureDate;
        }

        public bool IsValidStatutoryContributions()
        {
            if (BasicSalary <= 0) return false;

            // Check EPF percentages
            var employerEpfPercentage = (EmployerEpf / BasicSalary) * 100;
            var employeeEpfPercentage = (EmployeeEpf / BasicSalary) * 100;

            if (employerEpfPercentage > 20 || employeeEpfPercentage > 15)
                return false;

            // Check total deductions
            var totalDeductions = EmployeeEpf + EmployeeSocso + EmployeeEis + EmployeeTax;
            if (totalDeductions >= BasicSalary)
                return false;

            // Check minimum net salary (at least 50% of basic)
            var netSalary = BasicSalary - totalDeductions;
            if (netSalary < (BasicSalary * 0.5m))
                return false;

            return true;
        }
    }

}