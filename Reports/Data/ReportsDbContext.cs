using Reports.Models;
using Microsoft.EntityFrameworkCore;

namespace Reports.Data
{
    public class ReportsDbContext : DbContext
    {
        public ReportsDbContext(DbContextOptions<ReportsDbContext> options) : base(options) { }

        // DbSets
        public DbSet<County> Counties { get; set; }
        public DbSet<Venue> Venues { get; set; }
        public DbSet<Summary> Summaries { get; set; }
        public DbSet<PaymentVoucher> PaymentVouchers { get; set; }
        public DbSet<UploadedFile> UploadedFiles { get; set; }
        public DbSet<PaymentVoucherItem> PaymentVoucherItems { get; set; }
        public DbSet<VoucherImage> VoucherImages { get; set; }
        public DbSet<UserAccount> UserAccounts { get; set; }
        public DbSet<VoucherDescription> VoucherDescriptions { get; set; }
        public DbSet<Invoice> Invoices { get; set; }
        public DbSet<InvoiceItem> InvoiceItems { get; set; }
        public DbSet<Receipt> Receipts { get; set; }
        public DbSet<ReceiptItem> ReceiptItems { get; set; }
        public DbSet<Quotation> Quotations { get; set; }
        public DbSet<QuotationItem> QuotationItems { get; set; }
        public DbSet<Certificate> Certificates { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Invoice>().Property(i => i.SubTotal).HasPrecision(18, 2);
            modelBuilder.Entity<Invoice>().Property(i => i.VatTotal).HasPrecision(18, 2);
            modelBuilder.Entity<Invoice>().Property(i => i.Credits).HasPrecision(18, 2);
            modelBuilder.Entity<Invoice>().Property(i => i.TotalAmount).HasPrecision(18, 2);

            // --- PaymentVoucher -> Items (1:N)
            modelBuilder.Entity<PaymentVoucher>()
                .HasMany(v => v.Items)
                .WithOne(i => i.PaymentVoucher)
                .HasForeignKey(i => i.PaymentVoucherId)
                .OnDelete(DeleteBehavior.Cascade);

            // --- Summary -> PaymentVoucher (N:1)
            modelBuilder.Entity<Summary>()
                .HasOne(s => s.Voucher)
                .WithMany(v => v.Summaries)   // ✅ linked to collection
                .HasForeignKey(s => s.PaymentVoucherId)
                .OnDelete(DeleteBehavior.Restrict);

            // --- Summary -> County (N:1)
            modelBuilder.Entity<Summary>()
                .HasOne(s => s.County)
                .WithMany()
                .HasForeignKey(s => s.CountyId)
                .OnDelete(DeleteBehavior.Restrict);

            // Define relationship between Quotation and QuotationItem
            modelBuilder.Entity<Quotation>()
                .HasMany(q => q.Items)
                .WithOne(i => i.Quotation)
                .HasForeignKey(i => i.QuotationId)
                .OnDelete(DeleteBehavior.Cascade); // When a quotation is deleted, delete its items

            // Optional: Configure decimal precision for money fields
            modelBuilder.Entity<Quotation>()
                .Property(q => q.SubTotal)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Quotation>()
                .Property(q => q.VAT)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Quotation>()
                .Property(q => q.TotalAmount)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<QuotationItem>()
                .Property(i => i.Amount)
                .HasColumnType("decimal(18,2)");

            // Define relationship between Receipt and ReceiptItem
            modelBuilder.Entity<Receipt>()
                .HasMany(q => q.Items)
                .WithOne(i => i.Receipt)
                .HasForeignKey(i => i.ReceiptId)
                .OnDelete(DeleteBehavior.Cascade); // When a Receipt is deleted, delete its items

            // Optional: Configure decimal precision for money fields
            modelBuilder.Entity<Receipt>()
                .Property(q => q.SubTotal)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Receipt>()
                .Property(q => q.VAT)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Receipt>()
                .Property(q => q.TotalAmount)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<ReceiptItem>()
                .Property(i => i.Amount)
                .HasColumnType("decimal(18,2)");

        }
    }
}
