using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PITS.Models;

[Table("systems")]
public class StarSystem
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Column("code")]
    [Required]
    [MaxLength(10)]
    public string Code { get; set; } = string.Empty;

    public ICollection<Station> Stations { get; set; } = new List<Station>();
}
