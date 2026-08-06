using DndCompanion.Models.Packages;

namespace DndCompanion.Services;

/// <summary>Che genere di scelta apre un privilegio di classe.</summary>
public enum TipoDiScelta
{
    Nessuna,
    Sottoclasse,
    TalentoGenerale,
    StileDiCombattimento,
    DonoEpico,
    /// <summary>Il catalogo non conosce le opzioni: si annota in prosa.</summary>
    Libera
}

/// <summary>I fatti sulle regole che il pacchetto dati non dichiara, e i riconoscitori dei
/// privilegi che aprono una scelta. Dati e stringhe: nessun calcolo.
///
/// Perché una mappa nel codice e non un campo nel pacchetto: aggiungere un campo al formato di
/// scambio significa toccare serializzazione e modelli — la fascia a tre giri di revisione, più il
/// publish trimmato obbligatorio — per dodici valori che lo SRD non cambierà. Quando il formato si
/// toccherà per altri motivi, questa mappa diventa il suo valore predefinito.</summary>
public static class LevelUpRules
{
    /// <summary>Classe → caratteristica da incantatore, **in inglese minuscolo** come vuole
    /// <see cref="CharacterCalculations"/>: gli altri valori vengono scartati in silenzio e la CD
    /// degli incantesimi resta vuota.
    ///
    /// Non è derivabile da <c>primaryAbility</c>: il Ranger dichiara «Destrezza» ma lancia con
    /// Saggezza, e il Paladino dichiara «Forza e Carisma».</summary>
    private static readonly Dictionary<string, string> PerClasse = new(StringComparer.Ordinal)
    {
        [CatalogKey.NormalizeName("Bardo")] = "charisma",
        [CatalogKey.NormalizeName("Chierico")] = "wisdom",
        [CatalogKey.NormalizeName("Druido")] = "wisdom",
        [CatalogKey.NormalizeName("Mago")] = "intelligence",
        [CatalogKey.NormalizeName("Paladino")] = "charisma",
        [CatalogKey.NormalizeName("Ranger")] = "wisdom",
        [CatalogKey.NormalizeName("Stregone")] = "charisma",
        [CatalogKey.NormalizeName("Warlock")] = "charisma",
    };

    /// <summary>Null per le classi che non incantano e per i nomi che il manuale non conosce — una
    /// classe del tavolo non deve ereditare la caratteristica di un'omonima del manuale.</summary>
    public static string? CaratteristicaIncantatore(string? nomeClasse)
    {
        if (string.IsNullOrWhiteSpace(nomeClasse)) return null;
        return PerClasse.TryGetValue(CatalogKey.NormalizeName(nomeClasse), out var v) ? v : null;
    }

    /// <summary>Le parole con cui lo SRD nomina le scelte senza elenco a catalogo.</summary>
    private static readonly string[] SenzaElenco =
        { "invocazioni", "metamagia", "maestria" };

    /// <summary>Che scelta apre questo privilegio. L'ordine dei controlli conta: «Privilegio di
    /// tradizione arcana» va letto come sottoclasse, e <see cref="ClassProgression.RiguardaSottoclasse"/>
    /// conosce già le quattro grafie che lo SRD usa.</summary>
    public static TipoDiScelta TipoDi(string? privilegio)
    {
        if (string.IsNullOrWhiteSpace(privilegio)) return TipoDiScelta.Nessuna;

        if (ClassProgression.RiguardaSottoclasse(privilegio)) return TipoDiScelta.Sottoclasse;

        if (privilegio.Contains("incremento", StringComparison.OrdinalIgnoreCase))
            return TipoDiScelta.TalentoGenerale;
        if (privilegio.Contains("stile di combattimento", StringComparison.OrdinalIgnoreCase))
            return TipoDiScelta.StileDiCombattimento;
        if (privilegio.Contains("dono epico", StringComparison.OrdinalIgnoreCase))
            return TipoDiScelta.DonoEpico;

        return SenzaElenco.Any(s => privilegio.Contains(s, StringComparison.OrdinalIgnoreCase))
            ? TipoDiScelta.Libera
            : TipoDiScelta.Nessuna;
    }

    /// <summary>La categoria di talenti da cui pescare le opzioni, null se la scelta non è un
    /// talento.</summary>
    public static string? CategoriaPerScelta(TipoDiScelta tipo) => tipo switch
    {
        TipoDiScelta.TalentoGenerale => "Generale",
        TipoDiScelta.StileDiCombattimento => "Stile di combattimento",
        TipoDiScelta.DonoEpico => "Epico",
        _ => null
    };

    /// <summary>Vero se è il talento che concede l'incremento di caratteristica — quello che apre la
    /// sotto-scelta dei punteggi. Si riconosce dalla parola, non dal nome intero: il privilegio si
    /// chiama «Incremento punteggio caratteristica» e il talento «Incremento del Punteggio di
    /// Caratteristica», e confrontarli per intero non funzionerebbe.</summary>
    public static bool ÈTalentoDiIncremento(PackageFeat? talento)
        => talento is not null
           && talento.Name.Contains("incremento", StringComparison.OrdinalIgnoreCase);
}
