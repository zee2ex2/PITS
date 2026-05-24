using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PITS.Models;

[Table("plugins")]
public class Plugin
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Column("version")]
    [Required]
    [MaxLength(20)]
    public string Version { get; set; } = string.Empty;

    [Column("assembly_path")]
    [Required]
    [MaxLength(500)]
    public string AssemblyPath { get; set; } = string.Empty;

    [Column("is_enabled")]
    public bool IsEnabled { get; set; } = true;

    [Column("installed_at")]
    public DateTime InstalledAt { get; set; } = DateTime.UtcNow;
}
