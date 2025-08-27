using System.Collections.Concurrent;
using ReceiptBot.Models;

namespace ReceiptBot.Persistence;

public sealed class InMemoryReceiptRepository : IReceiptRepository
{
    private readonly ConcurrentBag<ReceiptSummary> _store = new();

    public Task<bool> ExistsByHashAsync(string receiptHash, CancellationToken ct = default)
    {
        var exists = _store.Any(r => r.ComputeHash() == receiptHash);
        return Task.FromResult(exists);
    }

    public Task SaveAsync(ReceiptSummary receipt, CancellationToken ct = default)
    {
        _store.Add(receipt);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ReceiptSummary>> GetAllAsync(CancellationToken ct = default)
        => Task.FromResult((IReadOnlyList<ReceiptSummary>)_store.ToArray());
}
