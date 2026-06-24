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
}
