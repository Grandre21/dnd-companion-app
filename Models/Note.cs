using Postgrest.Attributes;
using Postgrest.Models;

namespace DndCompanion.Models;

[Table("notes")]
public class Note : BaseModel
{
    [PrimaryKey("id", false)]
    public string Id { get; set; } = string.Empty;

    [Column("player_id")]
    public string PlayerId { get; set; } = string.Empty;

    [Column("title")]
    public string Title { get; set; } = string.Empty;

    [Column("content")]
    public string Content { get; set; } = string.Empty;

    [Column("is_shared")]
    public bool IsShared { get; set; }

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }
}
