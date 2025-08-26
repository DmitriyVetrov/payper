using Microsoft.EntityFrameworkCore;
using ReceiptBot.Persistence.Entities;

namespace ReceiptBot.Persistence;

public sealed class ReceiptDbContext : DbContext
{
    public ReceiptDbContext(DbContextOptions<ReceiptDbContext> options) : base(options)
    {
    }

    public DbSet<ReceiptEntity> Receipts { get; set; }
    public DbSet<ReceiptItemEntity> ReceiptItems { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Configure relationships
        modelBuilder.Entity<ReceiptItemEntity>()
            .HasOne(ri => ri.Receipt)
            .WithMany(r => r.Items)
            .HasForeignKey(ri => ri.ReceiptId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure indexes for better query performance
        modelBuilder.Entity<ReceiptEntity>()
            .HasIndex(r => r.TransactionDate)
            .HasDatabaseName("IX_Receipts_TransactionDate");

        modelBuilder.Entity<ReceiptEntity>()
            .HasIndex(r => r.MerchantName)
            .HasDatabaseName("IX_Receipts_MerchantName");

        modelBuilder.Entity<ReceiptEntity>()
            .HasIndex(r => r.CreatedAt)
            .HasDatabaseName("IX_Receipts_CreatedAt");
    }
}