using System.Collections.Generic;
using FinserveNew.Models;

namespace FinserveNew.Models.ViewModels
{
    public class PayrollProcessViewModel
    {
        public int BatchId { get; set; }
        public int Step { get; set; }
        public int Year { get; set; }
        public int Month { get; set; }
        public string? Status { get; set; }

        public List<PayrollRecord> Records { get; set; } = new();
        public List<Employee> Employees { get; set; } = new();
        public List<StatutoryRate> StatutoryRates { get; set; } = new();
    }
} 