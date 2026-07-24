using System.Globalization;

namespace DndCompanion.Services;

/// <summary>
/// Logica pura del catalogo mostri. Nessuno stato/I/O: il grado sfida (<c>ChallengeRating</c>) è testo
/// libero e va reso un numero per poter filtrare e ordinare. Stesso pattern di <see cref="CombatImport"/>.
/// </summary>
public static class MonsterCatalog
{
    /// <summary>
    /// Grado sfida come numero ordinabile: le frazioni canoniche 5e ("1/8", "1/4", "1/2") non sono
    /// parsabili come numero e hanno una mappatura esplicita; il resto passa da <c>double</c> a cultura
    /// invariante. Vuoto/non riconosciuto → <c>-1</c> (sentinella "ignoto": esclude dai filtri CR e
    /// ordina in testa).
    /// </summary>
    public static double ParseChallengeRating(string? cr)
    {
        if (string.IsNullOrWhiteSpace(cr)) return -1;
        return cr.Trim() switch
        {
            "0" => 0,
            "1/8" => 0.125,
            "1/4" => 0.25,
            "1/2" => 0.5,
            // Il fallback parsa volutamente cr NON trimmato (comportamento storico): NumberStyles.Float
            // tollera solo gli spazi ASCII, quindi cr.Trim() NON sarebbe equivalente per gli spazi Unicode
            // (es. NBSP " 5" → oggi -1/ignoto, con Trim diventerebbe 5). Non "semplificare" in cr.Trim().
            _ => double.TryParse(cr, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : -1
        };
    }
}
