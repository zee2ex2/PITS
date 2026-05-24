using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PITS.Models;

[Table("stations")]
public class Station
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

    [Column("systemid")]
    public int SystemId { get; set; }

    [ForeignKey(nameof(SystemId))]
    public StarSystem? System { get; set; }
}
