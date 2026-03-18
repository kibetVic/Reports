using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Reports.Models
{
    public class ReceiptItem
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
        [ForeignKey("Receipt")]
        public int ReceiptId { get; set; }
        public Receipt? Receipt { get; set; }
    }
}
