using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
namespace Reports.Models
{
    public class FileUploadViewModel
    {
        public int PaymentVoucherId { get; set; }
        public List<IFormFile>? Files { get; set; }
        public List<string>? FileNames { get; set; }

        public List<UploadedFile>? ExistingFiles { get; set; } = new();
    }
}
