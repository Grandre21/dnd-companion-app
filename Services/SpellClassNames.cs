namespace DndCompanion.Services;

/// <summary>Le otto classi incantatrici nei due nomi che il catalogo può contenere: quello inglese
/// delle voci digitate finora e quello italiano delle voci di pacchetto (§4.6 dello spec).
/// Logica pura.</summary>
public static class SpellClassNames
{
    /// <summary>Coppie (italiano, inglese) nell'ordine in cui la pagina mostra i filtri.
    /// DEVE restare dichiarata PRIMA di Aliases: l'inizializzazione dei campi statici segue
    /// l'ordine testuale, e invertirli lascerebbe Aliases vuoto senza alcun errore.</summary>
    public static readonly IReadOnlyList<(string Italian, string English)> Pairs = new[]
    {
        ("Bardo",    "Bard"),
        ("Chierico", "Cleric"),
        ("Druido",   "Druid"),
        ("Paladino", "Paladin"),
        ("Ranger",   "Ranger"),
        ("Stregone", "Sorcerer"),
        ("Warlock",  "Warlock"),
        ("Mago",     "Wizard"),
    };

    // Chiave: nome italiano normalizzato. Valore: i due alias normalizzati da cercare nel campo.
    private static readonly Dictionary<string, string[]> Aliases = Pairs.ToDictionary(
        p => CatalogKey.NormalizeName(p.Italian),
        p => new[] { CatalogKey.NormalizeName(p.Italian), CatalogKey.NormalizeName(p.English) },
        StringComparer.Ordinal);

    /// <summary>Vero se il campo "classi" di un incantesimo — testo libero, in italiano o in
    /// inglese — contiene la classe indicata.
    ///
    /// Il confronto è per TOKEN e non per sottostringa: `Contains` su un campo libero farebbe
    /// combaciare qualunque parola che contenga il nome, e il filtro mostrerebbe incantesimi
    /// che non appartengono alla classe.</summary>
    public static bool Matches(string? classesField, string italianName)
    {
        if (string.IsNullOrWhiteSpace(classesField)) return false;
        if (!Aliases.TryGetValue(CatalogKey.NormalizeName(italianName), out var aliases)) return false;

        foreach (var token in classesField.Split(',', ';', '/'))
        {
            var chiave = CatalogKey.NormalizeName(token);
            if (Array.IndexOf(aliases, chiave) >= 0) return true;
        }
        return false;
    }
}
