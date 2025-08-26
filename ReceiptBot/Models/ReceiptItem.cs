namespace ReceiptBot.Models;

public sealed class ReceiptItem
{
    public string? Description { get; set; }
    public string? Category    { get; set; }
    public decimal? Quantity   { get; set; }
    public string? Unit        { get; set; }
    public decimal? Price      { get; set; }
    public decimal? TotalPrice { get; set; }
}
