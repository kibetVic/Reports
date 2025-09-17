using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Reports.Models
{
    public class VoucherImage
    {
        [Key]
        public int Id { get; set; }

        public int PaymentVoucherId { get; set; }

        [ForeignKey(nameof(PaymentVoucherId))]
        public PaymentVoucher? Voucher { get; set; }

        [Required, StringLength(300)]
        public string ImagePath { get; set; } = string.Empty;
    }
}
