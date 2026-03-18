using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Reports.Models
{
    public class QuotationItem
    {
        public int Id { get; set; }

        [Display(Name = "Item & Description")]
        public string? Description { get; set; }

        public int? Qty { get; set; }

        public int? PricePerQty { get; set; }

        [Display(Name = "Amount (Kshs)")]
        [DataType(DataType.Currency)]
        public decimal Amount { get; set; }

        // ✅ Foreign key to Quotation
        [ForeignKey("Quotation")]
        public int QuotationId { get; set; }

        // ✅ Make this nullable to avoid required relationship enforcement
        public Quotation? Quotation { get; set; }
    }
}
