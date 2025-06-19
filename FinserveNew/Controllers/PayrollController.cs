using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FinserveNew.Data;
using FinserveNew.Models;
using FinserveNew.Models.ViewModels;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace FinserveNew.Controllers
{
    public class PayrollController : Controller
    {
        private readonly AppDbContext _context;
        public PayrollController(AppDbContext context)
        {
            _context = context;
        }

        // GET: Payroll/Dashboard
        public async Task<IActionResult> Dashboard(int? year, int? month)
        {
            var now = DateTime.Now;
            int selectedYear = year ?? now.Year;
            int selectedMonth = month ?? now.Month;

            // Get or create payroll batch for selected month/year
            var batch = await _context.PayrollBatches
                .Include(b => b.PayrollRecords)
                .ThenInclude(r => r.Employee)
                .FirstOrDefaultAsync(b => b.Year == selectedYear && b.Month == selectedMonth);

            // If not found, show option to start payroll
            bool canStartPayroll = batch == null;

            // Summary calculations
            decimal totalGross = 0, totalDeductions = 0, totalNet = 0;
            string status = "Not Started";
            List<PayrollRecord> records = new();
            if (batch != null)
            {
                records = batch.PayrollRecords.ToList();
                totalGross = records.Sum(r => r.BasicSalary + r.TotalAllowances);
                totalDeductions = records.Sum(r => r.TotalDeductions + r.TotalContributions);
                totalNet = records.Sum(r => r.NetSalary);
                status = batch.Status;
            }

            // For year/month dropdowns
            var years = Enumerable.Range(now.Year - 3, 7).ToList();
            var months = Enumerable.Range(1, 12).ToList();

            ViewBag.SelectedYear = selectedYear;
            ViewBag.SelectedMonth = selectedMonth;
            ViewBag.Years = years;
            ViewBag.Months = months;
            ViewBag.CanStartPayroll = canStartPayroll;
            ViewBag.Status = status;
            ViewBag.TotalGross = totalGross;
            ViewBag.TotalDeductions = totalDeductions;
            ViewBag.TotalNet = totalNet;
            ViewBag.Records = records;
            ViewBag.BatchId = batch?.PayrollBatchId;

            return View();
        }

        // GET: Payroll/Start
        public async Task<IActionResult> Start(int year, int month)
        {
            // Create a new payroll batch for the selected month/year
            var batch = new PayrollBatch
            {
                Year = year,
                Month = month,
                Status = "Draft"
            };
            _context.PayrollBatches.Add(batch);
            await _context.SaveChangesAsync();

            // Get all employees
            var employees = await _context.Employees.ToListAsync();

            // Get previous month's batch (if any)
            int prevMonth = month == 1 ? 12 : month - 1;
            int prevYear = month == 1 ? year - 1 : year;
            var prevBatch = await _context.PayrollBatches
                .Include(b => b.PayrollRecords)
                    .ThenInclude(r => r.Components)
                .FirstOrDefaultAsync(b => b.Year == prevYear && b.Month == prevMonth);

            foreach (var emp in employees)
            {
                PayrollRecord prevRecord = prevBatch?.PayrollRecords.FirstOrDefault(r => r.EmployeeID == emp.EmployeeID);
                var record = new PayrollRecord
                {
                    PayrollBatchId = batch.PayrollBatchId,
                    EmployeeID = emp.EmployeeID,
                    BasicSalary = prevRecord?.BasicSalary ?? 0,
                    Status = "Draft"
                };
                // Carry over allowances
                if (prevRecord != null)
                {
                    foreach (var comp in prevRecord.Components.Where(c => c.Type == "Allowance"))
                    {
                        record.Components.Add(new PayrollComponent
                        {
                            Type = comp.Type,
                            Name = comp.Name,
                            Amount = comp.Amount,
                            IsAutoCalculated = comp.IsAutoCalculated
                        });
                    }
                }
                record.TotalAllowances = record.Components.Where(c => c.Type == "Allowance").Sum(c => c.Amount);
                // Initial net salary (will be recalculated in later steps)
                record.NetSalary = record.BasicSalary + record.TotalAllowances;
                _context.PayrollRecords.Add(record);
            }
            await _context.SaveChangesAsync();

            return RedirectToAction("Process", new { batchId = batch.PayrollBatchId });
        }

        // GET: Payroll/Process
        public async Task<IActionResult> Process(int batchId, int step = 1)
        {
            var batch = await _context.PayrollBatches
                .Include(b => b.PayrollRecords)
                    .ThenInclude(r => r.Components)
                .Include(b => b.PayrollRecords)
                    .ThenInclude(r => r.Employee)
                .FirstOrDefaultAsync(b => b.PayrollBatchId == batchId);
            if (batch == null)
                return NotFound();

            var employees = await _context.Employees.ToListAsync();
            var statutoryRates = await _context.StatutoryRates.Where(r => r.IsActive).ToListAsync();

            var vm = new PayrollProcessViewModel
            {
                BatchId = batch.PayrollBatchId,
                Step = step,
                Year = batch.Year,
                Month = batch.Month,
                Status = batch.Status,
                Records = batch.PayrollRecords.ToList(),
                Employees = employees,
                StatutoryRates = statutoryRates
            };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveSalaryStep(int batchId)
        {
            var batch = await _context.PayrollBatches
                .Include(b => b.PayrollRecords)
                    .ThenInclude(r => r.Components)
                .Include(b => b.PayrollRecords)
                    .ThenInclude(r => r.Employee)
                .FirstOrDefaultAsync(b => b.PayrollBatchId == batchId);
            if (batch == null) return NotFound();

            var statutoryRates = await _context.StatutoryRates.Where(r => r.IsActive).ToListAsync();

            foreach (var rec in batch.PayrollRecords)
            {
                // Basic Salary
                if (decimal.TryParse(Request.Form[$"BasicSalary_{rec.PayrollRecordId}"], out var basicSalary))
                    rec.BasicSalary = basicSalary;

                // Allowances
                decimal housing = decimal.TryParse(Request.Form[$"Housing_{rec.PayrollRecordId}"], out var h) ? h : 0;
                decimal transport = decimal.TryParse(Request.Form[$"Transport_{rec.PayrollRecordId}"], out var t) ? t : 0;
                decimal other = decimal.TryParse(Request.Form[$"Other_{rec.PayrollRecordId}"], out var o) ? o : 0;

                // Update or add components
                UpdateOrAddComponent(rec, "Allowance", "Housing", housing);
                UpdateOrAddComponent(rec, "Allowance", "Transport", transport);
                UpdateOrAddComponent(rec, "Allowance", "Other", other);

                rec.TotalAllowances = rec.Components.Where(c => c.Type == "Allowance").Sum(c => c.Amount);

                // Auto-calculate statutory contributions
                var gross = rec.BasicSalary + rec.TotalAllowances;
                var epf = CalculateStatutory("EPF", statutoryRates, gross);
                var socso = CalculateStatutory("SOCSO", statutoryRates, gross);
                var eis = CalculateStatutory("EIS", statutoryRates, gross);
                UpdateOrAddComponent(rec, "Contribution", "EPF", epf, true);
                UpdateOrAddComponent(rec, "Contribution", "SOCSO", socso, true);
                UpdateOrAddComponent(rec, "Contribution", "EIS", eis, true);
                rec.TotalContributions = rec.Components.Where(c => c.Type == "Contribution").Sum(c => c.Amount);
            }
            await _context.SaveChangesAsync();
            return RedirectToAction("Process", new { batchId, step = 2 });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveContributionsStep(int batchId)
        {
            var batch = await _context.PayrollBatches
                .Include(b => b.PayrollRecords)
                    .ThenInclude(r => r.Components)
                .Include(b => b.PayrollRecords)
                    .ThenInclude(r => r.Employee)
                .FirstOrDefaultAsync(b => b.PayrollBatchId == batchId);
            if (batch == null) return NotFound();

            var statutoryRates = await _context.StatutoryRates.Where(r => r.IsActive).ToListAsync();

            foreach (var rec in batch.PayrollRecords)
            {
                var gross = rec.BasicSalary + rec.TotalAllowances;
                decimal epf = decimal.TryParse(Request.Form[$"EPF_{rec.PayrollRecordId}"], out var e) ? e : 0;
                decimal socso = decimal.TryParse(Request.Form[$"SOCSO_{rec.PayrollRecordId}"], out var s) ? s : 0;
                decimal eis = decimal.TryParse(Request.Form[$"EIS_{rec.PayrollRecordId}"], out var i) ? i : 0;

                // If user left field empty or zero, auto-calculate
                if (epf == 0) epf = CalculateStatutory("EPF", statutoryRates, gross);
                if (socso == 0) socso = CalculateStatutory("SOCSO", statutoryRates, gross);
                if (eis == 0) eis = CalculateStatutory("EIS", statutoryRates, gross);

                UpdateOrAddComponent(rec, "Contribution", "EPF", epf, true);
                UpdateOrAddComponent(rec, "Contribution", "SOCSO", socso, true);
                UpdateOrAddComponent(rec, "Contribution", "EIS", eis, true);

                rec.TotalContributions = rec.Components.Where(c => c.Type == "Contribution").Sum(c => c.Amount);
            }
            await _context.SaveChangesAsync();
            return RedirectToAction("Process", new { batchId, step = 3 });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveDeductionsStep(int batchId)
        {
            var batch = await _context.PayrollBatches
                .Include(b => b.PayrollRecords)
                    .ThenInclude(r => r.Components)
                .Include(b => b.PayrollRecords)
                    .ThenInclude(r => r.Employee)
                .FirstOrDefaultAsync(b => b.PayrollBatchId == batchId);
            if (batch == null) return NotFound();

            foreach (var rec in batch.PayrollRecords)
            {
                decimal pcb = decimal.TryParse(Request.Form[$"PCB_{rec.PayrollRecordId}"], out var p) ? p : 0;
                decimal zakat = decimal.TryParse(Request.Form[$"Zakat_{rec.PayrollRecordId}"], out var z) ? z : 0;
                decimal loan = decimal.TryParse(Request.Form[$"Loan_{rec.PayrollRecordId}"], out var l) ? l : 0;
                decimal other = decimal.TryParse(Request.Form[$"OtherDeduct_{rec.PayrollRecordId}"], out var o) ? o : 0;

                UpdateOrAddComponent(rec, "Deduction", "PCB", pcb);
                UpdateOrAddComponent(rec, "Deduction", "Zakat", zakat);
                UpdateOrAddComponent(rec, "Deduction", "Loan", loan);
                UpdateOrAddComponent(rec, "Deduction", "Other", other);

                rec.TotalDeductions = rec.Components.Where(c => c.Type == "Deduction").Sum(c => c.Amount);

                // Final calculation for net salary
                var gross = rec.BasicSalary + rec.TotalAllowances;
                var totalContrib = rec.TotalContributions;
                var totalDeduct = rec.TotalDeductions;
                rec.NetSalary = gross - (totalContrib + totalDeduct);
            }
            await _context.SaveChangesAsync();
            return RedirectToAction("Process", new { batchId, step = 4 });
        }

        // Helper method to update or add a payroll component
        private void UpdateOrAddComponent(PayrollRecord rec, string type, string name, decimal amount, bool isAuto = false)
        {
            var comp = rec.Components.FirstOrDefault(c => c.Type == type && c.Name == name);
            if (comp != null)
            {
                comp.Amount = amount;
                comp.IsAutoCalculated = isAuto;
            }
            else
            {
                rec.Components.Add(new PayrollComponent
                {
                    Type = type,
                    Name = name,
                    Amount = amount,
                    IsAutoCalculated = isAuto
                });
            }
        }

        // Helper to calculate statutory contributions
        private decimal CalculateStatutory(string name, List<StatutoryRate> rates, decimal baseAmount)
        {
            var rate = rates.FirstOrDefault(r => r.Name == name)?.Rate ?? 0;
            return Math.Round(baseAmount * rate, 2);
        }
    }
} 