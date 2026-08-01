using DndCompanion.Models;
using DndCompanion.Models.Packages;

namespace DndCompanion.Services;

/// <summary>Che cosa accadrà a una voce del pacchetto quando l'import verrà confermato.</summary>
public enum ImportOutcome
{
    /// <summary>Nessuna riga della campagna le corrisponde: sarà creata.</summary>
    Create,

    /// <summary>Esiste una riga con la stessa provenienza e chi importa può modificarla.</summary>
    Update,

    /// <summary>Esiste con la stessa provenienza, ma AccessControl.CanEdit dice no: il server la
    /// rifiuterebbe, quindi non viene nemmeno inviata (§7).</summary>
    SkippedNoPermission,

    /// <summary>La corrispondenza è solo per nome: la riga è dell'utente e vince sul pacchetto
    /// (§6). Non si tocca — e soprattutto non si duplica: marcarla Create farebbe inserire una
    /// riga gemella, perché un source_id nullo non collide con il vincolo di unicità.</summary>
    SkippedLocalWins,

    /// <summary>Sezione senza tabella: i talenti (§5).</summary>
    NotImportable,
}

/// <summary>Una voce del pacchetto e il suo destino.</summary>
public sealed record ImportItem(string SourceId, string Name, ImportOutcome Outcome, string? ExistingRowId);

/// <summary>Una sezione dell'anteprima: un tipo di contenuto e le sue voci.</summary>
public sealed record ImportSection(string Title, IReadOnlyList<ImportItem> Items, string? Note = null)
{
    public int CreateCount => Items.Count(i => i.Outcome == ImportOutcome.Create);
    public int UpdateCount => Items.Count(i => i.Outcome == ImportOutcome.Update);

    /// <summary>Voci che non verranno scritte per una ragione o per l'altra, talenti esclusi:
    /// quelli hanno una sezione e una spiegazione tutta loro.</summary>
    public int SkippedCount => Items.Count(i =>
        i.Outcome is ImportOutcome.SkippedNoPermission or ImportOutcome.SkippedLocalWins);

    /// <summary>Le voci che l'esecuzione invierà davvero al server.</summary>
    public IEnumerable<ImportItem> Writable => Items.Where(i =>
        i.Outcome is ImportOutcome.Create or ImportOutcome.Update);
}

/// <summary>L'anteprima completa che l'utente conferma.</summary>
public sealed record ImportPlanResult(IReadOnlyList<ImportSection> Sections)
{
    public int TotalWrites => Sections.Sum(s => s.CreateCount + s.UpdateCount);
    public int TotalSkipped => Sections.Sum(s => s.SkippedCount);

    /// <summary>Nulla da scrivere: la schermata deve dirlo invece di offrire una conferma che
    /// non farebbe niente.</summary>
    public bool IsEmpty => TotalWrites == 0;
}

/// <summary>I cinque cataloghi di una campagna, come li legge il database. Raccolti in un tipo
/// solo perché sia il piano di import sia l'export ne hanno bisogno tutti insieme.</summary>
public sealed class CampaignCatalogs
{
    public List<Race> Races { get; init; } = new();
    public List<CharacterClass> Classes { get; init; } = new();
    public List<Spell> Spells { get; init; } = new();
    public List<Monster> Monsters { get; init; } = new();
    public List<Background> Backgrounds { get; init; } = new();
}

