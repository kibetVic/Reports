using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Reports.Models
{
    public class PaymentVoucherItem
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("PaymentVoucher")]
        public int PaymentVoucherId { get; set; }
        public PaymentVoucher PaymentVoucher { get; set; }

        [Required]
        public int ItemNo { get; set; }
        public int EachAmount { get; set; }
        public int MpesaCharges { get; set; }

        [Required]
        [StringLength(500)]
        public string Description { get; set; }

        [Required]
        [DataType(DataType.Currency)]
        public decimal Amount { get; set; }
        [NotMapped]
        public decimal? VAT { get; set; }
        public int Cts { get; set; } = 0;
    }
}
