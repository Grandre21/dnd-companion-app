using System.Text.Json;
using DndCompanion.Models.Packages;
using DndCompanion.Services;
using Xunit;

namespace DndCompanion.Tests;

/// <summary>
/// Test sul CONTENUTO del pacchetto distribuito con l'app (<c>wwwroot/data/srd-2024-it.json</c>),
/// non sulla logica del parser (quella sta in <c>CatalogPackageParserTests</c>).
///
/// Perché serve: il pacchetto è un file di dati generato fuori dal compilatore, e i suoi difetti
/// non si vedono a build time. Le due modalità di rottura sono entrambe totali, non parziali:
/// una voce senza <c>id</c> fa **rifiutare l'intero pacchetto** (<c>CatalogPackageParser.Parse</c>),
/// e un decimale in <c>speed.value</c> — <c>PackageSpeed.Value</c> è un <c>int</c> — fa fallire la
/// **deserializzazione di tutto il file**, non della singola voce. In produzione si manifesterebbe
/// come "i cataloghi non hanno voci di manuale", senza un appiglio per capire perché.
/// </summary>
public class SrdPackageContentTests
{
    private const string PercorsoRelativo = "wwwroot/data/srd-2024-it.json";

    /// <summary>Gli otto nomi che <see cref="SpellClassNames"/> riconosce: un incantesimo che ne
    /// porta uno diverso non verrebbe mai trovato dal filtro per classe.</summary>
    private static readonly HashSet<string> ClassiIncantatrici =
        SpellClassNames.Pairs.Select(p => p.Italian).ToHashSet(StringComparer.Ordinal);

    /// <summary>I sei nomi che <see cref="CharacterWizardLogic.ParseSaveProficiencies"/> sa
    /// tradurre in chiavi: gli altri vengono scartati in silenzio, e il wizard proporrebbe una
    /// classe senza competenze nei tiri salvezza.</summary>
    private static readonly string[] Caratteristiche =
        { "Forza", "Destrezza", "Costituzione", "Intelligenza", "Saggezza", "Carisma" };

    private static string PercorsoPacchetto()
    {
        // Il test gira da bin/<config>/<tfm>/: si risale fino alla cartella che contiene il
        // .csproj dell'app, così il percorso non dipende dalla profondità di output.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "DndCompanion.csproj")))
            dir = dir.Parent;

        Assert.True(dir is not null, "Radice del progetto non trovata risalendo da " + AppContext.BaseDirectory);
        return Path.Combine(dir!.FullName, PercorsoRelativo);
    }

    private static CatalogPackage CaricaPacchetto()
    {
        var percorso = PercorsoPacchetto();
        Assert.True(File.Exists(percorso), $"Pacchetto SRD assente: {percorso}");

        var esito = CatalogPackageParser.Parse(File.ReadAllText(percorso));

        // Gli errori si mostrano tutti: con uno solo per volta servirebbe un giro di test per voce.
        Assert.True(esito.Errors.Count == 0,
            "Il pacchetto è stato RIFIUTATO dal parser:\n  " + string.Join("\n  ", esito.Errors));
        Assert.NotNull(esito.Package);
        return esito.Package!;
    }

    [Fact]
    public void Il_pacchetto_viene_accettato_dal_parser()
    {
        var pacchetto = CaricaPacchetto();

        Assert.Equal(CatalogPackageParser.SupportedSchemaVersion, pacchetto.SchemaVersion);
        Assert.Equal(CatalogPackageParser.AppPackageId, pacchetto.Id);
        Assert.Equal("it", pacchetto.Language);

        // L'attribuzione CC BY 4.0 non è un ornamento: è la condizione della licenza con cui il
        // contenuto SRD è ridistribuibile. Se sparisce, la ridistribuzione non è più conforme.
        Assert.False(string.IsNullOrWhiteSpace(pacchetto.License?.Attribution),
            "Manca l'attribuzione della licenza CC BY 4.0 dello SRD.");
    }

