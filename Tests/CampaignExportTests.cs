using DndCompanion.Models;
using DndCompanion.Models.Packages;
using DndCompanion.Services;

namespace DndCompanion.Tests;

public class CampaignExportTests
{
    private static CampaignCatalogs Cataloghi() => new()
    {
        Races = { new Race { Id = "uuid-1", Name = "Elfo Silvano", Speed = 9, SpeedUnit = "m", CampaignId = "c1" } },
        Classes = {
            new CharacterClass { Id = "uuid-4", Name = "Mago", HitDie = "d6", PrimaryAbility = "Intelligenza",
                                  SavingThrows = "Intelligenza, Saggezza",
                                  SkillChoices = "2 fra: Arcano, Storia", CampaignId = "c1" },
        },
        Spells = {
            new Spell { Id = "uuid-2", Name = "Palla di Fuoco", Level = 3,
                        Classes = "Mago, Stregone", CampaignId = "c1" },
            new Spell { Id = "uuid-3", Name = "Invisibilità", Level = 2,
                        SourceId = "srd-2024-it/invisibilita", CampaignId = "c1" },
        },
    };

    // §6: dare al proprio file l'id del pacchetto dell'app renderebbe le proprie voci di sola
    // lettura al reimport. L'export non deve mai produrlo.
    [Fact]
    public void PackageIdFor_NonProduceMaiLIdDelPacchettoDellApp()
    {
        var id = CampaignExport.PackageIdFor("SRD 2024 IT");

        Assert.NotEqual(CatalogPackageParser.AppPackageId, id);
        Assert.False(CatalogKey.IsFromAppPackage(id + "/qualcosa"));
    }

    [Fact]
    public void PackageIdFor_NormalizzaNomeEAccenti()
        => Assert.Equal("campagna-la-citta-perduta", CampaignExport.PackageIdFor("La Città Perduta"));

    [Fact]
    public void PackageIdFor_NomeVuoto_ProduceUnIdUsabile()
        => Assert.Equal("campagna-senza-nome", CampaignExport.PackageIdFor("   "));

    [Fact]
    public void Build_RigaSenzaProvenienza_RiceveUnIdDerivatoDalNome()
    {
        var pacchetto = CampaignExport.Build(Cataloghi(), "La Città Perduta");

        var elfo = Assert.Single(pacchetto.Species);
        Assert.Equal("campagna-la-citta-perduta/elfo-silvano", elfo.Id);
        Assert.Equal("Elfo Silvano", elfo.Name);
        Assert.Equal(9, elfo.Speed!.Value);
        Assert.Equal("m", elfo.Speed.Unit);
    }

    // Conservare il source_id è ciò che permette a un reimport di AGGIORNARE la voce invece di
    // duplicarla: senza, ogni giro di export/import creerebbe una riga in più.
    [Fact]
    public void Build_RigaConProvenienzaDaUnFileUtente_ConservaLIdOriginale()
    {
        var cataloghi = new CampaignCatalogs
        {
            Spells = { new Spell { Id = "u1", Name = "Dardo", SourceId = "altro-tavolo/dardo", CampaignId = "c1" } },
        };

        var pacchetto = CampaignExport.Build(cataloghi, "La Città Perduta");

        Assert.Contains(pacchetto.Spells, s => s.Id == "altro-tavolo/dardo");
    }

    // Ma NON la provenienza del pacchetto dell'app: propagarla renderebbe quelle voci intoccabili
    // in una campagna terza che non ha mai importato nulla di ufficiale (§6, §8).
    [Fact]
    public void Build_RigaMaterializzataDalManuale_PerdeLaProvenienzaDellApp()
    {
        var pacchetto = CampaignExport.Build(Cataloghi(), "La Città Perduta");

        Assert.DoesNotContain(pacchetto.Spells, s => CatalogKey.IsFromAppPackage(s.Id));
        Assert.Contains(pacchetto.Spells, s => s.Id == "campagna-la-citta-perduta/invisibilita");
    }

    // Nessuna tabella impedisce due righe omonime, e il parser rifiuta l'INTERO pacchetto se un
    // identificatore compare due volte: senza suffisso il file esportato sarebbe illeggibile.
    [Fact]
    public void Build_RigheOmonime_RicevonoIdentificatoriDistinti()
    {
        var cataloghi = new CampaignCatalogs
        {
            Spells =
            {
                new Spell { Id = "u1", Name = "Palla di Fuoco", CampaignId = "c1" },
                new Spell { Id = "u2", Name = "palla di fuoco", CampaignId = "c1" },
                new Spell { Id = "u3", Name = "PALLA DI FUOCO", CampaignId = "c1" },
            },
        };

        var ids = CampaignExport.Build(cataloghi, "Tavolo").Spells.Select(s => s.Id).ToList();

        Assert.Equal(3, ids.Distinct(StringComparer.Ordinal).Count());
    }

