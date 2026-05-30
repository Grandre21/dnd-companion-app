using Postgrest.Attributes;
using Postgrest.Models;

namespace DndCompanion.Models;

[Table("campaigns")]
public class Campaign : BaseModel
{
    [PrimaryKey("id", false)]
    public string Id { get; set; } = string.Empty;

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("description")]
    public string? Description { get; set; }

    [Column("owner_id")]
    public string OwnerId { get; set; } = string.Empty;

    [Column("invite_code")]
    public string InviteCode { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}
