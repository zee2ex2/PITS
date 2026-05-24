using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PITS.Models;

[Table("inventory_items")]
public class InventoryItem
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("item_id")]
    public int ItemId { get; set; }

    [ForeignKey(nameof(ItemId))]
    public Item? Item { get; set; }

    [Column("station_id")]
    public int? StationId { get; set; }

    [ForeignKey(nameof(StationId))]
    public Station? Station { get; set; }

    [Column("quality")]
    public float? Quality { get; set; }

    [Column("quantity")]
    public decimal Quantity { get; set; }

    [Column("is_scu")]
    public bool IsScu { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
