using Microsoft.EntityFrameworkCore;
using ReceiptBot.Persistence;

namespace ReceiptBot.Services;

public sealed class ExpenseAnalysisService
{
    private readonly ReceiptDbContext _context;

    public ExpenseAnalysisService(ReceiptDbContext context)
    {
        _context = context;
    }

    public async Task<decimal> GetTotalExpensesAsync(DateTime? from = null, DateTime? to = null, CancellationToken ct = default)
    {
        var query = _context.Receipts.AsQueryable();

        if (from.HasValue)
            query = query.Where(r => r.TransactionDate >= DateOnly.FromDateTime(from.Value));

        if (to.HasValue)
            query = query.Where(r => r.TransactionDate <= DateOnly.FromDateTime(to.Value));

        return await query
            .Where(r => r.Total.HasValue)
            .SumAsync(r => r.Total!.Value, ct);
    }

    public async Task<Dictionary<string, decimal>> GetExpensesByMerchantAsync(DateTime? from = null, DateTime? to = null, CancellationToken ct = default)
    {
        var query = _context.Receipts.AsQueryable();

        if (from.HasValue)
            query = query.Where(r => r.TransactionDate >= DateOnly.FromDateTime(from.Value));

        if (to.HasValue)
            query = query.Where(r => r.TransactionDate <= DateOnly.FromDateTime(to.Value));

        return await query
            .Where(r => r.Total.HasValue && !string.IsNullOrEmpty(r.MerchantName))
            .GroupBy(r => r.MerchantName!)
            .Select(g => new { Merchant = g.Key, Total = g.Sum(r => r.Total!.Value) })
            .ToDictionaryAsync(x => x.Merchant, x => x.Total, ct);
    }

    public async Task<Dictionary<string, decimal>> GetExpensesByCategoryAsync(DateTime? from = null, DateTime? to = null, CancellationToken ct = default)
    {
        var query = _context.ReceiptItems.AsQueryable();

        if (from.HasValue)
            query = query.Where(ri => ri.Receipt.TransactionDate >= DateOnly.FromDateTime(from.Value));

        if (to.HasValue)
            query = query.Where(ri => ri.Receipt.TransactionDate <= DateOnly.FromDateTime(to.Value));

        return await query
            .Where(ri => ri.TotalPrice.HasValue && !string.IsNullOrEmpty(ri.Category))
            .GroupBy(ri => ri.Category!)
            .Select(g => new { Category = g.Key, Total = g.Sum(ri => ri.TotalPrice!.Value) })
            .ToDictionaryAsync(x => x.Category, x => x.Total, ct);
    }

    public async Task<List<(DateOnly Date, decimal Amount)>> GetDailyExpensesAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        var fromDate = DateOnly.FromDateTime(from);
        var toDate = DateOnly.FromDateTime(to);

        return await _context.Receipts
            .Where(r => r.TransactionDate >= fromDate && r.TransactionDate <= toDate && r.Total.HasValue)
            .GroupBy(r => r.TransactionDate!.Value)
            .Select(g => new { Date = g.Key, Total = g.Sum(r => r.Total!.Value) })
            .OrderBy(x => x.Date)
            .Select(x => ValueTuple.Create(x.Date, x.Total))
            .ToListAsync(ct);
    }
}