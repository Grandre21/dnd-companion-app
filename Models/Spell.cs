using Postgrest.Attributes;
using Postgrest.Models;

namespace DndCompanion.Models;

[Table("spells")]
public class Spell : BaseModel
{
    [PrimaryKey("id", false)]
    public string Id { get; set; } = string.Empty;

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("level")]
    public int Level { get; set; }

    [Column("school")]
    public string School { get; set; } = string.Empty;

    [Column("casting_time")]
    public string CastingTime { get; set; } = string.Empty;

    [Column("range")]
    public string Range { get; set; } = string.Empty;

    [Column("components")]
    public string Components { get; set; } = string.Empty;

    [Column("duration")]
    public string Duration { get; set; } = string.Empty;

    [Column("description")]
    public string Description { get; set; } = string.Empty;

    [Column("classes")]
    public string Classes { get; set; } = string.Empty;

    [Column("added_by")]
    public string? AddedBy { get; set; }

    [Column("campaign_id")]
    public string CampaignId { get; set; } = string.Empty;
}
