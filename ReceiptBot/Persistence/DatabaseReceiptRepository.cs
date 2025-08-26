using Microsoft.EntityFrameworkCore;
using ReceiptBot.Models;
using ReceiptBot.Persistence.Entities;

namespace ReceiptBot.Persistence;

public sealed class DatabaseReceiptRepository : IReceiptRepository
{
    private readonly ReceiptDbContext _context;

    public DatabaseReceiptRepository(ReceiptDbContext context)
    {
        _context = context;
    }

    public async Task SaveAsync(ReceiptSummary receipt, CancellationToken ct = default)
    {
        var entity = new ReceiptEntity
        {
            CountryRegion = receipt.CountryRegion,
            MerchantName = receipt.MerchantName,
            MerchantPhone = receipt.MerchantPhone,
            ReceiptType = receipt.ReceiptType,
            TransactionDate = receipt.TransactionDate,
            TransactionTime = receipt.TransactionTime,
            Total = receipt.Total,
        };

        // Add items
        foreach (var item in receipt.Items)
        {
            entity.Items.Add(new ReceiptItemEntity
            {
                Description = item.Description,
                Category = item.Category,
                Quantity = item.Quantity,
                Unit = item.Unit,
                Price = item.Price,
                TotalPrice = item.TotalPrice,
            });
        }

        _context.Receipts.Add(entity);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ReceiptSummary>> GetAllAsync(CancellationToken ct = default)
    {
        var entities = await _context.Receipts
            .Include(r => r.Items)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

        return entities.Select(MapToModel).ToList();
    }

    private static ReceiptSummary MapToModel(ReceiptEntity entity)
    {
        var summary = new ReceiptSummary
        {
            CountryRegion = entity.CountryRegion,
            MerchantName = entity.MerchantName,
            MerchantPhone = entity.MerchantPhone,
            ReceiptType = entity.ReceiptType,
            TransactionDate = entity.TransactionDate,
            TransactionTime = entity.TransactionTime,
            Total = entity.Total,
        };

        foreach (var item in entity.Items)
        {
            summary.Items.Add(new ReceiptItem
            {
                Description = item.Description,
                Category = item.Category,
                Quantity = item.Quantity,
                Unit = item.Unit,
                Price = item.Price,
                TotalPrice = item.TotalPrice,
            });
        }

        return summary;
    }
}