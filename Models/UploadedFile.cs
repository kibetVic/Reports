using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Reports.Models
{
    public class UploadedFile
    {
        [Key]
        public int Id { get; set; }

        public int PaymentVoucherId { get; set; }
        [ForeignKey("PaymentVoucherId")]
        public PaymentVoucher? PaymentVoucher { get; set; }
              
        [Required]
        [MaxLength(255)]
        public string? FileName { get; set; }
        [Required]
        [MaxLength(255)]
        public string? InFavourOf { get; set; }

        [Required]
        public string? FilePath { get; set; }

        [MaxLength(1050)]
        public string? FileType { get; set; } 

        public DateTime UploadedOn { get; set; } = DateTime.Now;
        [Required]
        public string? UploadBatchId { get; set; }

        [NotMapped]
        public bool FileExists { get; set; }
    }
}
