using Postgrest.Attributes;
using Postgrest.Models;

namespace DndCompanion.Models;

[Table("monsters")]
public class Monster : BaseModel
{
    [PrimaryKey("id", false)]
    public string Id { get; set; } = string.Empty;

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("size")]
    public string Size { get; set; } = string.Empty;

    [Column("type")]
    public string Type { get; set; } = string.Empty;

    [Column("alignment")]
    public string Alignment { get; set; } = string.Empty;

    [Column("armor_class")]
    public int ArmorClass { get; set; }

    [Column("hit_points")]
    public string HitPoints { get; set; } = string.Empty;

    [Column("speed")]
    public string Speed { get; set; } = string.Empty;

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

    [Column("challenge_rating")]
    public string ChallengeRating { get; set; } = string.Empty;

    [Column("abilities")]
    public string Abilities { get; set; } = string.Empty;

    [Column("description")]
    public string Description { get; set; } = string.Empty;

    [Column("added_by")]
    public string? AddedBy { get; set; }

    [Column("campaign_id")]
    public string CampaignId { get; set; } = string.Empty;
}
