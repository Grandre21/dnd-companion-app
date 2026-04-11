using Postgrest.Attributes;
using Postgrest.Models;

namespace DndCompanion.Models;

[Table("players")]
public class Player : BaseModel
{
    [PrimaryKey("id", false)]
    public string Id { get; set; } = string.Empty;

    [Column("nickname")]
    public string Nickname { get; set; } = string.Empty;

    [Column("pin_hash")]
    public string PinHash { get; set; } = string.Empty;

    [Column("role")]
    public string Role { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }
}
