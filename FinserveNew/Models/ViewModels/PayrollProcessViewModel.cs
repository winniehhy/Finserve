using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using FinserveNew.Models;

namespace FinserveNew.Models.ViewModels
{
    // Models/ViewModels/PayrollProcessViewModel.cs
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
        public string? ProjectName { get; set; }

        // Salary details
        [Required(ErrorMessage = "Basic salary is required")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Basic salary must be greater than zero")]
        public decimal BasicSalary { get; set; }

        // Employer contributions
        [Display(Name = "Employer EPF (13%)")]
        [Range(0, double.MaxValue, ErrorMessage = "Must be a positive number")]
        public decimal EmployerEpf { get; set; }

        [Display(Name = "Employer SOCSO")]
        [Range(0, double.MaxValue, ErrorMessage = "Must be a positive number")]
        public decimal EmployerSocso { get; set; }

        [Display(Name = "Employer EIS")]
        [Range(0, double.MaxValue, ErrorMessage = "Must be a positive number")]
        public decimal EmployerEis { get; set; }

        [Display(Name = "Employer Tax")]
        [Range(0, double.MaxValue, ErrorMessage = "Must be a positive number")]
        public decimal EmployerTax { get; set; }

        [Display(Name = "Other Contributions")]
        [Range(0, double.MaxValue, ErrorMessage = "Must be a positive number")]
        public decimal EmployerOtherContributions { get; set; }

        // Employee deductions
        [Display(Name = "Employee EPF (11%)")]
        [Range(0, double.MaxValue, ErrorMessage = "Must be a positive number")]
        public decimal EmployeeEpf { get; set; }

        [Display(Name = "Employee SOCSO")]
        [Range(0, double.MaxValue, ErrorMessage = "Must be a positive number")]
        public decimal EmployeeSocso { get; set; }

        [Display(Name = "Employee EIS")]
        [Range(0, double.MaxValue, ErrorMessage = "Must be a positive number")]
        public decimal EmployeeEis { get; set; }

        [Display(Name = "Employee Tax")]
        [Range(0, double.MaxValue, ErrorMessage = "Must be a positive number")]
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
    }

}