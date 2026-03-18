using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Reports.Models
{
    public enum ReceiptStatus
    {
        [Display(Name = "None")]
        None = 0,

        [Display(Name = "Paid")]
        Paid = 1,

        [Display(Name = "Received")]
        Received = 2
    }

    public class Receipt
    {
        public int Id { get; set; }

        [Display(Name = "Receipt Number")]
        public string? ReceiptNumber { get; set; }

        [Display(Name = "Client Name")]
        public string? ClientName { get; set; }

        [Display(Name = "Phone Number")]
        public string? PhoneNomber { get; set; }

        [Display(Name = "Date Created")]
        public DateTime DateCreated { get; set; } = DateTime.Now;

        [Display(Name = "Sub total")]
        public decimal SubTotal { get; set; }

        [Display(Name = "Total Items")]
        public int TotalItems { get; set; }

        [Display(Name = "ReffenceNo / ChequeNo")]
        public string? RefferenceNo { get; set; }

        [Display(Name = "VAT (16%)")]
        public decimal VAT { get; set; }

        [Display(Name = "Total Amount")]
        public decimal TotalAmount { get; set; }

        [Display(Name = "Add VAT (16%)")]
        public bool AddVAT { get; set; }

        [Display(Name = "Receipt Status")]
        public ReceiptStatus Status { get; set; } = ReceiptStatus.None;
        public List<ReceiptItem> Items { get; set; } = new();
    }
}
