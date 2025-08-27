using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ReceiptBot.Persistence.Entities;

[Table("Receipts")]
public sealed class ReceiptEntity
{
    [Key]
    public int Id { get; set; }

    public string? CountryRegion { get; set; }
    public string? MerchantName { get; set; }
    public string? MerchantPhone { get; set; }
    public string? ReceiptType { get; set; }

    public DateOnly? TransactionDate { get; set; }
    public TimeOnly? TransactionTime { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? Total { get; set; }

    [MaxLength(64)]
    public string? ReceiptHash { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public List<ReceiptItemEntity> Items { get; set; } = new();
}