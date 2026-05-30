using Postgrest.Attributes;
using Postgrest.Models;

namespace DndCompanion.Models;

[Table("races")]
public class Race : BaseModel
{
    [PrimaryKey("id", false)]
    public string Id { get; set; } = string.Empty;

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("description")]
    public string Description { get; set; } = string.Empty;

    [Column("speed")]
    public int Speed { get; set; } = 30;

    [Column("str_bonus")]
    public int StrBonus { get; set; }

    [Column("dex_bonus")]
    public int DexBonus { get; set; }

    [Column("con_bonus")]
    public int ConBonus { get; set; }

    [Column("int_bonus")]
    public int IntBonus { get; set; }

    [Column("wis_bonus")]
    public int WisBonus { get; set; }

    [Column("cha_bonus")]
    public int ChaBonus { get; set; }

    [Column("traits")]
    public string Traits { get; set; } = string.Empty;

    [Column("languages")]
    public string Languages { get; set; } = string.Empty;

    [Column("added_by")]
    public string? AddedBy { get; set; }

    [Column("campaign_id")]
    public string CampaignId { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }
}
