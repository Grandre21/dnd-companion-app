namespace DndCompanion.Models;

/// <summary>
/// Un combattente nel tracker iniziativa. È serializzato come elemento del campo jsonb
/// <c>combatants</c> di <see cref="CombatState"/> (POCO, non una tabella a sé).
/// </summary>
public class Combatant
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Proprietario del PG se il combattente è stato importato da un personaggio (owner_id); null per
    /// mostri, PNG e aggiunte manuali. Usato solo dalla redazione lato player (vede solo le righe proprie).
    /// </summary>
    public string? OwnerId { get; set; }
    public int Initiative { get; set; }
    public int CurrentHp { get; set; }
    public int MaxHp { get; set; }
}
