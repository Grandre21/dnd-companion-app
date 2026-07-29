using DndCompanion.Models;

namespace DndCompanion.Services;

/// <summary>Che cosa una rimozione per provenienza toglierebbe, prima di toglierlo. Porta con sé gli
/// <b>id esatti</b> da cancellare, non un criterio da rivalutare: è la garanzia che l'insieme
/// cancellato sia quello mostrato (§8 dello spec).
///
/// <c>BlockedByPermission</c> non è un dettaglio: la rimozione rispetta <see cref="AccessControl.CanEdit"/>
/// ed è quindi quasi sempre PARZIALE.</summary>
public sealed record CatalogRemoval(
    string Prefix,
    List<string> RaceIds, List<string> ClassIds, List<string> BackgroundIds,
    List<string> SpellIds, List<string> MonsterIds,
    int BlockedByPermission)
{
    public int Total => RaceIds.Count + ClassIds.Count + BackgroundIds.Count
                        + SpellIds.Count + MonsterIds.Count;

    /// <summary>Tutti gli id che la cancellazione invierà, nell'ordine dei cinque cataloghi.</summary>
    public IEnumerable<string> AllIds =>
        RaceIds.Concat(ClassIds).Concat(BackgroundIds).Concat(SpellIds).Concat(MonsterIds);
}

/// <summary>Quali righe di catalogo appartengono a una provenienza e quali di esse chi opera può
/// davvero cancellare. Logica pura: nessuna rete, nessun database.
///
/// Vive qui e non nel blocco <c>@code</c> della schermata perché custodisce i tre invarianti
/// dell'operazione più distruttiva dello spec (§8): la selezione avviene <b>in memoria</b> con un
/// confronto ordinale e mai con un <c>LIKE</c> costruito col testo digitato (dove <c>_</c> e
/// <c>%</c> sarebbero wildcard); il pacchetto distribuito con l'app non è rimovibile; e le righe che
/// i permessi bloccano si contano a parte invece di finire fra gli id da cancellare.</summary>
public static class CatalogRemovalPlan
{
    /// <summary>La provenienza come la si confronta: senza spazi e senza lo slash finale, che
    /// l'utente copia volentieri dall'identificatore di una voce. Senza questa piega,
    /// «mio-pacchetto/» cercherebbe il prefisso «mio-pacchetto//» e non troverebbe nulla —
    /// un falso "niente da rimuovere" indistinguibile da quello vero.</summary>
    public static string NormalizePrefix(string? raw)
        => (raw ?? string.Empty).Trim().TrimEnd('/');

    /// <summary>Vero se la provenienza indicata si può rimuovere in blocco. Il pacchetto dell'app
    /// non si può: sarebbe il danno della materializzazione moltiplicato per N righe in un colpo
    /// solo, e <see cref="AccessControl.CanEdit"/> non farebbe da freno — quelle righe nascono con
    /// l'<c>added_by</c> del giocatore che le ha usate.</summary>
    public static bool IsRemovablePrefix(string? prefix)
    {
        var normalizzato = NormalizePrefix(prefix);
        return normalizzato.Length > 0 && !CatalogKey.IsFromAppPackage(normalizzato + "/");
    }

    /// <summary>Le righe dei cinque cataloghi che appartengono alla provenienza indicata, divise
    /// fra quelle che verranno cancellate e il conteggio di quelle che i permessi bloccano.</summary>
    public static CatalogRemoval Build(
        CampaignCatalogs catalogs, string prefix, bool isMaster, string? userId)
    {
        var conPrefisso = NormalizePrefix(prefix) + "/";

        // Confronto ordinale in memoria, MAI un LIKE: `_` e `%` digitati dall'utente sarebbero
        // wildcard e la DELETE colpirebbe righe che l'anteprima non ha mai contato.
        bool DaQuestaProvenienza(string? sourceId) =>
            sourceId is not null && sourceId.StartsWith(conPrefisso, StringComparison.Ordinal);

        bool Rimovibile(string? addedBy) => AccessControl.CanEdit(isMaster, addedBy, userId);

        var razze = catalogs.Races.Where(r => DaQuestaProvenienza(r.SourceId)).ToList();
        var classi = catalogs.Classes.Where(c => DaQuestaProvenienza(c.SourceId)).ToList();
        var background = catalogs.Backgrounds.Where(b => DaQuestaProvenienza(b.SourceId)).ToList();
        var incantesimi = catalogs.Spells.Where(s => DaQuestaProvenienza(s.SourceId)).ToList();
        var mostri = catalogs.Monsters.Where(m => DaQuestaProvenienza(m.SourceId)).ToList();

        var bloccate =
            razze.Count(r => !Rimovibile(r.AddedBy)) +
            classi.Count(c => !Rimovibile(c.AddedBy)) +
            background.Count(b => !Rimovibile(b.AddedBy)) +
            incantesimi.Count(s => !Rimovibile(s.AddedBy)) +
            mostri.Count(m => !Rimovibile(m.AddedBy));

        // Solo le righe che verranno DAVVERO rimosse finiscono negli elenchi: quelle bloccate dai
        // permessi si contano a parte e non si tenta nemmeno di cancellarle.
        return new CatalogRemoval(
            NormalizePrefix(prefix),
            razze.Where(r => Rimovibile(r.AddedBy)).Select(r => r.Id).ToList(),
            classi.Where(c => Rimovibile(c.AddedBy)).Select(c => c.Id).ToList(),
            background.Where(b => Rimovibile(b.AddedBy)).Select(b => b.Id).ToList(),
            incantesimi.Where(s => Rimovibile(s.AddedBy)).Select(s => s.Id).ToList(),
            mostri.Where(m => Rimovibile(m.AddedBy)).Select(m => m.Id).ToList(),
            bloccate);
    }

    /// <summary>Quante delle righe che <paramref name="before"/> aveva promesso di cancellare sono
    /// ancora lì dopo l'operazione. È il solo resoconto onesto possibile: il <c>Delete</c> di questa
    /// libreria non dice quante righe ha tolto e un <c>Delete</c> bloccato dalla RLS "riesce" a vuoto.
    ///
    /// Si contano gli <b>id congelati</b>, non le righe che il criterio raccoglie adesso: fra
    /// l'anteprima e il riconteggio possono esserne comparse di nuove — un import della stessa
    /// provenienza, un altro giocatore che scrive in parallelo — e confonderle con righe
    /// sopravvissute darebbe un resoconto sbagliato, fino al conteggio negativo.</summary>
    public static int StillPresent(CatalogRemoval before, CatalogRemoval after)
    {
        var rimasti = new HashSet<string>(after.AllIds, StringComparer.Ordinal);
        return before.AllIds.Count(rimasti.Contains);
    }
}