    [Fact]
    public void Il_pacchetto_ha_contenuto_in_tutte_le_sezioni()
    {
        var p = CaricaPacchetto();

        // Un pacchetto che si legge ma è vuoto passerebbe tutti gli altri test e lascerebbe il
        // wizard senza nulla da scegliere: è il difetto che questo test intercetta.
        Assert.NotEmpty(p.Species);
        Assert.NotEmpty(p.Backgrounds);
        Assert.NotEmpty(p.Classes);
        Assert.NotEmpty(p.Spells);
        Assert.NotEmpty(p.Monsters);
    }

    [Fact]
    public void Ogni_voce_ha_il_prefisso_del_pacchetto_dell_app()
    {
        var p = CaricaPacchetto();

        // Senza il prefisso "<id pacchetto>/", CatalogKey.IsFromAppPackage non riconosce la
        // provenienza e la voce verrebbe mostrata come modificabile invece che in sola lettura.
        var senzaPrefisso = TutteLeVoci(p)
            .Where(v => !CatalogKey.IsFromAppPackage(v.Id))
            .Select(v => $"{v.Sezione}: {v.Nome} ({v.Id})")
            .ToList();

        Assert.True(senzaPrefisso.Count == 0,
            "Voci senza il prefisso di provenienza:\n  " + string.Join("\n  ", senzaPrefisso.Take(20)));
    }

    [Fact]
    public void Le_velocita_delle_specie_sono_intere_e_nel_dominio_ammesso()
    {
        var p = CaricaPacchetto();

        foreach (var s in p.Species)
        {
            if (s.Speed is null) continue;

            // 'm' o 'ft': è il dominio del CHECK races_speed_unit_check sul database. Un altro
            // valore finirebbe nel fallback di PackageRowMerge.UnitaValida e verrebbe salvato
            // con un'unità diversa da quella dichiarata, in silenzio.
            Assert.True(s.Speed.Unit is "m" or "ft",
                $"Specie «{s.Name}»: unità '{s.Speed.Unit}' fuori dal dominio 'm'/'ft'.");

            // Il limite dipende dall'unità — 36 metri o 120 piedi — e non è una costante: si
            // verifica passando per il consumatore vero (PackageRowMerge.NuovaSpecie + FormValidation.ValidateRace),
            // così il test resta valido anche se il pacchetto un giorno tornasse metrico.
            var riga = PackageRowMerge.NuovaSpecie(s, "test-campaign", null);
            Assert.Null(FormValidation.ValidateRace(riga));
        }
    }

    [Fact]
    public void Le_velocita_convertite_in_metri_restano_plausibili()
    {
        var p = CaricaPacchetto();

        // Il wizard converte la velocità della specie nell'unità del PG (metri) prima di
        // scriverla. Se il pacchetto dichiarasse i piedi ma il valore fosse già metrico (o
        // viceversa), la conversione produrrebbe un personaggio lentissimo o velocissimo senza
        // che nulla fallisca: 3 m è meno di una creatura Piccola, 30 m è più di un cavallo.
        foreach (var s in p.Species)
        {
            if (s.Speed is null) continue;

            var metri = CharacterWizardLogic.SpeedInMeters(s.Speed.Value, s.Speed.Unit);
            Assert.True(metri is >= 5 and <= 25,
                $"Specie «{s.Name}»: {s.Speed.Value} {s.Speed.Unit} diventano {metri} m, fuori dal plausibile.");
        }
    }

