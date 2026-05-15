using Postgrest.Attributes;
using Postgrest.Models;

namespace DndCompanion.Models;

[Table("character_spells")]
public class CharacterSpell : BaseModel
{
    [PrimaryKey("id", false)]
    public string Id { get; set; } = string.Empty;

    [Column("character_id")]
    public string CharacterId { get; set; } = string.Empty;

    [Column("spell_id")]
    public string SpellId { get; set; } = string.Empty;

    [Column("is_prepared")]
    public bool IsPrepared { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}
