namespace DndCompanion.Services;

/// <summary>
/// Logica pura della barra di navigazione: dire se una rotta è quella corrente.
/// Sta qui e non nel componente perché ha rami (query string, frammento, slash, rotta vuota
/// della Home) che meritano dei test, secondo il pattern del progetto per la logica dei .razor.
/// </summary>
public static class BottomNavRoutes
{
    /// <summary>
    /// Confronta il percorso corrente (relativo al <c>&lt;base href&gt;</c>, come lo restituisce
    /// <c>NavigationManager.ToBaseRelativePath</c>) con la rotta di una voce di menu.
    /// La rotta vuota è la Home.
    /// </summary>
    public static bool IsActive(string? baseRelativePath, string route)
    {
        var current = baseRelativePath ?? string.Empty;

        // Via query string e frammento: "spells?q=fuoco" resta la sezione Incantesimi.
        var cut = current.IndexOfAny(new[] { '?', '#' });
        if (cut >= 0) current = current[..cut];

        current = current.Trim('/');
        var target = (route ?? string.Empty).Trim('/');

        if (string.Equals(current, target, StringComparison.OrdinalIgnoreCase)) return true;

        // I SOTTOPERCORSI appartengono alla loro sezione: "characters/nuovo" (il wizard di
        // creazione, che dal 2026-07-31 è una pagina propria) deve tenere accesa la voce
        // "Personaggi". Con il solo confronto esatto la barra restava senza alcuna voce attiva.
        // La rotta vuota della Home è esclusa di proposito: sarebbe prefisso di QUALUNQUE percorso
        // e resterebbe accesa ovunque.
        return target.Length > 0
            && current.StartsWith(target, StringComparison.OrdinalIgnoreCase)
            && current.Length > target.Length
            && current[target.Length] == '/';
    }
}
