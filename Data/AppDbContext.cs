using Microsoft.EntityFrameworkCore;
using App.Models;

namespace App.Data
{
      public class AppDbContext : DbContext
      {
            public AppDbContext(DbContextOptions<AppDbContext> options)
                : base(options)
            {
            }

            public DbSet<LabAdvanceAmount> LabAdvanceAmounts { get; set; }

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                  base.OnModelCreating(modelBuilder);

                  modelBuilder.Entity<LabAdvanceAmount>(entity =>
                  {
                        entity.HasKey(e => e.LabReceiptID);

                        entity.Property(e => e.Amount).HasPrecision(18, 2);
                        // Some databases store IsCancel as 0/1 (int/tinyint) instead of BIT.
                        entity.Property(e => e.IsCancel).HasConversion<int>();
                        entity.Property(e => e.LabReceiptNo).HasMaxLength(100);
                        entity.Property(e => e.TransactionId).HasMaxLength(100);
                        entity.Property(e => e.PaymentMode).HasMaxLength(50);
                        entity.Property(e => e.PayMode).HasMaxLength(50);
                        entity.Property(e => e.Status).HasMaxLength(50);
                        entity.Property(e => e.ChequeCardNo).HasMaxLength(100);
                        entity.Property(e => e.IpAddress).HasMaxLength(50);
                        entity.Property(e => e.UniqueId).HasMaxLength(100);
                        entity.Property(e => e.CancelReason).HasMaxLength(500);
                        entity.Property(e => e.remarks).HasMaxLength(500);
                  });
            }
      }
}
