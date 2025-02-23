using Microsoft.EntityFrameworkCore;

namespace Finserve3.Models
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Claim> Claims { get; set; }
        public DbSet<Employee> Employees { get; set; }
        // Add more DbSets if needed
    }
}
