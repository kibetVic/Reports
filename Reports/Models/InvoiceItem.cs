namespace Reports.Models
{
    public class InvoiceItem
    {
        public int InvoiceItemId { get; set; }
        public int InvoiceId { get; set; }

        public string? Description { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal Rate { get; set; }
        public decimal VatAmount { get; set; }
        public decimal Amount => Quantity * Rate;

        public Invoice? Invoice { get; set; }
    }
}
