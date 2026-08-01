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

    /// <summary>Le sottoclassi di <b>questa riga</b>: la sua colonna se le dichiara, altrimenti il
    /// manuale — ma solo se è la riga a venire dal manuale.
    ///
    /// Deliberatamente <b>non</b> <see cref="SubclassCatalog.Disponibili"/>, che le schermate usano a
    /// ragione: quella risolve per <i>nome</i> su tutte le righe omonime, e qui si emette una voce per
    /// <i>riga</i>. Con due «Barbaro» — una importata dal manuale e una del tavolo, che «Duplica e
    /// modifica» crea per costruzione — il risolutore per nome dava a entrambe l'elenco della
    /// rappresentativa: la riga del tavolo esportava le sottoclassi SRD dell'altra (contenuto che non
    /// è suo), e una riga a cui l'utente le aveva <b>tolte</b> se le ritrovava nel file.</summary>
    private static IReadOnlyList<PackageSubclass> SottoclassiDellaRiga(CharacterClass c, CatalogPackage? manuale)
        => SubclassText.SembraElenco(c.Subclasses)
            ? SubclassText.Leggi(c.Subclasses)
            : CatalogKey.IsFromAppPackage(c.SourceId)
                // Riga importata prima che l'import scrivesse la colonna: è solo vecchia, e il
                // manuale ne è la versione aggiornata.
                ? SubclassCatalog.PerClasse(manuale?.Classes, c.Name)
                : Array.Empty<PackageSubclass>();

    /// <summary>Le sottoclassi con identificatori esportabili. Esiste per la stessa ragione di
    /// <see cref="AssignIds"/>, e applica le stesse regole riusandolo. Due motivi, entrambi
    /// necessari:
    ///
    /// <list type="bullet">
    /// <item>il parser esige un id su <b>ogni</b> voce, sottoclassi comprese, e una sottoclasse creata
    /// nella pagina Classi non ne ha — nessuno lo digita. Il file usciva con <c>"id": ""</c> e al
    /// reimport veniva respinto per intero;</item>
    /// <item>un id del manuale non va conservato: è la regola 1 di <see cref="AssignIds"/> —
    /// rivendicherebbe una provenienza SRD in un file che è di un altro tavolo.</item>
    /// </list>
    ///
    /// Il difetto non si vedeva in nessun test perché le prove sull'export non reimportano, e quella
    /// sul giro completo non aveva ancora una classe con sottoclassi.
    ///
    /// Le voci si ricostruiscono, non si modificano: <see cref="SottoclassiDellaRiga"/> ripiega su
    /// <see cref="SubclassCatalog.PerClasse"/>, che restituisce gli oggetti del manuale caricato in
    /// memoria, e riscriverne l'id lo corromperebbe per tutta la sessione.</summary>
    private static List<PackageSubclass> SottoclassiEsportabili(
        string packageId, IReadOnlyList<PackageSubclass> sottoclassi)
    {
        var ids = AssignIds(packageId, sottoclassi, s => s.Id, s => s.Name);

        return sottoclassi
            .Select((s, i) => new PackageSubclass
            {
                Id = ids[i],
                Name = s.Name,
                Description = s.Description,
                Levels = ConNoveSlot(s.Levels),
            })
            .ToList();
    }

    /// <summary>I talenti del manuale con identificatori esportabili. Stessa ragione e stesse regole
    /// di <see cref="SottoclassiEsportabili"/>: gli id del manuale sono riservati, e copiarli verbatim
    /// produceva un file che il parser rifiuta per intero.</summary>
    private static List<PackageFeat> TalentiEsportabili(string packageId, IReadOnlyList<PackageFeat>? talenti)
    {
        var voci = (talenti ?? Array.Empty<PackageFeat>()).Where(f => f is not null).ToList();
        var ids = AssignIds(packageId, voci, f => f.Id, f => f.Name);

        return voci
            .Select((f, i) => new PackageFeat
            {
                Id = ids[i],
                Name = f.Name,
                Category = f.Category,
                Description = f.Description,
            })
            .ToList();
    }

    /// <summary>I livelli con i nove slot che il formato dichiara. Unica sede della regola: nel testo
    /// gli zeri finali sono omessi (<see cref="ClassProgression"/>), nel formato di scambio no
    /// (<see cref="PackageClassLevel"/>). Scritta due volte era già divergente — la tabella di classe
    /// ne emetteva nove e le sottoclassi la lista tagliata, cioè una seconda variante del formato nata
    /// per distrazione. Ricostruisce anche le liste, così l'export non condivide mai un riferimento
    /// con il manuale in memoria.</summary>
    private static List<PackageClassLevel> ConNoveSlot(IEnumerable<PackageClassLevel>? livelli)
        => (livelli ?? Enumerable.Empty<PackageClassLevel>())
            .Where(l => l is not null)
            .Select(l => new PackageClassLevel
            {
                Level = l.Level,
                Features = (l.Features ?? new List<string>()).ToList(),
                SpellSlots = Enumerable.Range(0, 9)
                    .Select(i => i < (l.SpellSlots?.Count ?? 0) ? l.SpellSlots![i] : 0)
                    .ToList(),
            })
            .ToList();

    /// <summary>Unisce ai cataloghi della campagna le voci del manuale che nessuna riga già copre.
    ///
    /// Serve perché il manuale non è nel database: le sue voci si vedono nei cataloghi solo perché
    /// la UI le sovrappone a quelle di campagna, quindi un export "della campagna" di chi non ha
    /// importato nulla è un file quasi vuoto — senza i 331 mostri, e senza un solo esempio da cui
    /// capire come si scrive una voce. È il motivo per cui esiste anche l'export completo.
    ///
    /// Il confronto è per nome normalizzato, come nel resto dei cataloghi: se il tavolo ha già il
    /// suo «Goblin», vince quello e la voce di manuale non si aggiunge.</summary>
    public static CampaignCatalogs ConIlManuale(CampaignCatalogs catalogs, CatalogPackage? manuale)
    {
        if (manuale is null) return catalogs;

        static HashSet<string> Nomi<T>(IEnumerable<T> righe, Func<T, string> nomeDi)
            => righe.Select(r => CatalogKey.NormalizeName(nomeDi(r))).ToHashSet(StringComparer.Ordinal);

        static List<TRiga> Unisci<TRiga, TVoce>(
            List<TRiga> esistenti, IEnumerable<TVoce> voci,
            Func<TRiga, string> nomeRiga, Func<TVoce, string> nomeVoce, Func<TVoce, TRiga> converti)
        {
            var presenti = Nomi(esistenti, nomeRiga);
            var aggiunte = voci
                .Where(v => !string.IsNullOrWhiteSpace(nomeVoce(v)))
                .Where(v => presenti.Add(CatalogKey.NormalizeName(nomeVoce(v))))
                .Select(converti);
            return esistenti.Concat(aggiunte).ToList();
        }

        // campaignId e userId restano vuoti: le righe prodotte qui non vengono mai scritte nel
        // database, servono solo a passare per la stessa conversione dell'export normale.
        return new CampaignCatalogs
        {
            Races = Unisci(catalogs.Races, manuale.Species, r => r.Name, v => v.Name,
                v => PackageRowMerge.NuovaSpecie(v, string.Empty, null)),
            Classes = Unisci(catalogs.Classes, manuale.Classes, c => c.Name, v => v.Name,
                v => PackageRowMerge.NuovaClasse(v, string.Empty, null)),
            Backgrounds = Unisci(catalogs.Backgrounds, manuale.Backgrounds, b => b.Name, v => v.Name,
                v => PackageRowMerge.NuovoBackground(v, string.Empty, null)),
            Spells = Unisci(catalogs.Spells, manuale.Spells, s => s.Name, v => v.Name,
                v => PackageRowMerge.NuovoIncantesimo(v, string.Empty, null)),
            Monsters = Unisci(catalogs.Monsters, manuale.Monsters, m => m.Name, v => v.Name,
                v => PackageRowMerge.NuovoMostro(v, string.Empty, null)),
        };
    }

    /// <summary>Vero se fra le righe della campagna ce n'è almeno una arrivata dal manuale
    /// dell'app. Non è il caso raro che sembra: <see cref="SpellMaterialization"/> crea una riga
    /// con quella provenienza — descrizione SRD inclusa — ogni volta che un giocatore aggiunge alla
    /// scheda un incantesimo che vive solo nel manuale.</summary>
    public static bool ContieneMaterialeDiManuale(CampaignCatalogs c)
        => c.Races.Any(r => CatalogKey.IsFromAppPackage(r.SourceId))
           || c.Classes.Any(x => CatalogKey.IsFromAppPackage(x.SourceId))
           // Anche dentro la colonna `subclasses`, e non è una rifinitura: «Duplica e modifica» da
           // una voce di pacchetto copia l'elenco delle sottoclassi e **azzera** la provenienza
           // della riga (Pages/Classes.razor), quindi la prosa SRD di una sottoclasse — che è
           // descrizione intera, non un elenco di nomi — viaggia in una riga con `source_id` nullo.
           // Guardando i soli `source_id`, l'export della sola campagna produceva un file con quel
           // testo e `License = null`. L'`id:` dentro la colonna è l'ultimo segnale che resta: è
           // proprio per questo che il formato testuale lo conserva.
           || c.Classes.Any(x => SubclassText.Leggi(x.Subclasses)
                                     .Any(s => CatalogKey.IsFromAppPackage(s.Id)))
           || c.Backgrounds.Any(b => CatalogKey.IsFromAppPackage(b.SourceId))
           || c.Spells.Any(s => CatalogKey.IsFromAppPackage(s.SourceId))
           || c.Monsters.Any(m => CatalogKey.IsFromAppPackage(m.SourceId));

    /// <param name="manuale">Il manuale dell'app, se disponibile. Serve a due cose distinte: se
    /// <paramref name="unisciIlManuale"/> è vero le sue voci entrano nell'export, e in ogni caso la
    /// sua licenza è quella da riportare quando il file contiene materiale SRD.</param>
    /// <param name="unisciIlManuale">Falso per l'export della sola campagna.</param>
    public static CatalogPackage Build(
        CampaignCatalogs catalogs, string campaignName,
        CatalogPackage? manuale = null, bool unisciIlManuale = true)
    {
        // L'attribuzione si decide sui cataloghi ORIGINALI: dopo l'unione ogni voce di manuale
        // sarebbe materiale SRD, e la domanda «ne contiene?» risponderebbe sempre sì.
        var attribuzioneDovuta = (unisciIlManuale && manuale is not null)
                                 || ContieneMaterialeDiManuale(catalogs);
        var licenzaDaRiportare = manuale?.License;

        if (unisciIlManuale) catalogs = ConIlManuale(catalogs, manuale);
        else manuale = null;   // le voci non entrano; la licenza è già stata messa da parte

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

            // L'attribuzione viaggia col materiale: senza, la copia esportata non sarebbe più
            // conforme alla licenza con cui lo SRD è ridistribuibile.
            //
            // Non basta guardare se il manuale è incluso. Anche l'export della sola campagna può
            // contenere materiale SRD: SpellMaterialization scrive righe di database con
            // `source_id` del pacchetto ogni volta che un giocatore aggiunge alla scheda un
            // incantesimo che vive solo nel manuale — descrizione compresa. Quelle righe finiscono
            // nell'export, e AssignIds ne cancella pure la provenienza: senza questo controllo,
            // un file con testo SRD uscirebbe senza attribuzione e senza traccia dell'origine.
            License = attribuzioneDovuta ? licenzaDaRiportare : null,

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
                Subclasses = SottoclassiEsportabili(id, SottoclassiDellaRiga(c, manuale)),
                // `features` ha un'inversione da quando l'import ci scrive la tabella dei livelli
                // (2026-07-31): senza questa riga una campagna esportata e reimportata altrove
                // perderebbe la progressione, e le schede tornerebbero senza privilegi. Il testo
                // che tabella non è produce una lista vuota, cioè il comportamento di prima.
                // I nove slot che il formato dichiara li mette ConNoveSlot, sede unica della regola:
                // vale identica per le sottoclassi qui sopra.
                Levels = ConNoveSlot(ClassProgression.Leggi(c.Features)
                    .Select(r => new PackageClassLevel
                    {
                        Level = r.Livello,
                        Features = r.Privilegi.ToList(),
                        SpellSlots = r.Slot.ToList(),
                    })),
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

            // Dal database non arriva alcun talento: non hanno tabella (§5). Con il manuale incluso
            // però ci sono, e vanno riportati: chi esporta «tutto» se li aspetta, e sono la sola
            // sezione del formato che altrimenti non avrebbe mai un esempio da cui copiare.
            //
            // Passano da AssignIds come ogni altra sezione, e non è una rifinitura: i loro id sono
            // «srd-2024-it/talento/…», che il parser rifiuta in un file che manuale non è (§6). Copiarli
            // verbatim rendeva l'export «tutto, manuale incluso» — proprio quello che la guida indica
            // come modello da cui partire — irricevibile al reimport, con 17 errori e nessuna voce
            // importata. Le voci si ricostruiscono invece di essere modificate: sono gli oggetti del
            // manuale caricato in memoria, e riscriverne l'id lo corromperebbe per tutta la sessione.
            Feats = TalentiEsportabili(id, manuale?.Feats),
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
