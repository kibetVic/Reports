using System.ComponentModel.DataAnnotations;

namespace Reports.Models
{
    public class Venue
    {
        [Key]
        public int Id { get; set; }

        [Required, StringLength(150)]
        public string Name { get; set; } = string.Empty;

        [StringLength(300)]
        public string? Address { get; set; }
        public ICollection<Summary> Summaries { get; set; } = new List<Summary>();
        public ICollection<PaymentVoucher> PaymentVouchers { get; set; } = new List<PaymentVoucher>();
    }
}