    [Fact]
    public void I_background_concedono_caratteristiche_che_il_wizard_riconosce()
    {
        var p = CaricaPacchetto();

        // Speculare al test sui tiri salvezza delle classi. Un nome non tradotto (es. "Wisdom")
        // verrebbe scartato in silenzio da BackgroundAbilityKeys: il wizard direbbe che il
        // background non concede bonus e salverebbe il PG senza i +2/+1 del modello 2024,
        // lasciando anche `background_ability_choice` vuoto. Nessun errore, dati sbagliati.
        foreach (var b in p.Backgrounds)
        {
            var chiavi = CharacterWizardLogic.BackgroundAbilityKeys(string.Join(", ", b.AbilityScores));
            Assert.True(chiavi.Count == b.AbilityScores.Count,
                $"Background «{b.Name}»: riconosciute {chiavi.Count} caratteristiche su {b.AbilityScores.Count} " +
                $"([{string.Join(", ", b.AbilityScores)}]).");
        }
    }

    [Fact]
    public void Il_dado_vita_di_ogni_classe_e_utilizzabile_dal_wizard()
    {
        var p = CaricaPacchetto();

        // BuildHitDice e SuggestMaxHp restituiscono "" e 0 come sentinelle quando non sanno
        // leggere il dado: il wizard nasconde i pulsanti "usa il valore suggerito" e il
        // principiante resta senza aiuto proprio sui due campi che non sa calcolare.
        foreach (var c in p.Classes)
        {
            Assert.False(string.IsNullOrEmpty(CharacterWizardLogic.BuildHitDice(c.HitDie, 1)),
                $"Classe «{c.Name}»: dado vita '{c.HitDie}' non interpretabile.");
            Assert.True(CharacterWizardLogic.SuggestMaxHp(c.HitDie, 0, 1) > 0,
                $"Classe «{c.Name}»: PF suggeriti nulli con dado vita '{c.HitDie}'.");
        }
    }

    [Fact]
    public void Il_pacchetto_non_usa_termini_fuori_dal_glossario_ufficiale()
    {
        var percorso = PercorsoPacchetto();
        var testo = File.ReadAllText(percorso);

        // Termini che sembrano giusti ma non sono quelli del manuale italiano: chi cerca la
        // condizione con il nome ufficiale non troverebbe le voci scritte con l'altro. Il
        // controllo è sul file intero perché il rischio non è confinato a un campo.
        var banditi = new[]
        {
            ("ammaliat", "affascinat (condizione Charmed)"),
            ("Ammaliat", "Affascinat (condizione Charmed)"),
        };

        var trovati = banditi
            .Where(b => testo.Contains(b.Item1, StringComparison.Ordinal))
            .Select(b => $"'{b.Item1}' → usare '{b.Item2}'")
            .ToList();

        Assert.True(trovati.Count == 0,
            "Termini fuori dal glossario ufficiale nel pacchetto:\n  " + string.Join("\n  ", trovati));
    }

    [Fact]
    public void Le_classi_coprono_venti_livelli_con_nove_slot_ciascuno()
    {
        var p = CaricaPacchetto();

        foreach (var c in p.Classes)
        {
            var livelli = c.Levels.Select(l => l.Level).OrderBy(x => x).ToList();
            Assert.True(livelli.SequenceEqual(Enumerable.Range(1, 20)),
                $"Classe «{c.Name}»: attesi i livelli 1..20, trovati [{string.Join(", ", livelli)}].");

            foreach (var l in c.Levels)
            {
                // Nove slot, dal 1º al 9º livello di incantesimo: è il contratto dichiarato da
                // PackageClassLevel.SpellSlots. Una lista più corta manderebbe fuori range
                // qualunque lettura per indice a valle.
                Assert.True(l.SpellSlots.Count == 9,
                    $"Classe «{c.Name}» livello {l.Level}: {l.SpellSlots.Count} slot invece di 9.");
            }
        }
    }

