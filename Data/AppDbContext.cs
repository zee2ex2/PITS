using Microsoft.EntityFrameworkCore;
using PITS.Models;

namespace PITS.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<StarSystem> Systems => Set<StarSystem>();
    public DbSet<ItemCategory> ItemCategories => Set<ItemCategory>();
    public DbSet<Item> Items => Set<Item>();
    public DbSet<Station> Stations => Set<Station>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<Plugin> Plugins => Set<Plugin>();
    public DbSet<SchemaInfo> SchemaInfo => Set<SchemaInfo>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Item>()
            .HasOne(i => i.Category)
            .WithMany(c => c.Items)
            .HasForeignKey(i => i.CatId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Station>()
            .HasOne(s => s.System)
            .WithMany(sys => sys.Stations)
            .HasForeignKey(s => s.SystemId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<InventoryItem>()
            .Property(ii => ii.Quantity)
            .HasPrecision(18, 2);

        modelBuilder.Entity<InventoryItem>()
            .HasOne(ii => ii.Item)
            .WithMany()
            .HasForeignKey(ii => ii.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<InventoryItem>()
            .HasOne(ii => ii.Station)
            .WithMany()
            .HasForeignKey(ii => ii.StationId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
