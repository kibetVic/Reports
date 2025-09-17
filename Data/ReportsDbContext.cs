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
        public DbSet<UserAccount> UserAccounts { get; set; }   // ✅ Added


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

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

            // --- Summary -> Venue (N:1)
            modelBuilder.Entity<Summary>()
                .HasOne(s => s.Venue)
                .WithMany()
                .HasForeignKey(s => s.VenueId)
                .OnDelete(DeleteBehavior.Restrict);

            // --- Summary -> County (N:1)
            modelBuilder.Entity<Summary>()
                .HasOne(s => s.County)
                .WithMany()
                .HasForeignKey(s => s.CountyId)
                .OnDelete(DeleteBehavior.Restrict);

            // --- PaymentVoucher -> CreatedBy (N:1 with UserAccount)
            modelBuilder.Entity<PaymentVoucher>()
                .HasOne(v => v.CreatedBy)
                .WithMany(u => u.CreatedVouchers)
                .HasForeignKey(v => v.CreatedById)
                .OnDelete(DeleteBehavior.Restrict); // prevent cascade delete of user wiping vouchers

            modelBuilder.Entity<PaymentVoucher>()
                .HasOne(v => v.CreatedBy)
                .WithMany(u => u.CreatedVouchers)
                .HasForeignKey(v => v.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);

        }
    }
}
