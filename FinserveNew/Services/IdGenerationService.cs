using FinserveNew.Data;
using Microsoft.EntityFrameworkCore;

namespace FinserveNew.Services
{
    public interface IIdGenerationService
    {
        Task<string> GeneratePayrollIdAsync();
        Task<string> GenerateApprovalIdAsync();
    }

    public class IdGenerationService : IIdGenerationService
    {
        private readonly AppDbContext _context;

        public IdGenerationService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<string> GeneratePayrollIdAsync()
        {
            var lastPayroll = await _context.Payrolls
                .Where(p => p.PayrollID.StartsWith("PRL"))
                .OrderByDescending(p => p.PayrollID)
                .FirstOrDefaultAsync();

            int nextNumber = 1;
            if (lastPayroll != null && lastPayroll.PayrollID.Length >= 6)
            {
                var numberPart = lastPayroll.PayrollID.Substring(3);
                if (int.TryParse(numberPart, out int lastNumber))
                {
                    nextNumber = lastNumber + 1;
                }
            }

            return $"PRL{nextNumber:D3}";
        }

        public async Task<string> GenerateApprovalIdAsync()
        {
            var lastApproval = await _context.Approvals
                .Where(a => a.ApprovalID.StartsWith("APV"))
                .OrderByDescending(a => a.ApprovalID)
                .FirstOrDefaultAsync();

            int nextNumber = 1;
            if (lastApproval != null && lastApproval.ApprovalID.Length >= 6)
            {
                var numberPart = lastApproval.ApprovalID.Substring(3);
                if (int.TryParse(numberPart, out int lastNumber))
                {
                    nextNumber = lastNumber + 1;
                }
            }

            return $"APV{nextNumber:D3}";
        }
    }
}