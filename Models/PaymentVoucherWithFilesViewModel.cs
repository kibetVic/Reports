using Microsoft.AspNetCore.Http;
using System.Collections.Generic;

namespace Reports.Models
{
    public class PaymentVoucherWithFilesViewModel
    {
        // Payment Voucher
        public PaymentVoucher PaymentVoucher { get; set; } = new();

        // Voucher Items (added dynamically in view)
        public List<PaymentVoucherItem> Items { get; set; } = new();

        // File Uploads
        public List<IFormFile>? Files { get; set; }
        public List<string>? FileNames { get; set; }
        public List<string>? InfavourOf { get; set; }

        // Existing files (for edit scenario)
        public List<UploadedFile>? ExistingFiles { get; set; } = new();
    }
}

