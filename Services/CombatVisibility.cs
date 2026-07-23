using DndCompanion.Models;

namespace DndCompanion.Services;

/// <summary>
/// Redazione lato client di ciò che un player può vedere nel tracker combattimento: la propria
/// scheda con statistiche complete e, degli altri, solo il nome. Funzioni pure (nessuno stato/I/O),
/// testabili. È una redazione UX cosmetica: i dati grezzi arrivano comunque al browser del player.
/// Una riga "mia" è quella importata dal proprio PG (<c>OwnerId</c> == utente corrente); le righe
/// senza owner (mostri, PNG, aggiunte a mano) non sono mai "mie". Specchio di <see cref="AccessControl"/>:
/// un owner o uno userId null/vuoto non produce mai un match (niente <c>null == null</c> degenere).
/// </summary>
public static class CombatVisibility
{
    /// <summary>La riga appartiene al player. Owner o userId null/vuoto → false.</summary>
    public static bool IsOwn(Combatant c, string? userId)
        => !string.IsNullOrEmpty(userId)
           && !string.IsNullOrEmpty(c.OwnerId)
           && c.OwnerId == userId;

    /// <summary>Il combattente di turno appartiene al player. Indice fuori range → false.</summary>
    public static bool IsCurrentTurnOwn(IReadOnlyList<Combatant> combatants, int currentTurnIndex, string? userId)
        => currentTurnIndex >= 0
           && currentTurnIndex < combatants.Count
           && IsOwn(combatants[currentTurnIndex], userId);

    /// <summary>Le righe del player, nell'ordine originale (coerente con l'ordinamento del Master).</summary>
    public static List<Combatant> OwnForPlayer(IReadOnlyList<Combatant> combatants, string? userId)
        => combatants.Where(c => IsOwn(c, userId)).ToList();

    /// <summary>
    /// Le righe non del player, ordinate per nome (case-insensitive): l'ordine mostrato non deve
    /// svelare l'iniziativa/l'ordine di turno delle altre entità.
    /// </summary>
    public static List<Combatant> OthersForPlayer(IReadOnlyList<Combatant> combatants, string? userId)
        => combatants
            .Where(c => !IsOwn(c, userId))
            .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
}
