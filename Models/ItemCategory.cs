using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PITS.Models;

[Table("itemcategory")]
public class ItemCategory
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Column("parent_id")]
    public int ParentId { get; set; }

    public ICollection<Item> Items { get; set; } = new List<Item>();
}
