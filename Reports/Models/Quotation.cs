using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Reports.Models
{
    public class Quotation
    {
        public int Id { get; set; }

        [Display(Name = "Quotation Number")]
        public string? QuotationNumber { get; set; }

        [Display(Name = "Client Name")]
        public string? ClientName { get; set; }

        [Display(Name = "Phone Nomber")]
        public string? PhoneNomber { get; set; }

        [Display(Name = "Date Created")]
        public DateTime DateCreated { get; set; } = DateTime.Now;

        [Display(Name = "Sub total")]
        public decimal SubTotal { get; set; }

        [Display(Name = "Total Items")]
        public int TotalItems { get; set; }

        [Display(Name = "VAT (16%)")]
        public decimal VAT { get; set; }

        [Display(Name = "Total Amount")]
        public decimal TotalAmount { get; set; }

        [Display(Name = "Add VAT (16%)")]
        public bool AddVAT { get; set; }

        public List<QuotationItem> Items { get; set; } = new();
    }
}
