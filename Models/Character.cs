using Postgrest.Attributes;
using Postgrest.Models;

namespace DndCompanion.Models;

[Table("characters")]
public class Character : BaseModel
{
    [PrimaryKey("id", false)]
    public string Id { get; set; } = string.Empty;

    [Column("player_id")]
    public string PlayerId { get; set; } = string.Empty;

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("class")]
    public string Class { get; set; } = string.Empty;

    [Column("race")]
    public string Race { get; set; } = string.Empty;

    [Column("level")]
    public int Level { get; set; } = 1;

    [Column("hit_points")]
    public int HitPoints { get; set; }

    [Column("max_hit_points")]
    public int MaxHitPoints { get; set; }

    [Column("armor_class")]
    public int ArmorClass { get; set; } = 10;

    [Column("strength")]
    public int Strength { get; set; } = 10;

    [Column("dexterity")]
    public int Dexterity { get; set; } = 10;

    [Column("constitution")]
    public int Constitution { get; set; } = 10;

    [Column("intelligence")]
    public int Intelligence { get; set; } = 10;

    [Column("wisdom")]
    public int Wisdom { get; set; } = 10;

    [Column("charisma")]
    public int Charisma { get; set; } = 10;

    [Column("notes")]
    public string? Notes { get; set; }

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }
}
