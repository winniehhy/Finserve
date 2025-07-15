using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System;
using System.Runtime.Serialization;

namespace FinserveNew.Models.ViewModels
{

    public class NewPayrollViewModel
    {
        // Selection fields
        [Required]
        public string EmployeeID { get; set; }

        [Required]
        [Range(1, 12)]
        public int Month { get; set; }

        [Required]
        [Range(2000, 2100)]
        public int Year { get; set; }

        public bool SameAsPreviousMonth { get; set; }

        [Required]
        public string ProjectName { get; set; }

        // Salary details
        [Required]
        [Range(0, double.MaxValue)]
        public decimal BasicSalary { get; set; }

        // Employer contributions
        [Range(0, double.MaxValue)]
        public decimal EmployerEpf { get; set; }

        [Range(0, double.MaxValue)]
        public decimal EmployerSocso { get; set; }

        [Range(0, double.MaxValue)]
        public decimal EmployerEis { get; set; }

        [Range(0, double.MaxValue)]
        public decimal EmployerTax { get; set; }

        [Range(0, double.MaxValue)]
        public decimal EmployerOtherContributions { get; set; }

        // Employee deductions
        [Range(0, double.MaxValue)]
        public decimal EmployeeEpf { get; set; }

        [Range(0, double.MaxValue)]
        public decimal EmployeeSocso { get; set; }

        [Range(0, double.MaxValue)]
        public decimal EmployeeEis { get; set; }

        [Range(0, double.MaxValue)]
        public decimal EmployeeTax { get; set; }

        // Calculated fields (for display only)
        public decimal TotalWedges => BasicSalary - EmployeeEpf - EmployeeSocso - EmployeeEis - EmployeeTax;

        public decimal TotalEmployerCost =>
            BasicSalary + EmployerEpf + EmployerSocso + EmployerEis + EmployerTax + EmployerOtherContributions;

        // Lookup data
        [IgnoreDataMember]
        public IEnumerable<Employee> Employees { get; set; } = new List<Employee>();

        [IgnoreDataMember]
        public IEnumerable<StatutoryRate> StatutoryRates { get; set; } = new List<StatutoryRate>();
    }
}
