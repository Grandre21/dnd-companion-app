using DndCompanion.Models;

namespace DndCompanion.Services;

/// <summary>
/// Logica pura per la pagina Party: raggruppamento (il proprio PG separato dal resto del gruppo,
/// specchio di <see cref="CombatVisibility"/>) e formattazione PF. Nessuno stato/I/O, testabile.
///
/// La percezione passiva NON viene ricalcolata qui: <see cref="PartyMember"/> è il risultato della
/// RPC <c>get_party_overview</c>, che la restituisce già calcolata lato server (unica fonte di
/// verità — la riga non porta saggezza né competenze grezze per ricostruirla lato client, e non
/// dovrebbe: sarebbe una colonna "sintetica" in più, contro il perimetro voluto per questa vista).
/// </summary>
public static class PartyOverviewCalculations
{
    /// <summary>Il PG appartiene all'utente corrente. Owner o userId null/vuoto → false (niente
    /// <c>null == null</c> degenere), stessa regola di <see cref="CombatVisibility.IsOwn"/>.</summary>
    public static bool IsMine(PartyMember member, string? userId)
        => !string.IsNullOrEmpty(userId)
           && !string.IsNullOrEmpty(member.OwnerId)
           && member.OwnerId == userId;

    /// <summary>Il proprio/i propri PG (di norma uno solo), ordinati per nome case-insensitive.</summary>
    public static List<PartyMember> Mine(IEnumerable<PartyMember> members, string? userId)
        => members
            .Where(m => IsMine(m, userId))
            .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>Il resto del gruppo, ordinato per nome case-insensitive.</summary>
    public static List<PartyMember> Others(IEnumerable<PartyMember> members, string? userId)
        => members
            .Where(m => !IsMine(m, userId))
            .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>Formato "PF attuali/max" della scheda personaggio, es. "18 / 24".</summary>
    public static string FormatHp(int currentHp, int maxHp) => $"{currentHp} / {maxHp}";

    /// <summary>
    /// Percentuale 0-100 per la barra dei PF. Difensiva: PF massimi non positivi → 0 (evita
    /// divisione per zero/negativa su dati sporchi), PF attuali fuori [0, max] → clampati.
    /// </summary>
    public static int HpPercent(int currentHp, int maxHp)
    {
        if (maxHp <= 0) return 0;
        var clamped = Math.Clamp(currentHp, 0, maxHp);
        return (int)Math.Round(clamped * 100.0 / maxHp);
    }

    /// <summary>
    /// Classe CSS della barra dei PF secondo la fascia di salute: fino al 25% pericolo, fino al
    /// 50% attenzione, oltre normale.
    ///
    /// Le due soglie stanno qui e non nel markup perché sono una decisione ("quando un compagno è
    /// in pericolo"), non una scelta di stile: è la stessa informazione che il master usa per
    /// decidere se curare, e va poter essere verificata da un test invece che a vista sui colori.
    /// </summary>
    public static string HpFillClass(int currentHp, int maxHp)
    {
        var percent = HpPercent(currentHp, maxHp);
        if (percent <= 25) return "hp-fill-danger";
        if (percent <= 50) return "hp-fill-warn";
        return "hp-fill-ok";
    }
}
