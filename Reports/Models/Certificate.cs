using System;
using System.ComponentModel.DataAnnotations;

namespace Reports.Models
{
    public class Certificate
    {
        [Key]
        public int Id { get; set; }
        public string? CertTitle { get; set; }

        [Required(ErrorMessage = "Recipient Name is required")]
        [Display(Name = "Recipient Name")]
        public string? RecipientName { get; set; }

        [Display(Name = "Training Title")]
        public string? TrainingTitle { get; set; }

        [Display(Name = "Location")]
        public string? Location { get; set; }

        [Display(Name = "Event Date")]
        [DataType(DataType.Date)]
        public DateTime EventDate { get; set; } = DateTime.Now;

        [Display(Name = "Issue Date")]
        [DataType(DataType.Date)]
        public DateTime IssueDate { get; set; } = DateTime.Now;

        [Display(Name = "Certificate Number")]
        public string? CertificateNumber { get; set; }
        public string? CompanyName { get; set; }   
        public string? CEO_Name { get; set; }
        public string? CEO_Title { get; set; }
        public byte[]? TrainerSignature { get; set; }
        public byte[]? CEOSignature { get; set; }
        public string? Trainer_Name { get; set; }
        public string? Trainer_Title { get; set; }
    }
}