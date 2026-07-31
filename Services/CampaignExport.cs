using System.Text;
using System.Text.Json;
using DndCompanion.Models;
using DndCompanion.Models.Packages;

namespace DndCompanion.Services;

/// <summary>Trasforma i cataloghi di una campagna nel formato di scambio (§5 dello spec), così che
/// un gruppo possa portarsi via i propri dati o passarli a un altro tavolo. Logica pura.</summary>
public static class CampaignExport
{
    /// <summary>Prefisso degli id prodotti dall'export. Deliberatamente diverso da
    /// CatalogPackageParser.AppPackageId: un file con quell'id renderebbe di sola lettura le
    /// proprie voci al reimport (§6).</summary>
    private const string Prefix = "campagna-";

    public static string PackageIdFor(string campaignName)
    {
        var slug = Slug(campaignName);
        return Prefix + (string.IsNullOrEmpty(slug) ? "senza-nome" : slug);
    }

    // Nome normalizzato con gli spazi in trattini: la chiave di CatalogKey piega già accenti e
    // maiuscole senza toccare le API di globalizzazione, che qui non funzionerebbero.
    private static string Slug(string? name)
    {
        var normalizzato = CatalogKey.NormalizeName(name);
        var sb = new StringBuilder(normalizzato.Length);
        var trattinoPendente = false;

        foreach (var c in normalizzato)
        {
            if (char.IsLetterOrDigit(c))
            {
                if (trattinoPendente && sb.Length > 0) sb.Append('-');
                trattinoPendente = false;
                sb.Append(c);
            }
            else
            {
                trattinoPendente = true;
            }
        }
        return sb.ToString();
    }

    /// <summary>Assegna gli identificatori di una sezione, garantendone l'unicità.
    ///
    /// Tre regole, in quest'ordine:
    /// 1. una provenienza dal PACCHETTO DELL'APP non si conserva — degraderebbe una campagna terza a
    ///    contenitore di voci che nessuno può modificare né rimuovere (§6, §8);
    /// 2. ogni altra provenienza si conserva: è ciò che permette a un reimport di aggiornare invece
    ///    di duplicare;
    /// 3. gli slug derivati dal nome che collidono ricevono un suffisso progressivo. Non è un caso
    ///    di scuola: nessuna tabella impedisce due righe omonime, e il parser rifiuta l'INTERO
    ///    pacchetto se un identificatore compare due volte.
    /// </summary>
    private static List<string> AssignIds<TRow>(
        string packageId, IReadOnlyList<TRow> rows, Func<TRow, string?> sourceIdOf, Func<TRow, string> nameOf)
    {
        static bool Conservabile(string? sourceId)
            => !string.IsNullOrWhiteSpace(sourceId) && !CatalogKey.IsFromAppPackage(sourceId);

        // DUE passaggi, non uno. Le provenienze conservate vanno prenotate TUTTE prima di generare
        // il primo slug: una riga senza provenienza processata per prima si prenderebbe
        // "<pacchetto>/dardo" e una riga successiva con quel source_id lo riproporrebbe identico —
        // due id uguali, e il parser rifiuta l'intero pacchetto, non la voce.
        var usati = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            var sourceId = sourceIdOf(row);
            if (Conservabile(sourceId)) usati.Add(sourceId!.Trim());
        }

