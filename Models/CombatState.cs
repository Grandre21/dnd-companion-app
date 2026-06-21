using Postgrest.Attributes;
using Postgrest.Models;

namespace DndCompanion.Models;

/// <summary>
/// Stato condiviso del combattimento per una campagna (tabella <c>combat_state</c>, una riga per
/// campagna). Il Master lo scrive a ogni azione; i giocatori lo leggono (con polling).
/// </summary>
[Table("combat_state")]
public class CombatState : BaseModel
{
    [PrimaryKey("campaign_id", true)]
    public string CampaignId { get; set; } = string.Empty;

    [Column("combatants")]
    public List<Combatant> Combatants { get; set; } = new();

    [Column("current_turn_index")]
    public int CurrentTurnIndex { get; set; }

    [Column("round_number")]
    public int RoundNumber { get; set; } = 1;

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }
}
