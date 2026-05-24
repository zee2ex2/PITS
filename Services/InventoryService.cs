using Microsoft.EntityFrameworkCore;
using PITS.Data;
using PITS.Models;

namespace PITS.Services;

public class InventoryService
{
    private readonly InventoryChangeNotifier _notifier;

    public InventoryService(InventoryChangeNotifier notifier)
    {
        _notifier = notifier;
    }

    public async Task<InventoryItem> AddItemAsync(AppDbContext db, InventoryItem item)
    {
        var existing = await db.InventoryItems
            .Where(i => i.ItemId == item.ItemId
                && i.StationId == item.StationId
                && i.Quality == item.Quality)
            .FirstOrDefaultAsync();

        if (existing != null)
        {
            existing.Quantity += item.Quantity;
            existing.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            _notifier.Notify();
            return existing;
        }

        item.CreatedAt = DateTime.UtcNow;
        item.UpdatedAt = DateTime.UtcNow;
        db.InventoryItems.Add(item);
        await db.SaveChangesAsync();
        _notifier.Notify();
        return item;
    }

    public async Task<InventoryItem?> UpdateItemAsync(AppDbContext db, int itemId, InventoryItem update)
    {
        var item = await db.InventoryItems.FindAsync(itemId);
        if (item == null) return null;

        item.ItemId = update.ItemId;
        item.StationId = update.StationId;
        item.Quality = update.Quality;
        item.Quantity = update.Quantity;
        item.IsScu = update.IsScu;
        item.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        _notifier.Notify();
        return item;
    }

    public async Task<bool> DeleteItemAsync(AppDbContext db, int itemId)
    {
        var item = await db.InventoryItems.FindAsync(itemId);
        if (item == null) return false;

        db.InventoryItems.Remove(item);
        await db.SaveChangesAsync();
        _notifier.Notify();
        return true;
    }
}
