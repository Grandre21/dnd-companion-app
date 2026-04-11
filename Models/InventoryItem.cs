using Postgrest.Attributes;
using Postgrest.Models;

namespace DndCompanion.Models;

[Table("inventory")]
public class InventoryItem : BaseModel
{
    [PrimaryKey("id", false)]
    public string Id { get; set; } = string.Empty;

    [Column("character_id")]
    public string CharacterId { get; set; } = string.Empty;

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("quantity")]
    public int Quantity { get; set; } = 1;

    [Column("weight")]
    public double? Weight { get; set; }

    [Column("description")]
    public string? Description { get; set; }

    [Column("item_type")]
    public string? ItemType { get; set; }

    [Column("is_equipped")]
    public bool IsEquipped { get; set; }

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }
}
