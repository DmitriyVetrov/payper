using ReceiptBot.Models;

namespace ReceiptBot.Persistence;

public interface IReceiptRepository
{
    Task SaveAsync(ReceiptSummary receipt, CancellationToken ct = default);
    Task<IReadOnlyList<ReceiptSummary>> GetAllAsync(CancellationToken ct = default);
}
