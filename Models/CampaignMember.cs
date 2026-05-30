using Postgrest.Attributes;
using Postgrest.Models;

namespace DndCompanion.Models;

[Table("campaign_members")]
public class CampaignMember : BaseModel
{
    [PrimaryKey("id", false)]
    public string Id { get; set; } = string.Empty;

    [Column("campaign_id")]
    public string CampaignId { get; set; } = string.Empty;

    [Column("user_id")]
    public string UserId { get; set; } = string.Empty;

    [Column("role")]
    public string Role { get; set; } = string.Empty;

    [Column("joined_at")]
    public DateTime JoinedAt { get; set; }
}
