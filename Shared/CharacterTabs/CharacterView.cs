using DndCompanion.Models;
using Microsoft.AspNetCore.Components.Web;

namespace DndCompanion.Shared.CharacterTabs;

/// <summary>
/// Helper di formattazione/accessibilità puri, condivisi dai componenti-tab della scheda PG.
/// Importati globalmente via <c>@using static</c> in <c>_Imports.razor</c>: i call-site li usano
/// non qualificati (es. <c>FormatBonus(x)</c>), così l'estrazione dei tab non cambia il markup.
/// </summary>
public static class CharacterView
{
    /// <summary>Bonus con segno esplicito: +3, -1, +0.</summary>
    public static string FormatBonus(int v) => v >= 0 ? $"+{v}" : v.ToString();

    /// <summary>Valore stringa per <c>aria-pressed</c>/<c>aria-checked</c>.</summary>
    public static string AriaBool(bool b) => b ? "true" : "false";

    /// <summary>Attiva l'azione su Invio/Spazio per i controlli resi accessibili da tastiera.</summary>
    public static async Task OnKey(KeyboardEventArgs e, Func<Task> action)
    {
        if (e.Key == "Enter" || e.Key == " ") await action();
    }

    // ===== Slot incantesimo (condivisi tra il tab Magic e il form di modifica) =====

    public static int GetSpellSlotMax(Character c, int level) => level switch
    {
        1 => c.SpellSlots1Max,
        2 => c.SpellSlots2Max,
        3 => c.SpellSlots3Max,
        4 => c.SpellSlots4Max,
        5 => c.SpellSlots5Max,
        6 => c.SpellSlots6Max,
        7 => c.SpellSlots7Max,
        8 => c.SpellSlots8Max,
        9 => c.SpellSlots9Max,
        _ => 0
    };

    public static int GetSpellSlotUsed(Character c, int level) => level switch
    {
        1 => c.SpellSlots1Used,
        2 => c.SpellSlots2Used,
        3 => c.SpellSlots3Used,
        4 => c.SpellSlots4Used,
        5 => c.SpellSlots5Used,
        6 => c.SpellSlots6Used,
        7 => c.SpellSlots7Used,
        8 => c.SpellSlots8Used,
        9 => c.SpellSlots9Used,
        _ => 0
    };

    public static void SetSpellSlotUsed(Character c, int level, int v)
    {
        switch (level)
        {
            case 1: c.SpellSlots1Used = v; break;
            case 2: c.SpellSlots2Used = v; break;
            case 3: c.SpellSlots3Used = v; break;
            case 4: c.SpellSlots4Used = v; break;
            case 5: c.SpellSlots5Used = v; break;
            case 6: c.SpellSlots6Used = v; break;
            case 7: c.SpellSlots7Used = v; break;
            case 8: c.SpellSlots8Used = v; break;
            case 9: c.SpellSlots9Used = v; break;
        }
    }
}
