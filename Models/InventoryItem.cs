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

    // ---------------------------------------------------------------
    // Sezione arma (valorizzata solo quando ItemType == "weapon")
    // ---------------------------------------------------------------
    [Column("attack_bonus")]
    public string? AttackBonus { get; set; }

    [Column("damage")]
    public string? Damage { get; set; }

    [Column("damage_type")]
    public string? DamageType { get; set; }

    [Column("attack_notes")]
    public string? AttackNotes { get; set; }

    /// <summary>Arma accurata (finesse): il bonus d'attacco calcolato usa il migliore fra Forza e
    /// Destrezza invece della sola Forza (Services/WeaponCalculations.cs).</summary>
    [Column("is_finesse")]
    public bool IsFinesse { get; set; }

    /// <summary>Arma a distanza: il bonus d'attacco calcolato usa Destrezza invece di Forza.</summary>
    [Column("is_ranged")]
    public bool IsRanged { get; set; }

    /// <summary>Eccezione a D6 (la competenza con l'arma si assume vera): se true, il bonus di
    /// competenza non entra nel calcolo del bonus d'attacco.</summary>
    [Column("is_not_proficient")]
    public bool IsNotProficient { get; set; }
}
