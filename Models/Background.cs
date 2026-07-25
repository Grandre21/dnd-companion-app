using Postgrest.Attributes;
using Postgrest.Models;

namespace DndCompanion.Models;

/// <summary>Background 2024: porta i punteggi di caratteristica, che nel 2014 stavano sulla specie.
/// La colonna elenca le TRE caratteristiche; la ripartizione la sceglie il giocatore (§4.2).</summary>
[Table("backgrounds")]
public class Background : BaseModel
{
    [PrimaryKey("id", false)]
    public string Id { get; set; } = string.Empty;

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("description")]
    public string Description { get; set; } = string.Empty;

    [Column("ability_scores")]
    public string AbilityScores { get; set; } = string.Empty;

    [Column("origin_feat")]
    public string OriginFeat { get; set; } = string.Empty;

    [Column("skill_proficiencies")]
    public string SkillProficiencies { get; set; } = string.Empty;

    [Column("tool_proficiency")]
    public string ToolProficiency { get; set; } = string.Empty;

    [Column("equipment")]
    public string Equipment { get; set; } = string.Empty;

    [Column("source_id")]
    public string? SourceId { get; set; }

    [Column("added_by")]
    public string? AddedBy { get; set; }

    [Column("campaign_id")]
    public string CampaignId { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }
}
