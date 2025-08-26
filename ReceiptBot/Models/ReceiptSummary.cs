using System.Collections.Generic;

namespace ReceiptBot.Models;

public sealed class ReceiptSummary
{
    public string? CountryRegion   { get; set; }
    public string? MerchantName    { get; set; }
    public string? MerchantPhone   { get; set; }
    public string? ReceiptType     { get; set; }

    public DateOnly? TransactionDate { get; set; }
    public TimeOnly? TransactionTime { get; set; }

    public decimal? Total { get; set; }

    public List<ReceiptItem> Items { get; } = new();
}
