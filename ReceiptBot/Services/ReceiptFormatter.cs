using System.Globalization;
using ReceiptBot.Models;

namespace ReceiptBot.Services
{
    public sealed class ReceiptFormatter
    {
        public string Format(ReceiptSummary r)
        {
            var lines = new List<string>();

            if (!string.IsNullOrWhiteSpace(r.MerchantName))
                lines.Add($"Merchant: {r.MerchantName}");

            if (r.Total is not null)
                lines.Add($"Total: {r.Total.Value.ToString("F2", CultureInfo.InvariantCulture)}");

            if (r.TransactionDate is not null || r.TransactionTime is not null)
            {
                string datePart = r.TransactionDate?.ToString("yyyy-MM-dd") ?? "";
                string timePart = r.TransactionTime?.ToString("HH:mm") ?? "";
                lines.Add($"Date: {datePart} {timePart}".Trim());
            }

            lines.Add($"Items: {r.Items.Count}");

            return string.Join("\n", lines);
        }
    }
}
