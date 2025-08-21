using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FinserveNew.Models
{
    public class InvoiceItem
    {
        [Key]
        public int InvoiceItemID { get; set; }

        [Required]
        public int InvoiceID { get; set; }

        [Required]
        [Display(Name = "Item Description")]
        [StringLength(500)]
        public string Description { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Quantity")]
        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1")]
        public int Quantity { get; set; } = 1;

        [Required]
        [Display(Name = "Unit Price")]
        [Column(TypeName = "decimal(18,2)")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Price must be greater than 0")]
        public decimal UnitPrice { get; set; }

        [Display(Name = "Line Total")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal LineTotal { get; set; }

        [Display(Name = "Created Date")]
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        // Navigation property
        [ForeignKey("InvoiceID")]
        public virtual Invoice? Invoice { get; set; }

        // Computed property to calculate line total
        public void CalculateLineTotal()
        {
            LineTotal = Quantity * UnitPrice;
        }
    }
}