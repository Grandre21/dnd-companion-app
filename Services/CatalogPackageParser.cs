using System.Text.Json;
using System.Text.Json.Serialization;
using DndCompanion.Models.Packages;

namespace DndCompanion.Services;

/// <summary>Contesto di serializzazione generato a compile-time: il progetto pubblica con
/// TrimMode=full, dove gli overload a reflection di System.Text.Json producono warning.</summary>
[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    WriteIndented = true,
    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(CatalogPackage))]
internal partial class CatalogPackageJsonContext : JsonSerializerContext { }

/// <summary>Esito della lettura di un pacchetto: o il pacchetto, o gli errori che lo hanno
/// respinto. Gli avvisi non impediscono l'uso.</summary>
public sealed record ParseResult(
    CatalogPackage? Package,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings);

/// <summary>Lettura e validazione di un pacchetto di dati (§5 dello spec). Logica pura:
/// nessuna rete, nessun accesso al database.</summary>
public static class CatalogPackageParser
{
    /// <summary>Versione di schema che questo codice sa leggere.</summary>
    public const int SupportedSchemaVersion = 1;

    /// <summary>Prefisso degli identificatori del pacchetto distribuito con l'app (§6).</summary>
    public const string AppPackageId = "srd-2024-it";

    /// <summary>Legge e valida un pacchetto di dati dal suo JSON.</summary>
    /// <param name="json">Il testo del file.</param>
    /// <param name="èIlManualeDellApp">Vero solo per il caricamento di <c>wwwroot/data/srd-2024-it.json</c>
    /// (<see cref="CatalogService"/>). È l'unico proprietario legittimo del prefisso
    /// <see cref="AppPackageId"/>: con il default <c>false</c>, un file scritto a mano che dichiari
    /// quell'id — o un id di voce che ci comincia — viene respinto invece di essere accettato.</param>
    public static ParseResult Parse(string? json, bool èIlManualeDellApp = false)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (string.IsNullOrWhiteSpace(json))
            return new ParseResult(null, new[] { "Il file è vuoto." }, warnings);

        CatalogPackage? package;
        try
        {
            package = JsonSerializer.Deserialize(json, CatalogPackageJsonContext.Default.CatalogPackage);
        }
        catch (JsonException ex)
        {
            return new ParseResult(null, new[] { $"Il file non è un JSON valido: {ex.Message}" }, warnings);
        }

        if (package is null)
            return new ParseResult(null, new[] { "Il file non contiene un pacchetto." }, warnings);

        NormalizeLists(package);
        TrimIdsAndNames(package);

        // Buco di sicurezza chiuso qui, non a valle: un file che si spaccia per il manuale produce
        // righe indistinguibili da quelle ufficiali — CatalogKey.IsFromAppPackage le riconosce dal
        // solo prefisso, non da chi le ha scritte — quindi l'interfaccia le rende di sola lettura
        // (nemmeno il master può modificarle) e CatalogRemovalPlan.IsRemovablePrefix rifiuta proprio
        // quel prefisso in blocco: senza questo controllo restano indelebili, recuperabili solo dal
        // database. Deve girare DOPO TrimIdsAndNames: un id con spazi ai margini deve essere
        // riconosciuto lo stesso, non sfuggire per un dettaglio di battitura.
        if (!èIlManualeDellApp)
            CheckNonImpersonaIlManuale(package, errors);

        if (package.SchemaVersion != SupportedSchemaVersion)
        {
            errors.Add($"Versione di schema {package.SchemaVersion} non supportata " +
                       $"(questa app legge la versione {SupportedSchemaVersion}).");
            return new ParseResult(null, errors, warnings);
        }

        if (string.IsNullOrWhiteSpace(package.Id))
            errors.Add("Il pacchetto non ha un identificatore ('id').");

        ValidateEntries(package, errors);

        if (!string.Equals(package.Language, "it", StringComparison.OrdinalIgnoreCase))
            warnings.Add($"Il pacchetto è in lingua '{package.Language}': alcune funzioni che " +
                         "dipendono dalla lingua, come il filtro per classe, potrebbero non trovarlo.");

