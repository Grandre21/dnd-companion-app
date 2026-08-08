using DndCompanion.Models;

namespace DndCompanion.Services;

/// <summary>
/// Il tag di economia d'azione dei privilegi (Ira, Difesa senza armatura, ...) — la sezione della
/// scheda in cui ciascuno compare al tavolo. Logica pura sul campo jsonb
/// <c>characters.character_features</c> (v. spec 2026-08-08, D5).
///
/// Gemello di <see cref="ClassResourceRules"/>: stessa mappa curata a mano con chiavi normalizzate,
/// stesso incrocio col pacchetto SRD nei test, stessa tolleranza a un jsonb malformato in
/// <see cref="Normalizza"/>. Differenza voluta: dove <see cref="ClassResourceRules.Normalizza"/> fa
/// cadere una <c>Ricarica</c> ignota sul default "lungo", qui un <see cref="CharacterFeature.Azione"/>
/// ignoto cade su <c>null</c> — in combattimento un tag indovinato è peggio di un tag mancante.
/// </summary>
public static class CharacterFeatureRules
{
    /// <summary>I cinque tag ammessi per <see cref="CharacterFeature.Azione"/>, nell'ordine in cui
    /// la scheda li raggruppa (v. spec 2026-08-08, D5). "turno" è per i *rider* del tipo «una volta
    /// per turno, se colpisci» che non sono nessuno degli altri quattro.</summary>
    public static IReadOnlyList<string> TagAmmessi { get; } =
        new[] { "azione", "bonus", "reazione", "passivo", "turno" };

    private static readonly HashSet<string> TagAmmessiSet = new(TagAmmessi, StringComparer.Ordinal);

    /// <summary>Tetto a <see cref="CharacterFeature.Nota"/> dentro <see cref="Normalizza"/>: una
    /// nota è un riassunto per il tavolo, non un capitolo.</summary>
    private const int LunghezzaMassimaNota = 2000;

    /// <summary>I privilegi tipici di ciascuna classe con il loro tag suggerito, coi nomi presi
    /// ESATTAMENTE come compaiono fra i <c>features</c> del pacchetto SRD — v.
    /// <c>Tests/CharacterFeatureRulesTests.cs</c> che incrocia questa mappa col pacchetto. Le classi
    /// assenti da qui non hanno suggerimenti: <see cref="AzioneSuggerita"/> restituisce null e la
    /// voce finisce nel gruppo «Da classificare» finché l'utente non le dà un tag.</summary>
    private static readonly Dictionary<string, Dictionary<string, string>> PerClasse =
        new(StringComparer.Ordinal)
        {
            [CatalogKey.NormalizeName("Barbaro")] = new(StringComparer.Ordinal)
            {
                [CatalogKey.NormalizeName("Ira")] = "bonus",
                [CatalogKey.NormalizeName("Difesa senza armatura")] = "passivo",
                [CatalogKey.NormalizeName("Maestria nelle armi")] = "passivo",
                [CatalogKey.NormalizeName("Senso del pericolo")] = "passivo",
                [CatalogKey.NormalizeName("Attacco fuori controllo")] = "azione",
                [CatalogKey.NormalizeName("Conoscenza primordiale")] = "passivo",
                [CatalogKey.NormalizeName("Attacco extra")] = "azione",
                [CatalogKey.NormalizeName("Movimento aumentato")] = "passivo",
            },
        };

    /// <summary>La tabella curata, esposta al solo test che la incrocia col pacchetto SRD. Chiave
    /// esterna: classe normalizzata. Chiave interna: privilegio normalizzato.</summary>
    internal static IReadOnlyDictionary<string, Dictionary<string, string>> TabellaPerTest => PerClasse;

    /// <summary>Il nome normalizzato del solo marcatore di impalcatura che non è già coperto da
    /// <see cref="ClassProgression.RiguardaSottoclasse"/>: l'incremento caratteristica non è una
    /// scelta di sottoclasse, è un evento a sé (v. <see cref="ÈImpalcatura"/>).</summary>
    private static readonly string IncrementoPunteggioCaratteristica =
        CatalogKey.NormalizeName("Incremento punteggio caratteristica");

