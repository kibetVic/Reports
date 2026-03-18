using Microsoft.AspNetCore.Http;
using System.Collections.Generic;

namespace Reports.Models
{
    public class PaymentVoucherEditViewModel
    {
        public PaymentVoucher PaymentVoucher { get; set; } = new();
        public List<PaymentVoucherItem> Items { get; set; } = new();
        public List<UploadedFile> ExistingFiles { get; set; } = new();

        // For new file uploads
        public List<IFormFile>? NewFiles { get; set; }
        public List<string>? NewFileNames { get; set; }
        public List<string>? NewInFavourOf { get; set; }
        public List<int> FilesToDelete { get; set; } = new();

        // Used to track updates
        public List<int>? ExistingIds { get; set; }
        public List<string>? ExistingNames { get; set; }
        public List<string>? ExistingInfavourOf { get; set; }


    }
}
