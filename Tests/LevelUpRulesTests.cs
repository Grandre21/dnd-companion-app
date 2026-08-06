using System.Text.Json;
using DndCompanion.Models.Packages;
using DndCompanion.Services;
using Xunit;

namespace DndCompanion.Tests;

public class LevelUpRulesTests
{
    private const string PercorsoRelativo = "wwwroot/data/srd-2024-it.json";

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

        var esito = CatalogPackageParser.Parse(File.ReadAllText(percorso), èIlManualeDellApp: true);

        // Gli errori si mostrano tutti: con uno solo per volta servirebbe un giro di test per voce.
        Assert.True(esito.Errors.Count == 0,
            "Il pacchetto è stato RIFIUTATO dal parser:\n  " + string.Join("\n  ", esito.Errors));
        Assert.NotNull(esito.Package);
        return esito.Package!;
    }

    [Fact]
    public void Ogni_classe_che_ha_slot_ha_una_caratteristica_da_incantatore()
    {
        var pacchetto = CaricaPacchetto();

        foreach (var classe in pacchetto.Classes)
        {
            var haSlot = classe.Levels.Any(l => l.SpellSlots.Any(s => s > 0));
            var caratteristica = LevelUpRules.CaratteristicaIncantatore(classe.Name);

            if (haSlot)
                Assert.True(caratteristica is not null,
                    $"«{classe.Name}» ha slot nel pacchetto ma nessuna caratteristica in mappa: " +
                    "la scheda mostrerebbe gli slot senza la CD degli incantesimi.");
            else
                Assert.True(caratteristica is null,
                    $"«{classe.Name}» non ha slot ma è in mappa come incantatore.");
        }
    }

    [Theory]
    [InlineData("Mago", "intelligence")]
    [InlineData("Ranger", "wisdom")]      // primaryAbility dice Destrezza: non è derivabile
    [InlineData("Paladino", "charisma")]  // primaryAbility dice «Forza e Carisma»
    [InlineData("Barbaro", null)]
    public void La_caratteristica_da_incantatore_e_in_inglese_minuscolo(string classe, string? atteso)
        => Assert.Equal(atteso, LevelUpRules.CaratteristicaIncantatore(classe));

    [Fact]
    public void Le_categorie_di_talento_che_servono_esistono_nel_pacchetto()
    {
        var pacchetto = CaricaPacchetto();
        var categorie = pacchetto.Feats.Select(f => f.Category).ToHashSet(StringComparer.Ordinal);

        Assert.Contains("Generale", categorie);
        Assert.Contains("Stile di combattimento", categorie);
        Assert.Contains("Epico", categorie);
    }

    [Fact]
    public void Il_talento_dell_incremento_si_riconosce_nel_pacchetto()
    {
        var pacchetto = CaricaPacchetto();
        var generali = pacchetto.Feats.Where(f => f.Category == "Generale").ToList();

        Assert.Single(generali, LevelUpRules.ÈTalentoDiIncremento);
    }

    [Theory]
    [InlineData("Sottoclasse del Barbaro", TipoDiScelta.Sottoclasse)]
    [InlineData("Tradizione arcana", TipoDiScelta.Sottoclasse)]
    [InlineData("Incremento punteggio caratteristica", TipoDiScelta.TalentoGenerale)]
    [InlineData("Stile di combattimento", TipoDiScelta.StileDiCombattimento)]
    [InlineData("Dono epico", TipoDiScelta.DonoEpico)]
    [InlineData("Invocazioni occulte", TipoDiScelta.Libera)]
    [InlineData("Metamagia", TipoDiScelta.Libera)]
    [InlineData("Ira", TipoDiScelta.Nessuna)]
    [InlineData(null, TipoDiScelta.Nessuna)]
    public void I_privilegi_che_aprono_una_scelta_si_riconoscono(string? privilegio, TipoDiScelta atteso)
        => Assert.Equal(atteso, LevelUpRules.TipoDi(privilegio));

    [Fact]
    public void Ogni_privilegio_del_pacchetto_che_dice_scegli_e_riconosciuto()
    {
        // Guardia contro il pacchetto che cambia sotto i piedi al codice: se una classe introduce
        // un privilegio di scelta con un nome nuovo, il dialogo lo mostrerebbe come passivo e la
        // scelta sparirebbe in silenzio. Le parole spia sono quelle dello SRD 2024.
        var pacchetto = CaricaPacchetto();
        string[] spie = { "sottoclasse", "incremento", "stile di combattimento", "dono epico",
                          "invocazioni", "metamagia", "maestria", "tradizione", "giuramento" };

        var mancanti = pacchetto.Classes
            .SelectMany(c => c.Levels)
            .SelectMany(l => l.Features)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(f => spie.Any(s => f.Contains(s, StringComparison.OrdinalIgnoreCase)))
            .Where(f => LevelUpRules.TipoDi(f) == TipoDiScelta.Nessuna)
            .ToList();

        Assert.True(mancanti.Count == 0,
            "Privilegi che sembrano una scelta ma non sono riconosciuti:\n  " +
            string.Join("\n  ", mancanti));
    }
}