    [Fact]
    public void I_tiri_salvezza_delle_classi_sono_riconosciuti_dal_wizard()
    {
        var p = CaricaPacchetto();

        foreach (var c in p.Classes)
        {
            var ignoti = c.SavingThrows.Where(s => !Caratteristiche.Contains(s)).ToList();
            Assert.True(ignoti.Count == 0,
                $"Classe «{c.Name}»: tiri salvezza non riconosciuti [{string.Join(", ", ignoti)}]. " +
                $"Attesi: {string.Join(", ", Caratteristiche)}.");

            // Verifica sul consumatore vero, non solo sui nomi: se ParseSaveProficiencies non
            // ne ricava nulla, il wizard proporrebbe la classe senza competenze.
            var chiavi = CharacterWizardLogic.ParseSaveProficiencies(string.Join(", ", c.SavingThrows));
            Assert.True(chiavi.Count == c.SavingThrows.Count,
                $"Classe «{c.Name}»: ParseSaveProficiencies ne ha riconosciuti {chiavi.Count} su {c.SavingThrows.Count}.");
        }
    }

    [Fact]
    public void Gli_incantesimi_usano_i_nomi_di_classe_che_il_filtro_riconosce()
    {
        var p = CaricaPacchetto();

        var ignoti = p.Spells
            .SelectMany(s => s.Classes.Select(c => (Incantesimo: s.Name, Classe: c)))
            .Where(x => !ClassiIncantatrici.Contains(x.Classe))
            .Select(x => $"{x.Incantesimo} → '{x.Classe}'")
            .Distinct()
            .ToList();

        Assert.True(ignoti.Count == 0,
            "Nomi di classe che SpellClassNames non riconosce (il filtro per classe non li troverebbe):\n  "
            + string.Join("\n  ", ignoti.Take(20)));
    }

    [Fact]
    public void I_livelli_degli_incantesimi_stanno_fra_zero_e_nove()
    {
        var p = CaricaPacchetto();

        foreach (var s in p.Spells)
        {
            // 0-9 è il range validato dai form del progetto: un valore fuori scala renderebbe
            // l'incantesimo non modificabile dopo un eventuale import in campagna.
            Assert.True(s.Level is null or >= 0 and <= 9,
                $"Incantesimo «{s.Name}»: livello {s.Level} fuori da 0..9.");
        }
    }

    [Fact]
    public void Il_filtro_per_classe_trova_incantesimi_per_ogni_classe_incantatrice()
    {
        var p = CaricaPacchetto();

        // Prova end-to-end della catena "contenuto → SpellClassNames.Matches": il campo del
        // pacchetto è una lista, quello del database è testo libero, e il filtro lavora sul
        // secondo. Se la lista fosse vuota o scritta in inglese, qui non uscirebbe nulla.
        foreach (var (italiano, _) in SpellClassNames.Pairs)
        {
            var quanti = p.Spells.Count(s => SpellClassNames.Matches(string.Join(", ", s.Classes), italiano));
            Assert.True(quanti > 0, $"Nessun incantesimo trovato per la classe «{italiano}».");
        }
    }

    [Fact]
    public void Il_pacchetto_e_deserializzabile_anche_dal_serializzatore_a_riflessione()
    {
        // Il parser dell'app usa un JsonSerializerContext generato a compile-time (TrimMode=full).
        // Questo test rilegge lo stesso file con il serializzatore ordinario: se le due strade
        // divergono, il difetto è nel file, non nel contesto generato.
        var testo = File.ReadAllText(PercorsoPacchetto());
        var pacchetto = JsonSerializer.Deserialize<CatalogPackage>(testo,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(pacchetto);
        Assert.NotEmpty(pacchetto!.Species);
    }

    private static IEnumerable<(string Sezione, string Id, string Nome)> TutteLeVoci(CatalogPackage p)
    {
        foreach (var x in p.Species) yield return ("specie", x.Id, x.Name);
        foreach (var x in p.Backgrounds) yield return ("background", x.Id, x.Name);
        foreach (var x in p.Feats) yield return ("talenti", x.Id, x.Name);
        foreach (var x in p.Classes) yield return ("classi", x.Id, x.Name);
        foreach (var x in p.Spells) yield return ("incantesimi", x.Id, x.Name);
        foreach (var x in p.Monsters) yield return ("mostri", x.Id, x.Name);
    }
}
