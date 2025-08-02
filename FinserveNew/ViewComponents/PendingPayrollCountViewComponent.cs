using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FinserveNew.Data;
using System.Linq;
using System.Threading.Tasks;

namespace FinserveNew.ViewComponents
{
    public class PendingPayrollCountViewComponent : ViewComponent
    {
        private readonly AppDbContext _context;

        public PendingPayrollCountViewComponent(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            int count = await _context.Payrolls
                .CountAsync(p => p.PaymentStatus == "Pending Approval");
                
            return Content(count.ToString());
        }
    }
}
