namespace Reports.Models
{
    public class Invoice
    {
        public int InvoiceId { get; set; }
        public string? VATREGNO { get; set; } = "0145058P";
        public string? InvoiceNumber { get; set; } = string.Empty;
        public DateTime TrxDate { get; set; } = DateTime.Now;
        public string? InvoiceTo { get; set; } = string.Empty;
        public string? Project { get; set; } = string.Empty;
        public string? Terms { get; set; } = string.Empty;
        public string? PONO{ get; set; } = string.Empty;
        public decimal? SubTotal { get; set; }
        public decimal? VatTotal { get; set; }
        public decimal? Credits { get; set; }
        public decimal? TotalAmount { get; set; }

        public string? BankName { get; set; } = "CO-OPERATIVE BANK";
        public string? BankBranch { get; set; } = "PARLIAMENT ROAD (11044)";
        public string? BankAccount { get; set; } = "01136016034600";
        public string? MpesaPaybill { get; set; } = "400200";
        public string? MpesaAccount { get; set; } = "219602";
        public List<InvoiceItem> Items { get; set; } = new();
    }
}
