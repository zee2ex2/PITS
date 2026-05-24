using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PITS.Models;

namespace PITS.Data.Seed;

public static class SeedData
{
    public static async Task InitializeAsync(AppDbContext db, string seedFilePath)
    {
        if (!File.Exists(seedFilePath))
            return;

        var hasSeedData = await db.ItemCategories.AnyAsync();
        if (hasSeedData)
            return;

        var json = await File.ReadAllTextAsync(seedFilePath);
        var doc = JsonDocument.Parse(json);

        var systems = new List<StarSystem>();
        foreach (var sys in doc.RootElement.GetProperty("systems").EnumerateArray())
        {
            systems.Add(new StarSystem
            {
                Id = sys.GetProperty("id").GetInt32(),
                Name = sys.GetProperty("Name").GetString() ?? string.Empty,
                Code = sys.GetProperty("Code").GetString() ?? string.Empty
            });
        }
        db.Systems.AddRange(systems);
        await db.SaveChangesAsync();

        var categories = new List<ItemCategory>();
        foreach (var cat in doc.RootElement.GetProperty("itemcategory").EnumerateArray())
        {
            categories.Add(new ItemCategory
            {
                Id = cat.GetProperty("id").GetInt32(),
                Name = cat.GetProperty("name").GetString() ?? string.Empty,
                ParentId = cat.GetProperty("parent_id").GetInt32()
            });
        }
        db.ItemCategories.AddRange(categories);
        await db.SaveChangesAsync();

        var stations = new List<Station>();
        foreach (var sta in doc.RootElement.GetProperty("stations").EnumerateArray())
        {
            stations.Add(new Station
            {
                Id = sta.GetProperty("id").GetInt32(),
                Name = sta.GetProperty("name").GetString() ?? string.Empty,
                Code = sta.GetProperty("code").GetString() ?? string.Empty,
                SystemId = sta.GetProperty("systemid").GetInt32()
            });
        }
        db.Stations.AddRange(stations);
        await db.SaveChangesAsync();

        var items = new List<Item>();
        foreach (var item in doc.RootElement.GetProperty("item").EnumerateArray())
        {
            items.Add(new Item
            {
                Id = item.GetProperty("id").GetInt32(),
                Name = item.GetProperty("name").GetString() ?? string.Empty,
                Code = item.GetProperty("code").GetString() ?? string.Empty,
                CatId = item.TryGetProperty("catid", out var catid) && catid.ValueKind == JsonValueKind.Number
                    ? catid.GetInt32()
                    : null,
                HasQuality = item.TryGetProperty("hasquality", out var hq) && hq.GetInt32() == 1
            });
        }
        db.Items.AddRange(items);
        await db.SaveChangesAsync();
    }

    public static async Task ResetAsync(AppDbContext db, string seedFilePath)
    {
        db.InventoryItems.RemoveRange(db.InventoryItems);
        db.Plugins.RemoveRange(db.Plugins);
        db.Items.RemoveRange(db.Items);
        db.ItemCategories.RemoveRange(db.ItemCategories);
        db.Stations.RemoveRange(db.Stations);
        db.Systems.RemoveRange(db.Systems);
        await db.SaveChangesAsync();

        await InitializeAsync(db, seedFilePath);
    }
}
