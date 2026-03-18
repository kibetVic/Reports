using System.ComponentModel.DataAnnotations;

namespace Reports.Models
{
    public class County
    {
        [Key]
        public int Id { get; set; }
        public string CountyCode { get; set; } = string.Empty;

        [Required, StringLength(100)]
        public string Name { get; set; } = string.Empty;
        public ICollection<PaymentVoucher> PaymentVouchers { get; set; } = new List<PaymentVoucher>();
        public ICollection<Summary> Summaries { get; set; } = new List<Summary>();
    }
}