    // Il caso che un solo passaggio non copre: una riga SENZA provenienza processata per prima si
    // prenderebbe lo slug che una riga successiva porta già come source_id. Succede davvero — basta
    // un "duplica e modifica" di una voce importata da un file omonimo alla campagna.
    [Fact]
    public void Build_SlugGeneratoCheCollideConUnaProvenienzaConservata_RestaUnico()
    {
        var cataloghi = new CampaignCatalogs
        {
            Spells =
            {
                new Spell { Id = "u1", Name = "Dardo", SourceId = null, CampaignId = "c1" },
                new Spell { Id = "u2", Name = "Dardo", SourceId = "campagna-tavolo/dardo", CampaignId = "c1" },
            },
        };

        var ids = CampaignExport.Build(cataloghi, "Tavolo").Spells.Select(s => s.Id).ToList();

        Assert.Equal(2, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.Contains("campagna-tavolo/dardo", ids);
    }

    [Fact]
    public void Build_NomeSenzaLettereNeCifre_ProduceComunqueUnIdValido()
    {
        var cataloghi = new CampaignCatalogs
        {
            Monsters = { new Monster { Id = "u1", Name = "???", CampaignId = "c1" } },
        };

        var mostro = Assert.Single(CampaignExport.Build(cataloghi, "Tavolo").Monsters);
        Assert.False(mostro.Id.EndsWith("/", StringComparison.Ordinal));
    }

    // Una riga senza nome non è esportabile: il parser esige nome e identificatore, e uno slug
    // vuoto non produce né l'uno né l'altro.
    [Fact]
    public void Build_RigaSenzaNome_VieneScartata()
    {
        var cataloghi = new CampaignCatalogs
        {
            Races = { new Race { Id = "u1", Name = "   ", CampaignId = "c1" } },
        };

        Assert.Empty(CampaignExport.Build(cataloghi, "Tavolo").Species);
    }

    [Fact]
    public void Build_ClassiDellIncantesimo_TornanoUnaLista()
    {
        var pacchetto = CampaignExport.Build(Cataloghi(), "La Città Perduta");

        var palla = Assert.Single(pacchetto.Spells, s => s.Name == "Palla di Fuoco");
        Assert.Equal(new[] { "Mago", "Stregone" }, palla.Classes);
    }

    // La sezione Classi non era mai esercitata dagli altri test: HitDie e SavingThrows arrivano
    // come da mappatura diretta, e SkillChoices — testo nato da un import (DescriviScelte) — deve
    // tornare struttura via PackageRowMerge.LeggiScelte, non sparire.
    [Fact]
    public void Build_Classe_PortaHitDieSavingThrowsEScelteRicostruite()
    {
        var pacchetto = CampaignExport.Build(Cataloghi(), "La Città Perduta");

        var mago = Assert.Single(pacchetto.Classes);
        Assert.Equal("d6", mago.HitDie);
        Assert.Equal("Intelligenza", mago.PrimaryAbility);
        Assert.Equal(new[] { "Intelligenza", "Saggezza" }, mago.SavingThrows);
        Assert.NotNull(mago.SkillChoices);
        Assert.Equal(2, mago.SkillChoices!.Count);
        Assert.Equal(new[] { "Arcano", "Storia" }, mago.SkillChoices.From);
    }

    // ---- Export completo, manuale incluso ----

    private static CatalogPackage Manuale() => new()
    {
        SchemaVersion = 1,
        Id = "srd-2024-it",
        Name = "Manuale",
        License = new PackageLicense { Name = "CC BY 4.0", Attribution = "Quest'opera include materiale…" },
        Monsters =
        {
            new PackageMonster { Id = "srd-2024-it/mostro/goblin", Name = "Goblin", HitPoints = "7 (2d6)" },
            new PackageMonster { Id = "srd-2024-it/mostro/orco", Name = "Orco", HitPoints = "15 (2d8 + 6)" },
        },
        Backgrounds = { new PackageBackground { Id = "srd-2024-it/background/accolito", Name = "Accolito" } },
        // Un talento con l'id del manuale: è la sezione su cui l'export copiava gli id verbatim, e
        // senza una voce qui il difetto restava invisibile a tutta la suite.
        Feats = { new PackageFeat { Id = "srd-2024-it/talento/attento", Name = "Attento" } },
    };

    /// <summary>Il difetto che questo chiude: chi non ha importato nulla esportava un file con le
    /// sezioni vuote — nessun mostro, e nessun esempio da cui capire come si scrive una voce.</summary>
    [Fact]
    public void Build_ConIlManuale_PortaAncheLeVociDiManuale()
    {
        var pacchetto = CampaignExport.Build(new CampaignCatalogs(), "La Città Perduta", Manuale());

        Assert.Equal(new[] { "Goblin", "Orco" }, pacchetto.Monsters.Select(m => m.Name).OrderBy(n => n));
        Assert.Single(pacchetto.Backgrounds);
    }

    /// <summary>Lo SRD è ridistribuibile **a condizione** che l'attribuzione lo accompagni: un
    /// export che porta il materiale senza la licenza non sarebbe conforme.</summary>
    [Fact]
    public void Build_ConIlManuale_RiportaLAttribuzione()
    {
        var pacchetto = CampaignExport.Build(new CampaignCatalogs(), "c", Manuale());

        Assert.NotNull(pacchetto.License);
        Assert.Equal("CC BY 4.0", pacchetto.License!.Name);
        Assert.False(string.IsNullOrWhiteSpace(pacchetto.License.Attribution));
    }

    [Fact]
    public void Build_SenzaManuale_NonDichiaraAlcunaLicenza()
        => Assert.Null(CampaignExport.Build(new CampaignCatalogs(), "c").License);

    /// <summary>L'attribuzione non dipende dal pulsante premuto ma dal contenuto del file. Anche
    /// l'export della sola campagna può portare materiale SRD: <c>SpellMaterialization</c> crea una
    /// riga di database con la provenienza del manuale — descrizione compresa — ogni volta che un
    /// giocatore aggiunge alla scheda un incantesimo che vive solo lì.</summary>
    [Fact]
    public void Build_SoloCampagna_ConRigheDiProvenienzaSrd_RiportaComunqueLAttribuzione()
    {
        var cataloghi = new CampaignCatalogs
        {
            Spells =
            {
                new Spell
                {
                    Id = "u1", Name = "Palla di fuoco", CampaignId = "c1",
                    SourceId = "srd-2024-it/incantesimo/palla-di-fuoco",
                },
            },
        };

        var pacchetto = CampaignExport.Build(cataloghi, "c", Manuale(), unisciIlManuale: false);

        Assert.NotNull(pacchetto.License);
        Assert.Equal("CC BY 4.0", pacchetto.License!.Name);
        // Le voci del manuale però NON entrano: il pulsante premuto era «solo campagna».
        Assert.Empty(pacchetto.Monsters);
    }

    [Fact]
    public void Build_SoloCampagna_SenzaMaterialeSrd_NonDichiaraLicenza()
    {
        var cataloghi = new CampaignCatalogs
        {
            Spells = { new Spell { Id = "u1", Name = "Dardo del tavolo", CampaignId = "c1" } },
        };

        Assert.Null(CampaignExport.Build(cataloghi, "c", Manuale(), unisciIlManuale: false).License);
    }

    /// <summary>Le sottoclassi non passano dalla riga di database: senza recuperarle dal manuale,
    /// l'export «tutto incluso» darebbe classi senza sottoclassi — cioè senza il dato che chi
    /// esporta per farsene un modello sta cercando.</summary>
    [Fact]
    public void Build_ConIlManuale_PortaAncheLeSottoclassi()
    {
        var manuale = Manuale();
        manuale.Classes.Add(new PackageClass
        {
            Id = "srd-2024-it/classe/barbaro",
            Name = "Barbaro",
            Subclasses =
            {
                new PackageSubclass
                {
                    Id = "srd-2024-it/sottoclasse/cammino-del-berserker",
                    Name = "Cammino del berserker",
                    Levels = { new PackageClassLevel { Level = 3, Features = { "Frenesia" } } },
                },
            },
        });

        var classe = Assert.Single(CampaignExport.Build(new CampaignCatalogs(), "c", manuale).Classes);

        var sottoclasse = Assert.Single(classe.Subclasses);
        Assert.Equal("Cammino del berserker", sottoclasse.Name);
        Assert.Equal(3, Assert.Single(sottoclasse.Levels).Level);
    }

    /// <summary>Il rovescio: su una classe **del tavolo** che porti per caso il nome di una classe
    /// del manuale, le sottoclassi SRD non si innestano. Sarebbe attribuire al tavolo un contenuto
    /// che non è suo — e la stessa domanda se la pongono già la scheda e il wizard, che per una
    /// classe di campagna non offrono né mostrano le sottoclassi del manuale.</summary>
    [Fact]
    public void Build_ConIlManuale_NonInnestaLeSottoclassiSuUnaClasseDelTavolo()
    {
        var manuale = Manuale();
        manuale.Classes.Add(new PackageClass
        {
            Id = "srd-2024-it/classe/barbaro",
            Name = "Barbaro",
            Subclasses =
            {
                new PackageSubclass
                {
                    Id = "srd-2024-it/sottoclasse/cammino-del-berserker",
                    Name = "Cammino del berserker",
                },
            },
        });

        var cataloghi = new CampaignCatalogs
        {
            // Stesso nome, provenienza nulla: è la classe di questo tavolo, e oscura quella di
            // manuale anche nella pagina Classi.
            Classes = { new CharacterClass { Id = "u1", Name = "Barbaro", CampaignId = "c1" } },
        };

        var classe = Assert.Single(CampaignExport.Build(cataloghi, "c", manuale).Classes);

        Assert.Equal("Barbaro", classe.Name);
        Assert.Empty(classe.Subclasses);
    }

    /// <summary>Il difetto che questo chiude: prima le sottoclassi uscivano solo sulle righe di
    /// provenienza SRD (<c>IsFromAppPackage</c>), quindi una classe **del tavolo** con le proprie
    /// sottoclassi — scritte a mano nella sua colonna <c>subclasses</c> — non le vedeva mai nel
    /// file esportato, pur avendole davvero.</summary>
    [Fact]
    public void Build_ClasseDelTavolo_PortaLeProprieSottoclassi()
    {
        var sottoclassiScritteATavolo = SubclassText.Serializza(new List<PackageSubclass>
        {
            new()
            {
                Name = "Ordine del sale",
                Levels = { new PackageClassLevel { Level = 3, Features = { "Benedizione salina" } } },
            },
        });
        var cataloghi = new CampaignCatalogs
        {
            Classes = { new CharacterClass
            {
                Id = "u1", Name = "Salinaro", Subclasses = sottoclassiScritteATavolo, CampaignId = "c1",
            } },
        };

        var classe = Assert.Single(CampaignExport.Build(cataloghi, "Tavolo").Classes);

        var sottoclasse = Assert.Single(classe.Subclasses);
        Assert.Equal("Ordine del sale", sottoclasse.Name);
        Assert.Equal(3, Assert.Single(sottoclasse.Levels).Level);
    }

    /// <summary>Il rovescio del test precedente: una riga importata dal manuale con la colonna
    /// <c>subclasses</c> ancora vuota — il caso comune di ogni classe importata prima che l'import
    /// scrivesse quella colonna — deve continuare a ricevere le sottoclassi dal manuale, esattamente
    /// come faceva il vecchio controllo su <c>IsFromAppPackage</c>.</summary>
    [Fact]
    public void Build_ClasseImportataDalManuale_ContinuaAPortareLeSottoclassiDalManuale()
    {
        var manuale = Manuale();
        manuale.Classes.Add(new PackageClass
        {
            Id = "srd-2024-it/classe/chierico",
            Name = "Chierico",
            Subclasses = { new PackageSubclass { Id = "srd-2024-it/sottoclasse/ordine-della-vita", Name = "Ordine della vita" } },
        });
        var cataloghi = new CampaignCatalogs
        {
            // Riga vecchia: importata dal manuale, ma con la colonna subclasses ancora vuota.
            Classes = { new CharacterClass
            {
                Id = "u1", Name = "Chierico", SourceId = "srd-2024-it/classe/chierico", CampaignId = "c1",
            } },
        };

        var classe = Assert.Single(CampaignExport.Build(cataloghi, "c", manuale).Classes);

        Assert.Equal("Ordine della vita", Assert.Single(classe.Subclasses).Name);
    }

    /// <summary>La licenza segue la provenienza delle sottoclassi, non la loro sola presenza nel
    /// file: una classe del tavolo con sottoclassi proprie non deve dichiarare la licenza SRD (non
    /// c'è nulla da attribuire), mentre una classe che le eredita ancora dal manuale sì — altrimenti
    /// un file uscirebbe con testo SRD senza attribuzione, o con un'attribuzione superflua.</summary>
    [Fact]
    public void Build_LicenzaSegueLaProvenienzaDelleSottoclassi()
    {
        var sottoclassiDelTavolo = SubclassText.Serializza(new List<PackageSubclass> { new() { Name = "Ordine del sale" } });
        var soloTavolo = new CampaignCatalogs
        {
            Classes = { new CharacterClass
            {
                Id = "u1", Name = "Salinaro", Subclasses = sottoclassiDelTavolo, CampaignId = "c1",
            } },
        };
        Assert.Null(CampaignExport.Build(soloTavolo, "c", Manuale(), unisciIlManuale: false).License);

        var manuale = Manuale();
        manuale.Classes.Add(new PackageClass
        {
            Id = "srd-2024-it/classe/chierico",
            Name = "Chierico",
            Subclasses = { new PackageSubclass { Id = "srd-2024-it/sottoclasse/ordine-della-vita", Name = "Ordine della vita" } },
        });
        var importata = new CampaignCatalogs
        {
            Classes = { new CharacterClass
            {
                Id = "u2", Name = "Chierico", SourceId = "srd-2024-it/classe/chierico", CampaignId = "c1",
            } },
        };
        Assert.NotNull(CampaignExport.Build(importata, "c", manuale).License);
    }

    /// <summary>La riga del tavolo vince: se il master ha già il suo «Goblin», la voce di manuale
    /// non si aggiunge — altrimenti il file conterrebbe due mostri con lo stesso nome, e al
    /// reimport il tavolo di destinazione si troverebbe un doppione.</summary>
    [Fact]
    public void Build_ConIlManuale_NonDuplicaCioCheLaCampagnaHaGia()
    {
        var cataloghi = new CampaignCatalogs
        {
            Monsters = { new Monster { Id = "u1", Name = "goblin", HitPoints = "9", CampaignId = "c1" } },
        };

        var pacchetto = CampaignExport.Build(cataloghi, "c", Manuale());

        var goblin = Assert.Single(pacchetto.Monsters,
            m => string.Equals(m.Name, "goblin", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("9", goblin.HitPoints);
    }

    /// <summary>Le voci incluse non conservano la provenienza del manuale: nel tavolo che le
    /// reimporta devono essere righe proprie, modificabili e rimovibili, non voci di sola
    /// lettura (§6).</summary>
    [Fact]
    public void Build_ConIlManuale_NonConservaLaProvenienzaDelPacchettoDellApp()
    {
        var pacchetto = CampaignExport.Build(new CampaignCatalogs(), "La Città Perduta", Manuale());

        Assert.All(pacchetto.Monsters, m =>
            Assert.StartsWith("campagna-la-citta-perduta/", m.Id, StringComparison.Ordinal));
    }

    // La tabella dei livelli, da quando l'import la scrive in `features`, ha un'inversione: senza
    // riesportarla una campagna esportata e reimportata altrove perderebbe la progressione, e le
    // schede tornerebbero senza privilegi. Gli slot tornano a nove, come vuole il formato.
    [Fact]
    public void Build_Classe_RiesportaLaTabellaDeiLivelli()
    {
        var cataloghi = new CampaignCatalogs
        {
            Classes =
            {
                new CharacterClass
                {
                    Id = "uuid-4", Name = "Chierico", CampaignId = "c1",
                    Features = "L1 — Lanciare incantesimi, Ordine divino · Slot 2\nL3 — Sottoclasse del Chierico · Slot 4/2",
                },
            },
        };

        var chierico = Assert.Single(CampaignExport.Build(cataloghi, "c").Classes);

        Assert.Equal(2, chierico.Levels.Count);
        Assert.Equal(new[] { "Lanciare incantesimi", "Ordine divino" }, chierico.Levels[0].Features);
        Assert.Equal(3, chierico.Levels[1].Level);
        Assert.Equal(new[] { 4, 2, 0, 0, 0, 0, 0, 0, 0 }, chierico.Levels[1].SpellSlots);
    }

    // Il testo scritto a mano non è una tabella: non va inventata una progressione dal nulla.
    [Fact]
    public void Build_ClasseConFeaturesScritteAMano_NonInventaLivelli()
    {
        var cataloghi = new CampaignCatalogs
        {
            Classes =
            {
                new CharacterClass
                {
                    Id = "uuid-4", Name = "Mago", CampaignId = "c1",
                    Features = "Recupero arcano, e altre note sparse.",
                },
            },
        };

        Assert.Empty(Assert.Single(CampaignExport.Build(cataloghi, "c").Classes).Levels);
    }

    // Il testo libero digitato a mano dopo l'import non ha un'inversione affidabile: il campo va
    // omesso, non sostituito da una struttura inventata, e il resto della classe resta esportato.
    [Fact]
    public void Build_ClasseConScelteTestoLibero_OmetteLeScelteMaEsportaIlResto()
    {
        var cataloghi = new CampaignCatalogs
        {
            Classes = { new CharacterClass
            {
                Id = "u1", Name = "Guerriero", HitDie = "d10",
                SkillChoices = "Due a scelta fra le abilità del manuale", CampaignId = "c1",
            } },
        };

        var guerriero = Assert.Single(CampaignExport.Build(cataloghi, "Tavolo").Classes);

        Assert.Equal("d10", guerriero.HitDie);
        Assert.Null(guerriero.SkillChoices);
    }

    // Il caso più comune: una classe su cui nessuno ha mai scritto le scelte di abilità.
    // CharacterClass.SkillChoices è "string" non nullable con default string.Empty (mai null) — è
    // questo il valore che Build riceve davvero dal database, non un CharacterClass.SkillChoices
    // esplicitamente null.
    [Fact]
    public void Build_ClasseSenzaScelte_SkillChoicesRestaNull()
    {
        var cataloghi = new CampaignCatalogs
        {
            Classes = { new CharacterClass { Id = "u1", Name = "Chierico", CampaignId = "c1" } },
        };

        var chierico = Assert.Single(CampaignExport.Build(cataloghi, "Tavolo").Classes);

        Assert.Equal(string.Empty, new CharacterClass().SkillChoices); // documenta il default reale
        Assert.Null(chierico.SkillChoices);
    }

    // §5: l'export non produce mai talenti, perché nel database non ce ne sono.
    [Fact]
    public void Build_NonProduceMaiTalenti()
        => Assert.Empty(CampaignExport.Build(Cataloghi(), "La Città Perduta").Feats);

    [Fact]
    public void Build_DichiaraLaVersioneDiSchemaSupportata()
        => Assert.Equal(CatalogPackageParser.SupportedSchemaVersion,
                        CampaignExport.Build(Cataloghi(), "c").SchemaVersion);

    // Il giro completo: ciò che l'export produce, il parser deve saperlo rileggere. Senza questo
    // test un export malformato si scoprirebbe solo al reimport, su un file già distribuito.
    // Include una classe con scelte ricostruite: la struttura annidata (PackageSkillChoices) deve
    // sopravvivere alla scrittura e alla rilettura tanto quanto i campi semplici.
    [Fact]
    public void ToJson_IlRisultatoERileggibileDalParser()
    {
        var json = CampaignExport.ToJson(CampaignExport.Build(Cataloghi(), "La Città Perduta"));

        var riletto = CatalogPackageParser.Parse(json);

        Assert.Empty(riletto.Errors);
        Assert.NotNull(riletto.Package);
        Assert.Single(riletto.Package!.Species);
        Assert.Equal(2, riletto.Package.Spells.Count);

        var mago = Assert.Single(riletto.Package.Classes);
        Assert.NotNull(mago.SkillChoices);
        Assert.Equal(2, mago.SkillChoices!.Count);
        Assert.Equal(new[] { "Arcano", "Storia" }, mago.SkillChoices.From);
    }

    // ---- Il criterio di fatto: esporta, reimporta con gli helper puri dell'import, esporta di
    // nuovo. I due JSON devono essere IDENTICI: è la sola misura che dice "questo giro non perde
    // più niente", perché un file scritto a mano può legittimamente arrivare in una forma diversa
    // (es. "4/2" testuale contro [4,2,0,0,0,0,0,0,0] canonico) senza che sia una perdita.
    //
    // La classe porta anche le proprie sottoclassi: da quando l'import le scrive nella colonna
    // (PackageRowMerge.NuovaClasse) il giro le attraversa per intero, e non provarle qui lascerebbe
    // non verificato proprio il dato nuovo.
    private static CampaignCatalogs CatalogoPerIlRoundTrip() => new()
    {
        Races = { new Race
        {
            Id = "u1", Name = "Elfo delle paludi", Description = "Vive nelle paludi del delta.",
            Speed = 9, SpeedUnit = "m", Traits = "Vista nel buio, Resistenza fatata", CampaignId = "c1",
        } },
        Classes = { new CharacterClass
        {
            Id = "u2", Name = "Salinaro", HitDie = "d8", PrimaryAbility = "Saggezza",
            SavingThrows = "Saggezza, Carisma",
            SkillChoices = "2 fra: Intuizione, Religione",
            Features = ClassProgression.Serializza(new List<PackageClassLevel>
            {
                new() { Level = 1, Features = { "Benedizione salina" }, SpellSlots = { 2, 0, 0, 0, 0, 0, 0, 0, 0 } },
                new() { Level = 3, Features = { "Scelta dell'ordine" }, SpellSlots = { 4, 2, 0, 0, 0, 0, 0, 0, 0 } },
            }),
            // Due sottoclassi del tavolo, una con id e una senza: la seconda è il caso normale di
            // chi la crea nella pagina Classi, dove nessuno digita un identificatore. Dal primo
            // export in poi l'id assegnato si conserva, ed è ciò che rende il giro idempotente.
            Subclasses = SubclassText.Serializza(new List<PackageSubclass>
            {
                new()
                {
                    Id = "campagna-tavolo-di-prova/ordine-del-sale",
                    Name = "Ordine del sale",
                    Description = "Chi giura sul sale non rompe la parola data.",
                    Levels = { new PackageClassLevel { Level = 3, Features = { "Parola salata" } } },
                },
                new()
                {
                    Name = "Ordine della salamoia",
                    Levels = { new PackageClassLevel { Level = 3, Features = { "Conserva" } } },
                },
            }),
            CampaignId = "c1",
        } },
        Backgrounds = { new Background
        {
            Id = "u3", Name = "Cresciuto fra le saline", Description = "Un'infanzia passata a raccogliere sale.",
            AbilityScores = "Saggezza, Costituzione, Carisma", OriginFeat = "Iniziato alla magia",
            SkillProficiencies = "Intuizione, Sopravvivenza", ToolProficiency = "Strumenti del bottaio",
            Equipment = "Un sacco di sale, 10 mo", CampaignId = "c1",
        } },
        Spells = { new Spell
        {
            Id = "u4", Name = "Colpo di sale", Level = 1, School = "Trasmutazione",
            CastingTime = "1 azione", Range = "9 m", Components = "V, S", Duration = "Istantanea",
            Description = "Un cristallo di sale colpisce il bersaglio.", Classes = "Salinaro, Chierico",
            CampaignId = "c1",
        } },
        Monsters = { new Monster
        {
            Id = "u5", Name = "Granchio corazzato", ChallengeRating = "1/2", ArmorClass = 15,
            HitPoints = "22 (4d8 + 4)", Description = "Un granchio dal carapace durissimo.", CampaignId = "c1",
        } },
    };

    /// <summary>Il difetto che chiude: senza questo test una perdita nel giro export→import→export
    /// si scoprirebbe solo su una campagna vera, dopo che qualcuno ci ha già affidato i propri dati.
    /// Copre specie, classe (con scelte di abilità, tabella dei livelli/slot e sottoclassi),
    /// background, incantesimo e mostro — le sezioni che gli helper puri dell'import sanno
    /// ricostruire per intero. Non copre i talenti, che l'app legge di sola consultazione dal manuale
    /// e non ha una tabella dove scriverli.</summary>
    [Fact]
    public void Build_CicloEsportaReimportaEsporta_ProduceLoStessoJson()
    {
        const string nomeCampagna = "Tavolo di prova";

        var primoJson = CampaignExport.ToJson(CampaignExport.Build(CatalogoPerIlRoundTrip(), nomeCampagna));

        var riletto = CatalogPackageParser.Parse(primoJson);
        Assert.Empty(riletto.Errors);
        var pacchetto = riletto.Package!;

        // Stessa via che percorre l'import vero: dal pacchetto riletto alle righe di catalogo,
        // senza toccare il database.
        var reimportati = new CampaignCatalogs
        {
            Races = pacchetto.Species.Select(p => PackageRowMerge.NuovaSpecie(p, "c1", null)).ToList(),
            Classes = pacchetto.Classes.Select(p => PackageRowMerge.NuovaClasse(p, "c1", null)).ToList(),
            Backgrounds = pacchetto.Backgrounds.Select(p => PackageRowMerge.NuovoBackground(p, "c1", null)).ToList(),
            Spells = pacchetto.Spells.Select(p => PackageRowMerge.NuovoIncantesimo(p, "c1", null)).ToList(),
            Monsters = pacchetto.Monsters.Select(p => PackageRowMerge.NuovoMostro(p, "c1", null)).ToList(),
        };

        var secondoJson = CampaignExport.ToJson(CampaignExport.Build(reimportati, nomeCampagna));

        Assert.Equal(primoJson, secondoJson);
    }

    /// <summary>Il difetto che chiude, trovato al confine fra l'export e il parser: il parser esige un
    /// identificatore su <b>ogni</b> voce, sottoclassi comprese, e una sottoclasse creata nella pagina
    /// Classi non ne ha — nessuno lo digita. Il file usciva con <c>"id": ""</c> e al reimport veniva
    /// respinto per intero, senza che nulla lo segnalasse: le prove sull'export non reimportano.
    /// Verifica anche la regola 1 di <c>AssignIds</c>: un id del manuale non si conserva, perché
    /// rivendicherebbe una provenienza SRD in un file di un altro tavolo.</summary>
    [Fact]
    public void Build_LeSottoclassiRicevonoIdCheIlParserAccetta()
    {
        var manuale = Manuale();
        manuale.Classes.Add(new PackageClass
        {
            Id = "srd-2024-it/classe/guerriero",
            Name = "Guerriero",
            Subclasses =
            {
                new PackageSubclass { Id = "srd-2024-it/sottoclasse/campione", Name = "Campione" },
            },
        });
        var cataloghi = new CampaignCatalogs
        {
            // Una sottoclasse creata a mano: nessun id, come la crea la pagina Classi.
            Classes = { new CharacterClass
            {
                Id = "u1", Name = "Salinaro", CampaignId = "c1",
                Subclasses = SubclassText.Serializza(
                    new List<PackageSubclass> { new() { Name = "Ordine del sale" } }),
            } },
        };

        var json = CampaignExport.ToJson(CampaignExport.Build(cataloghi, "Tavolo di prova", manuale));

        var riletto = CatalogPackageParser.Parse(json);
        Assert.Empty(riletto.Errors);

        var sottoclassi = riletto.Package!.Classes.SelectMany(c => c.Subclasses).ToList();
        Assert.Equal(2, sottoclassi.Count);
        Assert.All(sottoclassi, s =>
        {
            Assert.False(string.IsNullOrWhiteSpace(s.Id));
            // Regola 1 di AssignIds: la provenienza del manuale non si conserva. Non è il parser a
            // pretenderlo — gli id di sottoclasse sono esenti dal divieto sul prefisso — ma questo
            // file è di un altro tavolo, e quell'id rivendicherebbe una provenienza SRD.
            Assert.False(CatalogKey.IsFromAppPackage(s.Id));
        });
    }

    /// <summary>Il difetto BLOCCANTE che questo chiude: i talenti erano l'unica sezione che l'export
    /// copiava dal manuale <b>senza</b> passare da <c>AssignIds</c>, quindi il file «tutto, manuale
    /// incluso» — proprio quello che la guida indica come modello da cui partire — portava 17 id
    /// <c>srd-2024-it/talento/…</c>, che il parser rifiuta. Esito: esporto, reimporto, e l'import
    /// muore per intero incolpando il mio file.</summary>
    [Fact]
    public void Build_ITalentiDelManualeRicevonoIdCheIlParserAccetta()
    {
        var json = CampaignExport.ToJson(
            CampaignExport.Build(new CampaignCatalogs(), "Tavolo di prova", Manuale()));

        var riletto = CatalogPackageParser.Parse(json);
        Assert.Empty(riletto.Errors);

        var talento = Assert.Single(riletto.Package!.Feats);
        Assert.Equal("Attento", talento.Name);
        Assert.False(CatalogKey.IsFromAppPackage(talento.Id));
    }

    /// <summary>Come per le sottoclassi: gli oggetti del manuale caricato in memoria non vanno
    /// modificati, o la sessione continuerebbe con un id che il manuale non ha mai avuto.</summary>
    [Fact]
    public void Build_NonModifica_ITalentiDelManuale()
    {
        var manuale = Manuale();

        CampaignExport.Build(new CampaignCatalogs(), "Tavolo di prova", manuale);

        Assert.Equal("srd-2024-it/talento/attento", manuale.Feats[0].Id);
    }

    /// <summary>Le voci del manuale caricato in memoria non vanno modificate: assegnare l'id
    /// esportabile <b>sopra</b> l'oggetto restituito dal catalogo lo corromperebbe per tutta la
    /// sessione, e la seconda esportazione — o la scheda aperta dopo — vedrebbe un id che il manuale
    /// non ha mai avuto.</summary>
    [Fact]
    public void Build_NonModifica_LeSottoclassiDelManuale()
    {
        var manuale = Manuale();
        manuale.Classes.Add(new PackageClass
        {
            Id = "srd-2024-it/classe/guerriero",
            Name = "Guerriero",
            Subclasses =
            {
                new PackageSubclass { Id = "srd-2024-it/sottoclasse/campione", Name = "Campione" },
            },
        });
        // Serve una riga importata con la colonna **vuota**: è il solo percorso in cui l'export
        // maneggia le istanze del manuale invece di oggetti appena letti dal testo. Con i soli
        // cataloghi vuoti il test passava a vuoto — `ConIlManuale` sintetizza la riga già con la
        // colonna scritta, e da lì in poi si lavora su copie.
        var cataloghi = new CampaignCatalogs
        {
            Classes = { new CharacterClass
            {
                Id = "u1", Name = "Guerriero", SourceId = "srd-2024-it/classe/guerriero",
                CampaignId = "c1", Subclasses = string.Empty,
            } },
        };

        CampaignExport.Build(cataloghi, "Tavolo di prova", manuale);

        Assert.Equal("srd-2024-it/sottoclasse/campione", manuale.Classes[0].Subclasses[0].Id);
    }

    /// <summary>Il difetto che chiude: l'export emette una voce per **riga**, ma risolveva le
    /// sottoclassi per **nome** su tutte le righe omonime. Con due «Barbaro» — una importata dal
    /// manuale e una del tavolo, che «Duplica e modifica» crea per costruzione — la riga del tavolo
    /// esportava le sottoclassi SRD dell'altra, cioè un contenuto che non è suo; e nel verso opposto
    /// una riga a cui l'utente le aveva <b>tolte</b> se le ritrovava nel file.</summary>
    [Fact]
    public void Build_ConDueRigheOmonime_OgnunaPortaLeProprieSottoclassi()
    {
        var cataloghi = new CampaignCatalogs
        {
            Classes =
            {
                new CharacterClass
                {
                    Id = "u1", Name = "Barbaro", SourceId = "srd-2024-it/classe/barbaro",
                    CampaignId = "c1",
                    Subclasses = SubclassText.Serializza(new List<PackageSubclass>
                    {
                        new() { Id = "srd-2024-it/sottoclasse/berserker", Name = "Cammino del berserker" },
                    }),
                },
                // La copia del tavolo, con le sottoclassi tolte a mano.
                new CharacterClass
                {
                    Id = "u2", Name = "Barbaro", SourceId = null, CampaignId = "c1",
                    Subclasses = string.Empty,
                },
            },
        };

        var classi = CampaignExport.Build(cataloghi, "Tavolo di prova", unisciIlManuale: false).Classes;

        // Le righe si riconoscono dall'ordine, non dall'id: `AssignIds` non conserva una provenienza
        // del manuale — la conserverebbe di sola lettura in una campagna terza — quindi entrambe
        // ricevono uno slug derivato dal nome.
        Assert.Equal(2, classi.Count);
        Assert.Single(classi[0].Subclasses);                 // la riga importata
        Assert.Empty(classi[1].Subclasses);                  // la copia del tavolo, svuotata a mano
    }

    /// <summary>Il difetto che chiude: «Duplica e modifica» da una voce di pacchetto copia l'elenco
    /// delle sottoclassi e <b>azzera</b> la provenienza della riga. La prosa SRD di una sottoclasse —
    /// che è descrizione intera, non un elenco di nomi — viaggiava quindi in una riga con
    /// <c>source_id</c> nullo, e l'export della sola campagna produceva un file con quel testo e
    /// <c>License = null</c>: fuori dalla licenza con cui lo SRD è ridistribuibile.</summary>
    [Fact]
    public void Build_RigaDuplicataConSottoclassiSRD_PortaComunqueLaLicenza()
    {
        var cataloghi = new CampaignCatalogs
        {
            Classes = { new CharacterClass
            {
                // Nessuna provenienza: è la copia creata da «Duplica e modifica».
                Id = "u1", Name = "Barbaro del sale", SourceId = null, CampaignId = "c1",
                Subclasses = SubclassText.Serializza(new List<PackageSubclass>
                {
                    new()
                    {
                        Id = "srd-2024-it/sottoclasse/berserker",
                        Name = "Cammino del berserker",
                        Description = "Testo SRD copiato per intero.",
                    },
                }),
            } },
        };

        var pacchetto = CampaignExport.Build(
            cataloghi, "Tavolo di prova", Manuale(), unisciIlManuale: false);

        Assert.NotNull(pacchetto.License);
    }
}
