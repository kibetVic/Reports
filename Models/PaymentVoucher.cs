using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Reports.Models
{
    public class PaymentVoucher
    {
        [Key]
        public int PaymentVoucherId { get; set; }
        [Required(ErrorMessage = "This field is required")]
        public string? VoucheName { get; set; }

        [Required]
        [Display(Name = "Voucher Number")]
        public string? VoucherNumber { get; set; }

        [Display(Name = "Cheque Number")]
        public string? ChequeNo { get; set; }

        [Required]
        public DateTime Date { get; set; }

        [Required]
        [Display(Name = "Payment Mode")]
        public string? PaymentMode { get; set; } // M-pesa or Cash

        [Display(Name = "Payment in Favour Of")]
        [Required(ErrorMessage = "This field is required")]
        public string? PaymentInFavourOf { get; set; }

        [Display(Name = "Total Amount")]
        [DataType(DataType.Currency)]
        public decimal? TotalAmount { get; set; }

        [Display(Name = "Amount in Words")]
        public string? AmountInWords { get; set; }

        [Display(Name = "Prepared By")]
        [Required(ErrorMessage = "This field is required")]
        public string? PreparedBy { get; set; }

        [Display(Name = "Checked By")]
        [Required(ErrorMessage = "This field is required")]
        public string? CheckedBy { get; set; }

        [Display(Name = "Approved By")]
        [Required(ErrorMessage = "This field is required")]
        public string? ApprovedBy { get; set; }

        // Navigation property for voucher items
        public ICollection<PaymentVoucherItem> Items { get; set; } = new List<PaymentVoucherItem>();
        public ICollection<UploadedFile> UploadedFile { get; set; } = new List<UploadedFile>();
        public ICollection<Summary> Summaries { get; set; } = new List<Summary>();
        //public ICollection<VoucherDescription> VoucherDescriptions { get; set; } = new List<VoucherDescription>();

        // --- NEW: Link back to UserAccount ---
        //[Required]
        //public int? CreatedById { get; set; } // foreign key

        //[ForeignKey(nameof(CreatedById))]
        //public UserAccount? CreatedBy { get; set; }
        [NotMapped]
        public List<UploadedFile>? Files { get; internal set; }
    }
}
