using FinserveNew.Models;

namespace FinserveNew.Models.ViewModels
{
    // Payroll Report ViewModels
    public class PayrollReportViewModel
    {
        public List<Payroll> Payrolls { get; set; } = new List<Payroll>();
        public List<Employee> AllEmployees { get; set; } = new List<Employee>();
        public List<int> AvailableYears { get; set; } = new List<int>();

        // Filter properties
        public string ReportType { get; set; } = "register";
        public string PeriodType { get; set; } = "monthly";
        public int? Month { get; set; }
        public int Year { get; set; } = DateTime.Now.Year;
        public string? EmployeeId { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        
        // Summary properties
        public int TotalPayrolls { get; set; }
        public decimal TotalNetWages { get; set; }
        public decimal TotalEmployerCost { get; set; }
        public decimal AverageSalary { get; set; }
        public int PendingPayrolls { get; set; }
        public int CompletedPayrolls { get; set; }
    }

    // Employee Report ViewModels
    public class EmployeeReportViewModel
    {
        public string? Department { get; set; }
        public string? Status { get; set; }
        public string? Role { get; set; }
        public DateTime? JoinDateFrom { get; set; }
        public DateTime? JoinDateTo { get; set; }
        public List<Employee> Employees { get; set; } = new List<Employee>();
        public EmployeeSummaryData Summary { get; set; } = new EmployeeSummaryData();
        public List<string> AvailableDepartments { get; set; } = new List<string>();
        public List<string> AvailableRoles { get; set; } = new List<string>();
        public List<object> StatusDistribution { get; set; } = new List<object>();
        public List<object> AgeDistribution { get; set; } = new List<object>();
        public List<object> TenureDistribution { get; set; } = new List<object>();
    }

    public class EmployeeSummaryData
    {
        public int TotalEmployees { get; set; }
        public int ActiveEmployees { get; set; }
        public int ResignedEmployees { get; set; }
        public int ConfirmedEmployees { get; set; }
        public int PendingConfirmation { get; set; }
    }

    public class EmployeeReportData
    {
        public List<Employee> Employees { get; set; } = new List<Employee>();
        public EmployeeSummaryData Summary { get; set; } = new EmployeeSummaryData();
        public List<object> StatusDistribution { get; set; } = new List<object>();
        public List<object> AgeDistribution { get; set; } = new List<object>();
        public List<object> TenureDistribution { get; set; } = new List<object>();
    }
}