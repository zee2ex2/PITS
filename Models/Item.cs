using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PITS.Models;

[Table("items")]
public class Item
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Column("code")]
    [Required]
    [MaxLength(10)]
    public string Code { get; set; } = string.Empty;

    [Column("catid")]
    public int? CatId { get; set; }

    [ForeignKey(nameof(CatId))]
    public ItemCategory? Category { get; set; }

    [Column("hasquality")]
    public bool HasQuality { get; set; }
}
