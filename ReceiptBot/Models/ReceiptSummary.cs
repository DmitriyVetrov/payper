using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

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

    public string ComputeHash()
    {
        var hashInput = $"{MerchantName?.Trim()?.ToLowerInvariant() ?? ""}" +
                       $"|{Total?.ToString("F2") ?? ""}" +
                       $"|{TransactionDate?.ToString("yyyy-MM-dd") ?? ""}" +
                       $"|{TransactionTime?.ToString("HH:mm") ?? ""}";
        
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(hashInput));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
