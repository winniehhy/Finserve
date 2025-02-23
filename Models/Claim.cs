using System.ComponentModel.DataAnnotations;
namespace Finserve3.Models
{
   public class Claim
{
    [Key]
    public int ClaimId { get; set; }  // Primary key for Claim

    [Required]
    public string EmployeeId { get; set; }  // Foreign key to Employee

    public string ClaimType { get; set; }
    public decimal ClaimAmount { get; set; }
    public string Status { get; set; }
    public DateTime SubmissionDate { get; set; }
    public string DocumentPath { get; set; }

    // Navigation property to Employee
    public virtual Employee Employee { get; set; }
}
}
