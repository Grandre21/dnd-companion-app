using DndCompanion.Models;
using DndCompanion.Models.Packages;

namespace DndCompanion.Services;

/// <summary>
/// Vista di un privilegio pronta per la scheda: il nome (che è la chiave), la nota del giocatore, il
/// tag di economia d'azione EFFETTIVO — già risolto secondo la precedenza fra annotazione dell'utente
/// e tabella curata — la sua origine, l'eventuale contatore collegato, e il livello a cui si è
/// sbloccato (<c>null</c> per talenti e voci proprie, che non hanno un livello di sblocco).
/// </summary>
public sealed record VistaPrivilegio(
    string Nome,
    string Nota,
    string? Azione,
    string Origine,               // "classe" | "sottoclasse" | "talento" | "propria"
    ClassResource? Contatore,
    bool Attivabile,
    int? SbloccatoAlLivello,
    string? RisorsaAnnotata);     // il valore grezzo scritto dall'utente, anche se non risolve
                                   // a nessuna ClassResource: senza, la modifica lo cancella

/// <summary>Un raggruppamento di privilegi per economia d'azione, pronto per il render: l'etichetta è
/// già quella da mostrare, le voci sono già ordinate (v. <see cref="CharacterFeatureJoin.Raggruppa"/>).</summary>
public sealed record GruppoPrivilegi(string Tag, string Etichetta, IReadOnlyList<VistaPrivilegio> Voci);

/// <summary>
/// JOIN puro in memoria fra i privilegi DERIVATI dal pacchetto SRD — quelli di classe
/// (<see cref="ClassProgression.PrivilegiFinoAl"/>), di sottoclasse
/// (<see cref="SubclassCatalog.PrivilegiFinoAl"/>) e i talenti riconosciuti nel testo libero
/// (<see cref="CharacterManualJoin.TalentiRiconosciuti"/>) — e le annotazioni del giocatore
/// (<see cref="CharacterFeature"/>, elemento del jsonb <c>characters.character_features</c>) più i
/// contatori di classe (<see cref="ClassResource"/>). Estratto per essere testabile, sullo stesso
/// modello di <see cref="CharacterSpellJoin"/> e <see cref="CharacterManualJoin"/>: nessuno stato,
/// nessuna I/O.
///
/// <b>Differenza voluta rispetto a <see cref="CharacterSpellJoin"/>:</b> lì un incantesimo orfano (che
/// non trova il proprio incantesimo di catalogo) si scarta. Qui una <see cref="CharacterFeature"/> la
/// cui chiave non corrisponde a nessun privilegio derivato NON si scarta: diventa una voce con
/// <c>Origine = "propria"</c>, in coda. È il solo modo con cui il giocatore annota ciò che il
/// pacchetto SRD tiene in un'unica stringa e non sa separare — i tratti di specie, in testa a tutti
/// (v. piano 2026-08-08, «I privilegi», regola 2).
/// </summary>
public static class CharacterFeatureJoin
{
    private static readonly string[] OrdineTagConEtichette =
        { "azione", "bonus", "reazione", "turno" };

    private static readonly Dictionary<string, string> Etichette = new(StringComparer.Ordinal)
    {
        ["azione"] = "Azione",
        ["bonus"] = "Azione bonus",
        ["reazione"] = "Reazione",
        ["turno"] = "Una volta per turno",
        ["passivo"] = "Passivi",
    };

