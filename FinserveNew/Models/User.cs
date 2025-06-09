using System.ComponentModel.DataAnnotations;

namespace FinserveNew.Models
{
    public class User
    {
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        public string Role { get; set; } = string.Empty;

        // Navigation Property - One User can have many Claims
        public virtual ICollection<Claim> Claims { get; set; } = new List<Claim>();
    }
}