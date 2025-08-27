using ReceiptBot.Models;

namespace ReceiptBot.Persistence;

public interface IReceiptRepository
{
    Task<bool> ExistsByHashAsync(string receiptHash, CancellationToken ct = default);
    Task SaveAsync(ReceiptSummary receipt, CancellationToken ct = default);
    Task<IReadOnlyList<ReceiptSummary>> GetAllAsync(CancellationToken ct = default);
}
