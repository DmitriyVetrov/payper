using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ReceiptBot.Persistence.Entities;

[Table("ReceiptItems")]
public sealed class ReceiptItemEntity
{
    [Key]
    public int Id { get; set; }

    public int ReceiptId { get; set; }

    public string? Description { get; set; }
    public string? Category { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal? Quantity { get; set; }

    public string? Unit { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? Price { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? TotalPrice { get; set; }

    // Navigation property
    public ReceiptEntity Receipt { get; set; } = null!;
}