    /// <summary>Vero per le voci che nella tabella SRD segnano un momento della progressione invece
    /// di conferire una capacità usabile: «Incremento punteggio caratteristica», «Sottoclasse del
    /// &lt;classe&gt;», «Privilegio di sottoclasse». Non si rendono nella vista di gioco perché il loro
    /// effetto è già altrove nella scheda — i punteggi, o i privilegi della sottoclasse, che
    /// compaiono per conto proprio.
    ///
    /// Riusa <see cref="ClassProgression.RiguardaSottoclasse"/> per il riconoscimento delle voci di
    /// sottoclasse, invece di tenerne una seconda lista di marcatori: qui si aggiunge solo il
    /// confronto con l'incremento caratteristica.</summary>
    public static bool ÈImpalcatura(string? nomePrivilegio)
    {
        if (string.IsNullOrWhiteSpace(nomePrivilegio)) return false;
        if (ClassProgression.RiguardaSottoclasse(nomePrivilegio)) return true;
        return CatalogKey.NormalizeName(nomePrivilegio) == IncrementoPunteggioCaratteristica;
    }

    /// <summary>Il nome del tag al singolare, per il menu «Quando si usa» — diverso di proposito
    /// dalle intestazioni di gruppo di <c>CharacterFeatureJoin.Etichette</c>, che sono al plurale
    /// perché intitolano una sezione: «Passivi» per un elenco, «Passivo» per una voce sola.
    /// Due mappe, due contesti, e questo commento perché la differenza non sembri una svista.</summary>
    public static string EtichettaTag(string? tag) => tag switch
    {
        "azione" => "Azione",
        "bonus" => "Azione bonus",
        "reazione" => "Reazione",
        "turno" => "Una volta per turno",
        "passivo" => "Passivo",
        _ => tag ?? string.Empty,
    };

    /// <summary>Il tag suggerito per un privilegio di una classe, dalla tabella curata. Null se la
    /// classe non è in tabella, se il privilegio non lo è, o se uno dei due argomenti è
    /// vuoto/nullo — mai un'eccezione, mai un tag indovinato.</summary>
    public static string? AzioneSuggerita(string? nomeClasse, string? nomePrivilegio)
    {
        if (string.IsNullOrWhiteSpace(nomeClasse) || string.IsNullOrWhiteSpace(nomePrivilegio))
            return null;
        if (!PerClasse.TryGetValue(CatalogKey.NormalizeName(nomeClasse), out var privilegi))
            return null;

        return privilegi.TryGetValue(CatalogKey.NormalizeName(nomePrivilegio), out var azione)
            ? azione
            : null;
    }

    /// <summary>La rete che tiene un jsonb malformato fuori dalla scheda: mai un'eccezione, mai una
    /// scheda che non si apre. Scarta le voci senza nome, riporta <see
    /// cref="CharacterFeature.Azione"/> a un valore ammesso (case-insensitive; <b>default null</b>
    /// se ignoto, non un valore indovinato), tronca <see cref="CharacterFeature.Nota"/> a <see
    /// cref="LunghezzaMassimaNota"/> caratteri, e scarta i duplicati per nome — tenendo la prima
    /// occorrenza, col confronto normalizzato di <see cref="CatalogKey.NormalizeName"/> così "Ira" e
    /// "IRA" non sopravvivono entrambe.</summary>
    public static List<CharacterFeature> Normalizza(IEnumerable<CharacterFeature?>? voci)
    {
        var risultato = new List<CharacterFeature>();
        if (voci is null) return risultato;

        var viste = new HashSet<string>(StringComparer.Ordinal);

        foreach (var v in voci)
        {
            if (v is null || string.IsNullOrWhiteSpace(v.Nome)) continue;

            var nome = v.Nome.Trim();
            var chiave = CatalogKey.NormalizeName(nome);
            if (!viste.Add(chiave)) continue; // duplicato: tiene la prima occorrenza

            var nota = v.Nota?.Trim() ?? string.Empty;
            if (nota.Length > LunghezzaMassimaNota) nota = nota[..LunghezzaMassimaNota];

            var azioneGrezza = v.Azione?.Trim().ToLowerInvariant() ?? string.Empty;
            var azione = TagAmmessiSet.Contains(azioneGrezza) ? azioneGrezza : null;

            risultato.Add(new CharacterFeature
            {
                Nome = nome,
                Nota = nota,
                Azione = azione,
                Risorsa = v.Risorsa,
                Attivabile = v.Attivabile,
            });
        }

        return risultato;
    }
}