        return errors.Count > 0
            ? new ParseResult(null, errors, warnings)
            : new ParseResult(package, errors, warnings);
    }

    // System.Text.Json non impone a runtime la non-nullabilità di C#: un JSON con una sezione
    // esplicitamente "null" (es. "species": null) sovrascrive il default `= new()` del modello.
    // Ripristina qui l'invariante "le sei liste non sono mai null" prima di iterarle, così un
    // pacchetto con sezioni assenti/nulle produce un pacchetto senza quelle voci, non un crash.
    //
    // Lo stesso vale per le liste ANNIDATE dentro le singole voci (es. "abilityScores": null su un
    // background): senza questo secondo passaggio un pacchetto così supera il parser e poi va in
    // crash altrove (es. string.Join su Pages/Backgrounds.razor, fuori dal try/catch del rendering).
    // Le voci null dell'array le lascia stare: le gestisce ValidateEntries, non questo metodo.
    private static void NormalizeLists(CatalogPackage p)
    {
        p.Species ??= new();
        p.Backgrounds ??= new();
        p.Feats ??= new();
        p.Classes ??= new();
        p.Spells ??= new();
        p.Monsters ??= new();

        foreach (var b in p.Backgrounds)
        {
            if (b is null) continue;
            b.AbilityScores ??= new();
            b.SkillProficiencies ??= new();
        }

        foreach (var c in p.Classes)
        {
            if (c is null) continue;
            c.SavingThrows ??= new();
            c.Levels ??= new();
            c.Subclasses ??= new();
            if (c.SkillChoices is not null) c.SkillChoices.From ??= new();
            foreach (var lvl in c.Levels)
            {
                if (lvl is null) continue;
                lvl.Features ??= new();
                lvl.SpellSlots ??= new();
            }
            foreach (var sub in c.Subclasses)
            {
                if (sub is null) continue;
                sub.Levels ??= new();
                foreach (var lvl in sub.Levels)
                {
                    if (lvl is null) continue;
                    lvl.Features ??= new();
                    lvl.SpellSlots ??= new();
                }
            }
        }

        foreach (var s in p.Spells)
        {
            if (s is null) continue;
            s.Classes ??= new();
        }
    }

    // Il confine giusto per normalizzare id e nomi è la lettura, non i punti a valle: CatalogKey.For
    // fa già il trim del sourceId letto dal database, e senza lo stesso trim qui un pacchetto con
    // spazi accidentali (" elfo ") romperebbe l'asimmetria — CatalogMerge.HiddenPackageIds confronta
    // id grezzi, quindi una voce con id non trimmato non verrebbe mai riconosciuta come duplicata.
    // Il trim non cambia mai se una voce è "vuota" (IsNullOrWhiteSpace è invariante al trim), quindi
    // non altera quali voci Check() respinge.
    private static void TrimIdsAndNames(CatalogPackage p)
    {
        p.Id = Trimmed(p.Id);
        p.Name = Trimmed(p.Name);

        foreach (var x in p.Species)
        {
            if (x is null) continue;
            x.Id = Trimmed(x.Id);
            x.Name = Trimmed(x.Name);
        }
        foreach (var x in p.Backgrounds)
        {
            if (x is null) continue;
            x.Id = Trimmed(x.Id);
            x.Name = Trimmed(x.Name);
        }
        foreach (var x in p.Feats)
        {
            if (x is null) continue;
            x.Id = Trimmed(x.Id);
            x.Name = Trimmed(x.Name);
        }
        foreach (var x in p.Classes)
        {
            if (x is null) continue;
            x.Id = Trimmed(x.Id);
            x.Name = Trimmed(x.Name);
            // Le sottoclassi passano dallo stesso trim: sono una sezione a tutti gli effetti (id e
            // nome, e l'export le riporta). Il nome, in più, finisce dentro un `<option value>` che
            // il `<select>` confronta per stringa esatta con `Draft.Subclass`: un « Campione » con
            // gli spazi si salverebbe così e poi non combacerebbe più con la propria opzione.
            foreach (var s in x.Subclasses)
            {
                if (s is null) continue;
                s.Id = Trimmed(s.Id);
                s.Name = Trimmed(s.Name);
            }
        }
        foreach (var x in p.Spells)
        {
            if (x is null) continue;
            x.Id = Trimmed(x.Id);
            x.Name = Trimmed(x.Name);
        }
        foreach (var x in p.Monsters)
        {
            if (x is null) continue;
            x.Id = Trimmed(x.Id);
            x.Name = Trimmed(x.Name);
        }
    }

    private static string Trimmed(string? s) => s?.Trim() ?? string.Empty;

    // Ogni voce deve avere id e nome: senza id non sopravvive all'import (§4.3),
    // senza nome non è confrontabile. L'errore cita il nome, o la posizione se manca anche quello.
    // Un elemento null nell'array (es. "species": [null]) è trattato come voce senza id e senza
    // nome, invece di far crashare la lettura del pacchetto.
    private static void ValidateEntries(CatalogPackage p, List<string> errors)
    {
        Check(p.Species.Select(x => x is null ? ("", "") : (x.Id, x.Name)), "specie", errors);
        Check(p.Backgrounds.Select(x => x is null ? ("", "") : (x.Id, x.Name)), "background", errors);
        Check(p.Feats.Select(x => x is null ? ("", "") : (x.Id, x.Name)), "talenti", errors);
        Check(p.Classes.Select(x => x is null ? ("", "") : (x.Id, x.Name)), "classi", errors);
        Check(p.Spells.Select(x => x is null ? ("", "") : (x.Id, x.Name)), "incantesimi", errors);
        Check(p.Monsters.Select(x => x is null ? ("", "") : (x.Id, x.Name)), "mostri", errors);

        // Le sottoclassi vivono annidate, quindi si controllano una classe per volta: l'unicità
        // dell'id vale dentro la classe, perché non c'è tabella dove due sottoclassi di classi
        // diverse potrebbero collidere. Il controllo c'è per la stessa ragione delle altre sezioni:
        // senza nome la voce non è confrontabile con quella scelta sulla scheda.
        foreach (var c in p.Classes)
        {
            if (c is null || c.Subclasses.Count == 0) continue;
            var sezione = string.IsNullOrWhiteSpace(c.Name)
                ? "sottoclassi"
                : $"sottoclassi di {c.Name}";
            Check(c.Subclasses.Select(s => s is null ? ("", "") : (s.Id, s.Name)), sezione, errors);
        }
    }

    // Il database impone UNIQUE (campaign_id, source_id): due voci con lo stesso id nello stesso
    // pacchetto passerebbero questa validazione e farebbero fallire l'import a metà in Fase 2, invece
    // di essere respinte subito con l'indicazione della voce colpevole.
    private static void Check(IEnumerable<(string Id, string Name)> entries, string section, List<string> errors)
    {
        var index = 0;
        var idsVisti = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (id, name) in entries)
        {
            var etichetta = string.IsNullOrWhiteSpace(name) ? $"posizione {index + 1}" : name;
            if (string.IsNullOrWhiteSpace(id))
                errors.Add($"Sezione '{section}': la voce «{etichetta}» non ha un identificatore.");
            else if (!idsVisti.Add(id))
                errors.Add($"Sezione '{section}': l'identificatore '{id}' compare più volte (voce «{etichetta}»).");
            if (string.IsNullOrWhiteSpace(name))
                errors.Add($"Sezione '{section}': la voce in posizione {index + 1} non ha un nome.");
            index++;
        }
    }

    // Il prefisso del manuale è "<AppPackageId>/": un file di terze parti che lo usi produrrebbe
    // righe indistinguibili, per CatalogKey.IsFromAppPackage, da quelle davvero importate dal
    // manuale — e quelle righe sono di sola lettura anche per il master (§6) e immuni a "Rimuovi un
    // import" (CatalogRemovalPlan.IsRemovablePrefix rifiuta proprio quel prefisso).
    //
    // Il divieto copre le sole sezioni che l'import **scrive**: specie, background, incantesimi,
    // mostri, classi. Ne restano fuori le sottoclassi e i talenti, e l'asimmetria è voluta.
    //
    // Il criterio è uno: l'immunità nasce dal `source_id`, quindi il divieto vale dove l'id *diventa*
    // un `source_id` (PackageRowMerge.NuovaClasse e sorelle). Un id di sottoclasse non lo diventa mai
    // — vive dentro il testo della colonna `subclasses` — e un talento non ha nemmeno una tabella:
    // PackageImportPlan.ForFeats lo marca NotImportable e nessun PackageRowMerge lo tocca. Per
    // entrambi, dunque, né righe di sola lettura né immunità a "Rimuovi un import".
    //
    // Vietarli costava invece la compatibilità con i file già esportati dal client **online**, che
    // porta gli id SRD di sottoclassi e talenti verbatim: sarebbero stati respinti per intero, con un
    // errore che incolpa il file dell'utente — e il service worker non fa skipWaiting, quindi quei
    // file continueranno a nascere anche dopo il rilascio. Il divieto non comprava niente: CampaignExport
    // non conserva comunque quel prefisso al primo riesporto (AssignIds, regola 1).
    //
    // Il giorno in cui i talenti avranno una tabella, il divieto va rimesso su quella sezione insieme
    // alla tabella: è quel giorno che il loro id comincerà a diventare un `source_id`.
    //
    // Un solo consumatore legge l'id di sottoclasse per decidere qualcosa:
    // CampaignExport.ContieneMaterialeDiManuale, che ci riconosce la prosa SRD sopravvissuta a
    // «Duplica e modifica» per non emettere un file senza attribuzione. Sbaglia solo per eccesso —
    // un file di terzi che dichiari quel prefisso si porta dietro una licenza di troppo — e fra i due
    // errori è quello innocuo.
    private static void CheckNonImpersonaIlManuale(CatalogPackage p, List<string> errors)
    {
        const string prefisso = AppPackageId + "/";

        // Uguaglianza **e** prefisso: `IsFromAppPackage` confronta il prefisso, quindi un pacchetto
        // che si chiami «srd-2024-it/mio» supererebbe un controllo di sola uguaglianza e poi
        // `PackageImportPlan.Build` — che interroga `IsFromAppPackage(package.Id + "/")` — lo
        // tratterebbe come il manuale, etichettando le sue voci «solo consultazione». Le due domande
        // vanno poste nello stesso modo, o il divieto e la conseguenza divergono.
        if (p.Id == AppPackageId || (p.Id?.StartsWith(prefisso, StringComparison.Ordinal) ?? false))
        {
            errors.Add($"L'id del pacchetto non può essere '{AppPackageId}' né cominciare per " +
                       $"'{prefisso}': è riservato al manuale distribuito con l'app. Scegli un id " +
                       "diverso per il tuo pacchetto.");
        }

        void Vieta(string? id, string sezione)
        {
            if (id is not null && id.StartsWith(prefisso, StringComparison.Ordinal))
                errors.Add($"Sezione '{sezione}': l'identificatore '{id}' usa il prefisso '{prefisso}', " +
                           "riservato al manuale dell'app. Scegline uno tuo.");
        }

        foreach (var x in p.Species) if (x is not null) Vieta(x.Id, "specie");
        foreach (var x in p.Backgrounds) if (x is not null) Vieta(x.Id, "background");
        foreach (var x in p.Spells) if (x is not null) Vieta(x.Id, "incantesimi");
        foreach (var x in p.Monsters) if (x is not null) Vieta(x.Id, "mostri");

        foreach (var c in p.Classes) if (c is not null) Vieta(c.Id, "classi");
    }
}