    /// <summary>
    /// Costruisce l'elenco dei privilegi visibili in scheda, in tre passi:
    /// 1. i privilegi DERIVATI — classe, poi sottoclasse, poi talenti riconosciuti nel testo — ognuno
    ///    unito (per <see cref="CatalogKey.NormalizeName"/>) alla propria annotazione, se esiste;
    /// 2. le annotazioni che non hanno trovato un derivato diventano voci <c>propria</c>, in coda.
    /// Un nome derivato già visto (per esempio ripetuto fra classe e sottoclasse) non genera una
    /// seconda voce: vince la prima fonte incontrata — stessa regola di "prima occorrenza vince" di
    /// <see cref="CharacterFeatureRules.Normalizza"/>.
    /// </summary>
    public static IReadOnlyList<VistaPrivilegio> Costruisci(
        string? classProgressionText, int livello, string? nomeClasse,
        PackageSubclass? sottoclasse, string? testoTalenti, IReadOnlyList<PackageFeat> catalogoTalenti,
        IEnumerable<CharacterFeature>? annotazioni, IEnumerable<ClassResource>? contatori)
    {
        var contatoriList = (contatori ?? Enumerable.Empty<ClassResource>())
            .Where(c => c is not null)
            .ToList();

        // Annotazioni indicizzate per nome normalizzato (prima occorrenza vince), e mantenute anche
        // in una lista che preserva l'ordine di ingresso: serve al passo 2, per rendere le voci
        // proprie nell'ordine in cui il giocatore le ha scritte.
        var perChiave = new Dictionary<string, CharacterFeature>(StringComparer.Ordinal);
        var listaAnnotazioni = new List<CharacterFeature>();
        foreach (var a in annotazioni ?? Enumerable.Empty<CharacterFeature>())
        {
            if (a is null || string.IsNullOrWhiteSpace(a.Nome)) continue;
            var chiave = CatalogKey.NormalizeName(a.Nome);
            if (perChiave.TryAdd(chiave, a)) listaAnnotazioni.Add(a);
        }

        var risultato = new List<VistaPrivilegio>();
        var chiaviDerivate = new HashSet<string>(StringComparer.Ordinal);

        void AggiungiDerivati(IEnumerable<ClassLevelRow> righe, string origine)
        {
            foreach (var riga in righe)
            {
                foreach (var nome in riga.Privilegi)
                {
                    var chiave = CatalogKey.NormalizeName(nome);
                    if (!chiaviDerivate.Add(chiave)) continue; // già derivato da un'altra fonte

                    perChiave.TryGetValue(chiave, out var annotazione);
                    risultato.Add(CostruisciDerivata(nome, origine, riga.Livello, nomeClasse, annotazione, contatoriList));
                }
            }
        }

        AggiungiDerivati(ClassProgression.PrivilegiFinoAl(classProgressionText, livello), "classe");
        AggiungiDerivati(SubclassCatalog.PrivilegiFinoAl(sottoclasse, livello), "sottoclasse");

        foreach (var talento in CharacterManualJoin.TalentiRiconosciuti(testoTalenti, catalogoTalenti))
        {
            var chiave = CatalogKey.NormalizeName(talento.Name);
            if (!chiaviDerivate.Add(chiave)) continue;

            perChiave.TryGetValue(chiave, out var annotazione);
            risultato.Add(CostruisciTalento(talento, nomeClasse, annotazione, contatoriList));
        }

        // Regola 2: un'annotazione la cui chiave non corrisponde a nessun derivato è una voce
        // propria, mai scartata — il meccanismo con cui il giocatore annota i tratti di specie.
        foreach (var annotazione in listaAnnotazioni)
        {
            var chiave = CatalogKey.NormalizeName(annotazione.Nome);
            if (chiaviDerivate.Contains(chiave)) continue;
            risultato.Add(CostruisciPropria(annotazione, nomeClasse, contatoriList));
        }

        return risultato;
    }

    /// <summary>
    /// Raggruppa per economia d'azione, nell'ordine <c>azione</c>, <c>bonus</c>, <c>reazione</c>,
    /// <c>turno</c>, senza tag, e <c>passivo</c> per ultimo — il chiamante lo rende in una sezione a
    /// parte. I gruppi vuoti sono omessi. Dentro ogni gruppo l'ordine è quello di sblocco (livello
    /// crescente, i <c>null</c> in fondo), poi alfabetico.
    /// </summary>
    public static IReadOnlyList<GruppoPrivilegi> Raggruppa(IReadOnlyList<VistaPrivilegio> voci)
    {
        var gruppi = new List<GruppoPrivilegi>();

        void AggiungiGruppo(string? tag, string tagGruppo, string etichetta)
        {
            var vociDelGruppo = voci
                .Where(v => v.Azione == tag)
                .OrderBy(v => v.SbloccatoAlLivello ?? int.MaxValue)
                .ThenBy(v => v.Nome, StringComparer.Ordinal)
                .ToList();
            if (vociDelGruppo.Count == 0) return;
            gruppi.Add(new GruppoPrivilegi(tagGruppo, etichetta, vociDelGruppo));
        }

        foreach (var tag in OrdineTagConEtichette)
            AggiungiGruppo(tag, tag, Etichette[tag]);

        // Senza tag ("da classificare"): Azione è null. Tag del gruppo vuoto, perché non ne ha uno.
        AggiungiGruppo(null, string.Empty, "Da classificare");

        AggiungiGruppo("passivo", "passivo", Etichette["passivo"]);

        return gruppi;
    }

