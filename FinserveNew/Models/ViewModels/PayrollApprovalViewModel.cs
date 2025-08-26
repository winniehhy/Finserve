using FinserveNew.Models;

namespace FinserveNew.Models.ViewModels
{
    public class PayrollApprovalViewModel
    {
        public List<Payroll> AllPayrolls { get; set; } = new List<Payroll>();
        public List<Payroll> PendingPayrolls { get; set; } = new List<Payroll>();
        public List<Payroll> ApprovedPayrolls { get; set; } = new List<Payroll>();
        public List<Payroll> RejectedPayrolls { get; set; } = new List<Payroll>();
        public List<ApprovalHistoryItem> AllApprovalHistory { get; set; } = new List<ApprovalHistoryItem>();

        // Search and Filter Properties
        public string SearchTerm { get; set; } = string.Empty;
        public int? FilterMonth { get; set; }
        public int? FilterYear { get; set; }
        public string FilterEmployee { get; set; } = string.Empty;
        public decimal? MinAmount { get; set; }
        public decimal? MaxAmount { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }

        // Additional Properties for Dropdowns
        public List<Employee> AllEmployees { get; set; } = new List<Employee>();
        public List<int> AvailableYears { get; set; } = new List<int>();
    }

    public class ApprovalHistoryItem
    {
        public Payroll Payroll { get; set; } = null!;
        public Approval Approval { get; set; } = null!;
    }
}