using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Finserve3.ViewModels
{
    public class CreateClaimViewModel
    {
        [Required]
        public string? EmployeeId { get; set; }

        [Required]
        public string? ClaimType { get; set; }

        [Required]
        public decimal ClaimAmount { get; set; }

        // For file uploads, ASP.NET Core uses IFormFile
        public IFormFile Document { get; set; }
    }
}