        var risultato = new List<string>(rows.Count);
        foreach (var row in rows)
        {
            var sourceId = sourceIdOf(row);
            if (Conservabile(sourceId))
            {
                risultato.Add(sourceId!.Trim());
                continue;
            }

            // Slug vuoto (nome fatto di soli segni di punteggiatura) → "voce", che il suffisso
            // rende comunque unica: meglio un id brutto di un pacchetto irrecuperabile.
            var baseSlug = Slug(nameOf(row));
            if (baseSlug.Length == 0) baseSlug = "voce";

            var candidato = $"{packageId}/{baseSlug}";
            var n = 2;
            while (!usati.Add(candidato))
                candidato = $"{packageId}/{baseSlug}-{n++}";

            risultato.Add(candidato);
        }
        return risultato;
    }

    public static CatalogPackage Build(CampaignCatalogs catalogs, string campaignName)
    {
        var id = PackageIdFor(campaignName);

        // Le righe senza nome non sono esportabili: il parser esige nome E identificatore, e uno
        // slug vuoto non produce né l'uno né l'altro. Si scartano qui, non si aggirano nel test.
        var razze = catalogs.Races.Where(r => !string.IsNullOrWhiteSpace(r.Name)).ToList();
        var classi = catalogs.Classes.Where(c => !string.IsNullOrWhiteSpace(c.Name)).ToList();
        var background = catalogs.Backgrounds.Where(b => !string.IsNullOrWhiteSpace(b.Name)).ToList();
        var incantesimi = catalogs.Spells.Where(s => !string.IsNullOrWhiteSpace(s.Name)).ToList();
        var mostri = catalogs.Monsters.Where(m => !string.IsNullOrWhiteSpace(m.Name)).ToList();

        var idRazze = AssignIds(id, razze, r => r.SourceId, r => r.Name);
        var idClassi = AssignIds(id, classi, c => c.SourceId, c => c.Name);
        var idBackground = AssignIds(id, background, b => b.SourceId, b => b.Name);
        var idIncantesimi = AssignIds(id, incantesimi, s => s.SourceId, s => s.Name);
        var idMostri = AssignIds(id, mostri, m => m.SourceId, m => m.Name);

        return new CatalogPackage
        {
            SchemaVersion = CatalogPackageParser.SupportedSchemaVersion,
            Id = id,
            Name = string.IsNullOrWhiteSpace(campaignName) ? "Campagna" : campaignName.Trim(),
            Edition = "2024",
            Language = "it",
            Version = "1.0.0",

            Species = razze.Select((r, i) => new PackageSpecies
            {
                Id = idRazze[i],
                Name = r.Name,
                Description = r.Description,
                Speed = new PackageSpeed { Value = r.Speed, Unit = r.SpeedUnit },
                Traits = r.Traits,
            }).ToList(),

            Backgrounds = background.Select((b, i) => new PackageBackground
            {
                Id = idBackground[i],
                Name = b.Name,
                Description = b.Description,
                AbilityScores = SplitList(b.AbilityScores),
                OriginFeat = b.OriginFeat,
                SkillProficiencies = SplitList(b.SkillProficiencies),
                ToolProficiency = b.ToolProficiency,
                Equipment = b.Equipment,
            }).ToList(),

            // SkillChoices: PackageRowMerge.LeggiScelte riconosce il testo che DescriviScelte genera
            // ("2 fra: Arcano, Storia") e lo ricostruisce in struttura — copre il percorso primario,
            // le classi nate da un import. Per il testo libero digitato a mano dopo l'import
            // (Pages/Classes.razor) restituisce null: quel campo non ha un'inversione affidabile e
            // viene omesso, senza rompere l'export del resto della classe.
            Classes = classi.Select((c, i) => new PackageClass
            {
                Id = idClassi[i],
                Name = c.Name,
                HitDie = c.HitDie,
                PrimaryAbility = c.PrimaryAbility,
                SavingThrows = SplitList(c.SavingThrows),
                SkillChoices = PackageRowMerge.LeggiScelte(c.SkillChoices),
                // `features` ha un'inversione da quando l'import ci scrive la tabella dei livelli
                // (2026-07-31): senza questa riga una campagna esportata e reimportata altrove
                // perderebbe la progressione, e le schede tornerebbero senza privilegi. Il testo
                // che tabella non è produce una lista vuota, cioè il comportamento di prima.
                Levels = ClassProgression.Leggi(c.Features)
                    .Select(r => new PackageClassLevel
                    {
                        Level = r.Livello,
                        Features = r.Privilegi.ToList(),
                        // Nove esatti: il formato dichiara «nove slot, dal livello 1 al 9»
                        // (PackageClassLevel), mentre nel testo gli zeri finali sono omessi.
                        SpellSlots = Enumerable.Range(0, 9)
                            .Select(i => i < r.Slot.Count ? r.Slot[i] : 0)
                            .ToList(),
                    })
                    .ToList(),
            }).ToList(),

            Spells = incantesimi.Select((s, i) => new PackageSpell
            {
                Id = idIncantesimi[i],
                Name = s.Name,
                Level = s.Level,
                School = s.School,
                CastingTime = s.CastingTime,
                Range = s.Range,
                Components = s.Components,
                Duration = s.Duration,
                Description = s.Description,
                Classes = SplitList(s.Classes),
            }).ToList(),

            Monsters = mostri.Select((m, i) => new PackageMonster
            {
                Id = idMostri[i],
                Name = m.Name,
                ChallengeRating = m.ChallengeRating,
                ArmorClass = m.ArmorClass,
                HitPoints = m.HitPoints,
                Description = m.Description,
            }).ToList(),

            // Mai talenti: non hanno tabella, quindi nel database non ce ne sono (§5).
            Feats = new List<PackageFeat>(),
        };
    }

    // Le colonne sono testo libero, le sezioni del formato sono liste: si spezza sugli stessi
    // separatori che i form accettano, scartando i vuoti.
    private static List<string> SplitList(string? field)
        => string.IsNullOrWhiteSpace(field)
            ? new List<string>()
            : field.Split(',', ';', '/')
                   .Select(t => t.Trim())
                   .Where(t => t.Length > 0)
                   .ToList();

    /// <summary>Il file da scaricare. Serializzazione col source generator: il progetto pubblica
    /// con TrimMode=full, dove gli overload a reflection producono warning.</summary>
    public static string ToJson(CatalogPackage package)
        => JsonSerializer.Serialize(package, CatalogPackageJsonContext.Default.CatalogPackage);
}