/// <summary>Calcola, senza scrivere nulla, che cosa un import produrrebbe (§7 dello spec).
/// Logica pura: nessuna rete, nessun database.</summary>
public static class PackageImportPlan
{
    /// <summary>Il destino di ogni voce di una sezione, confrontata con le righe già in campagna.
    ///
    /// I delegati invece dei Model concreti: i cinque cataloghi non condividono un'interfaccia
    /// comune (sono Model Postgrest indipendenti) e introdurne una per questo solo scopo
    /// significherebbe toccarli tutti.</summary>
    public static ImportSection ForSection<TPkg, TRow>(
        string title,
        IEnumerable<TPkg> packageEntries,
        Func<TPkg, string> packageIdOf,
        Func<TPkg, string> packageNameOf,
        IEnumerable<TRow> dbRows,
        Func<TRow, string?> sourceIdOf,
        Func<TRow, string> nameOf,
        Func<TRow, string> rowIdOf,
        Func<TRow, string?> addedByOf,
        bool isMaster,
        string? userId) where TRow : class
    {
        var righe = dbRows.ToList();

        // Due indici distinti perché le due corrispondenze hanno esiti DIVERSI: per provenienza
        // si aggiorna, per solo nome si lascia stare (§6). Fonderli farebbe sparire la differenza.
        var perProvenienza = righe
            .Where(r => !string.IsNullOrWhiteSpace(sourceIdOf(r)))
            .GroupBy(r => sourceIdOf(r)!.Trim(), StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

        var perNome = righe
            .GroupBy(r => CatalogKey.NormalizeName(nameOf(r)), StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

        var items = new List<ImportItem>();
        foreach (var entry in packageEntries)
        {
            var id = packageIdOf(entry);
            var nome = packageNameOf(entry);

            if (perProvenienza.TryGetValue(id, out var omologhe))
            {
                var rappresentante = CatalogMerge.Representative(omologhe, sourceIdOf, rowIdOf)!;
                var esito = AccessControl.CanEdit(isMaster, addedByOf(rappresentante), userId)
                    ? ImportOutcome.Update
                    : ImportOutcome.SkippedNoPermission;
                items.Add(new ImportItem(id, nome, esito, rowIdOf(rappresentante)));
                continue;
            }

            if (perNome.TryGetValue(CatalogKey.NormalizeName(nome), out var omonime))
            {
                var rappresentante = CatalogMerge.Representative(omonime, sourceIdOf, rowIdOf)!;
                items.Add(new ImportItem(id, nome, ImportOutcome.SkippedLocalWins, rowIdOf(rappresentante)));
                continue;
            }

            items.Add(new ImportItem(id, nome, ImportOutcome.Create, null));
        }

        return new ImportSection(title, items);
    }

    /// <summary>I talenti: mai importati, ma mai scartati in silenzio (§9). La dicitura cambia
    /// con la provenienza, perché cambia la conseguenza: dal pacchetto dell'app si leggono nella
    /// pagina Background, da un file dell'utente non finiscono da nessuna parte.</summary>
    public static ImportSection ForFeats(IEnumerable<PackageFeat> feats, bool fromAppPackage)
    {
        var items = feats
            .Select(f => new ImportItem(f.Id, f.Name, ImportOutcome.NotImportable, null))
            .ToList();

        var nota = fromAppPackage
            ? "Solo consultazione: i talenti si leggono nella pagina Background, accanto al talento d'origine che li richiama."
            : "Non importabile — resta nel tuo file: l'app non ha un catalogo dei talenti dove salvarli.";

        return new ImportSection("Talenti", items, nota);
    }

    /// <summary>Le classi, con l'avviso su quante sottoclassi il file porta.
    ///
    /// Dal 2026-08-01 le sottoclassi non sono più scartate: <c>PackageRowMerge.NuovaClasse</c> e
    /// <c>ApplicaClasse</c> le scrivono nella colonna <c>classes.subclasses</c> (v.
    /// <see cref="SubclassText"/>) insieme al resto della classe, e non hanno una riga propria
    /// nell'anteprima — un conteggio a parte le farebbe sembrare una sezione distinta, mentre non
    /// lo sono. L'avviso resta comunque, per la stessa regola dei talenti (§9): senza, chi ha
    /// scritto una sottoclasse propria non avrebbe modo di sapere, guardando l'anteprima, che verrà
    /// letta e portata a catalogo.</summary>
    public static ImportSection ForClasses(
        CatalogPackage package, CampaignCatalogs existing, bool isMaster, string? userId)
    {
        var sezione = ForSection("Classi", package.Classes, p => p.Id, p => p.Name,
            existing.Classes, r => r.SourceId, r => r.Name, r => r.Id, r => r.AddedBy,
            isMaster, userId);

        var numeroSottoclassi = package.Classes
            .Where(c => c is not null)
            .Sum(c => c.Subclasses.Count);
        if (numeroSottoclassi == 0) return sezione;

        var quante = numeroSottoclassi == 1 ? "1 sottoclasse" : $"{numeroSottoclassi} sottoclassi";
        return sezione with
        {
            Note = $"Il file porta {quante}: finiscono nel catalogo insieme alla classe che le "
                   + "dichiara, non in una riga propria.",
        };
    }

    /// <summary>L'anteprima completa. Le sezioni ci sono tutte anche quando sono vuote: chi
    /// importa deve poter constatare che il file non conteneva mostri, non dedurlo da un'assenza.</summary>
    public static ImportPlanResult Build(
        CatalogPackage package,
        CampaignCatalogs existing,
        bool isMaster,
        string? userId)
    {
        var sezioni = new List<ImportSection>
        {
            ForSection("Specie", package.Species, p => p.Id, p => p.Name,
                existing.Races, r => r.SourceId, r => r.Name, r => r.Id, r => r.AddedBy,
                isMaster, userId),

            ForClasses(package, existing, isMaster, userId),

            ForSection("Background", package.Backgrounds, p => p.Id, p => p.Name,
                existing.Backgrounds, r => r.SourceId, r => r.Name, r => r.Id, r => r.AddedBy,
                isMaster, userId),

            ForSection("Incantesimi", package.Spells, p => p.Id, p => p.Name,
                existing.Spells, r => r.SourceId, r => r.Name, r => r.Id, r => r.AddedBy,
                isMaster, userId),

            ForSection("Mostri", package.Monsters, p => p.Id, p => p.Name,
                existing.Monsters, r => r.SourceId, r => r.Name, r => r.Id, r => r.AddedBy,
                isMaster, userId),

            ForFeats(package.Feats, CatalogKey.IsFromAppPackage(package.Id + "/")),
        };

        return new ImportPlanResult(sezioni);
    }
}