    /// <summary>Il tag effettivo di un privilegio: quello scritto dall'utente se valorizzato,
    /// altrimenti quello suggerito dalla tabella curata, altrimenti null. L'utente vince sempre.</summary>
    private static string? SceltaAzione(CharacterFeature? annotazione, string? nomeClasse, string nomePrivilegio)
    {
        if (!string.IsNullOrWhiteSpace(annotazione?.Azione)) return annotazione!.Azione;
        return CharacterFeatureRules.AzioneSuggerita(nomeClasse, nomePrivilegio);
    }

    /// <summary>Il contatore collegato: la <see cref="ClassResource"/> il cui nome normalizzato
    /// coincide con <see cref="CharacterFeature.Risorsa"/>; se <c>Risorsa</c> è vuoto/nullo si tenta
    /// il nome del privilegio stesso (l'Ira trova da sola i propri pallini). Null se non trova
    /// nulla.</summary>
    private static ClassResource? TrovaContatore(
        CharacterFeature? annotazione, string nomePrivilegio, IReadOnlyList<ClassResource> contatori)
    {
        var nomeRisorsa = !string.IsNullOrWhiteSpace(annotazione?.Risorsa) ? annotazione!.Risorsa : nomePrivilegio;
        var chiave = CatalogKey.NormalizeName(nomeRisorsa);
        return contatori.FirstOrDefault(c => CatalogKey.NormalizeName(c.Nome) == chiave);
    }

    private static VistaPrivilegio CostruisciDerivata(
        string nome, string origine, int livello, string? nomeClasse,
        CharacterFeature? annotazione, IReadOnlyList<ClassResource> contatori)
    {
        var nota = annotazione?.Nota ?? string.Empty;
        var azione = SceltaAzione(annotazione, nomeClasse, nome);
        var contatore = TrovaContatore(annotazione, nome, contatori);
        return new VistaPrivilegio(
            nome, nota, azione, origine, contatore, annotazione?.Attivabile ?? false, livello, annotazione?.Risorsa);
    }

    /// <summary>Per i talenti la nota si preimposta alla <c>Description</c> ufficiale del pacchetto
    /// quando l'utente non ha scritto la propria; l'annotazione dell'utente, se c'è, vince.</summary>
    private static VistaPrivilegio CostruisciTalento(
        PackageFeat talento, string? nomeClasse, CharacterFeature? annotazione, IReadOnlyList<ClassResource> contatori)
    {
        var nota = !string.IsNullOrWhiteSpace(annotazione?.Nota) ? annotazione!.Nota : talento.Description;
        var azione = SceltaAzione(annotazione, nomeClasse, talento.Name);
        var contatore = TrovaContatore(annotazione, talento.Name, contatori);
        return new VistaPrivilegio(
            talento.Name, nota, azione, "talento", contatore, annotazione?.Attivabile ?? false, null,
            annotazione?.Risorsa);
    }

    private static VistaPrivilegio CostruisciPropria(
        CharacterFeature annotazione, string? nomeClasse, IReadOnlyList<ClassResource> contatori)
    {
        var azione = SceltaAzione(annotazione, nomeClasse, annotazione.Nome);
        var contatore = TrovaContatore(annotazione, annotazione.Nome, contatori);
        return new VistaPrivilegio(
            annotazione.Nome, annotazione.Nota, azione, "propria", contatore, annotazione.Attivabile, null,
            annotazione.Risorsa);
    }
}
