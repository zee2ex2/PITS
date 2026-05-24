using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PITS.Models;

[Table("schema_info")]
public class SchemaInfo
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("version")]
    [Required]
    [MaxLength(20)]
    public string Version { get; set; } = "v1";

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
