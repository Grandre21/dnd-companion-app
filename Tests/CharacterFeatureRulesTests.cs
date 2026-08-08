using DndCompanion.Models;
using DndCompanion.Models.Packages;
using DndCompanion.Services;
using Xunit;

namespace DndCompanion.Tests;

/// <summary>
/// Test di <see cref="CharacterFeatureRules"/> — il tag di economia d'azione dei privilegi, il
/// jsonb <c>characters.character_features</c>.
/// </summary>
public class CharacterFeatureRulesTests
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

        Assert.True(esito.Errors.Count == 0,
            "Il pacchetto è stato RIFIUTATO dal parser:\n  " + string.Join("\n  ", esito.Errors));
        Assert.NotNull(esito.Package);
        return esito.Package!;
    }

    // -----------------------------------------------------------------------------------
    // Normalizza — la rete che tiene un jsonb malformato fuori dalla scheda
    // -----------------------------------------------------------------------------------

    [Fact]
    public void Normalizza_ScartaLeVociSenzaNome()
    {
        var voci = new List<CharacterFeature?>
        {
            new() { Nome = "  ", Nota = "x" },
            null,
            new() { Nome = "Ira", Nota = "3/riposo lungo" },
        };

        var esito = CharacterFeatureRules.Normalizza(voci);

        Assert.Single(esito);
        Assert.Equal("Ira", esito[0].Nome);
    }

    /// <summary>Un tag ignoto torna a null («da classificare»), non a un valore indovinato:
    /// in combattimento un tag sbagliato è peggio di un tag mancante.</summary>
    [Fact]
    public void Normalizza_TagIgnoto_DiventaNull()
    {
        var esito = CharacterFeatureRules.Normalizza(new List<CharacterFeature?>
        {
            new() { Nome = "Ira", Azione = "azione bonus lunga" },
        });

        Assert.Null(esito[0].Azione);
    }

    [Fact]
    public void Normalizza_TagAmmessoMaConMaiuscole_SiRiportaAlValoreCanonico()
    {
        var esito = CharacterFeatureRules.Normalizza(new List<CharacterFeature?>
        {
            new() { Nome = "Ira", Azione = "BONUS" },
        });

        Assert.Equal("bonus", esito[0].Azione);
    }

    /// <summary>«Ira» e «IRA» non sopravvivono entrambe: stessa regola di ClassResourceRules.</summary>
    [Fact]
    public void Normalizza_ScartaIDuplicatiPerNomeNormalizzato()
    {
        var esito = CharacterFeatureRules.Normalizza(new List<CharacterFeature?>
        {
            new() { Nome = "Ira", Nota = "prima" },
            new() { Nome = "IRA", Nota = "seconda" },
        });

        Assert.Single(esito);
        Assert.Equal("prima", esito[0].Nota);
    }

    [Fact]
    public void Normalizza_NullOListaVuota_NonSolleva()
    {
        Assert.Empty(CharacterFeatureRules.Normalizza(null));
        Assert.Empty(CharacterFeatureRules.Normalizza(new List<CharacterFeature?>()));
    }

    [Fact]
    public void Normalizza_TroncaLaNotaOltreIlTetto()
    {
        var notaLunga = new string('x', 2500);
        var esito = CharacterFeatureRules.Normalizza(new List<CharacterFeature?>
        {
            new() { Nome = "Ira", Nota = notaLunga },
        });

        Assert.Equal(2000, esito[0].Nota.Length);
    }

    // -----------------------------------------------------------------------------------
    // NormalizzaBozza — la gemella di Normalizza per la singola bozza in modifica (foglio di
    // dettaglio e pannello di aggiunta, v. Pages/Characters.razor e
    // Shared/CharacterTabs/CharacterFeaturesSection.razor)
    // -----------------------------------------------------------------------------------

    [Fact]
    public void NormalizzaBozza_NotaConSpaziAiBordi_VieneRifilata()
    {
        var esito = CharacterFeatureRules.NormalizzaBozza(new CharacterFeature
        {
            Nome = "Ira",
            Nota = "  tre volte per riposo lungo  ",
        });

        Assert.Equal("tre volte per riposo lungo", esito.Nota);
    }

    /// <summary>Stessa soglia di <see cref="Normalizza_TroncaLaNotaOltreIlTetto"/> (2000 caratteri,
    /// 2500 scritti): il caso che rende il test non vacuo. Se NormalizzaBozza avesse una soglia
    /// diversa da Normalizza — o nessuna — il salvataggio dal foglio e quello dall'aggiunta
    /// divergerebbero fra loro e da quanto Normalizza produce al prossimo caricamento (v. commento
    /// su NormalizzaBozza).</summary>
    [Fact]
    public void NormalizzaBozza_TroncaLaNotaAllaStessaSogliaDiNormalizza()
    {
        var notaLunga = new string('x', 2500);
        var esito = CharacterFeatureRules.NormalizzaBozza(new CharacterFeature
        {
            Nome = "Ira",
            Nota = notaLunga,
        });

        Assert.Equal(2000, esito.Nota.Length);
    }

    [Fact]
    public void NormalizzaBozza_AzioneVuota_DiventaNull()
    {
        var esito = CharacterFeatureRules.NormalizzaBozza(new CharacterFeature { Nome = "Ira", Azione = "" });

        Assert.Null(esito.Azione);
    }

    [Fact]
    public void NormalizzaBozza_AzioneDiSoliSpazi_DiventaNull()
    {
        var esito = CharacterFeatureRules.NormalizzaBozza(new CharacterFeature { Nome = "Ira", Azione = "   " });

        Assert.Null(esito.Azione);
    }

    [Fact]
    public void NormalizzaBozza_RisorsaVuota_DiventaNull()
    {
        var esito = CharacterFeatureRules.NormalizzaBozza(new CharacterFeature { Nome = "Ira", Risorsa = "" });

        Assert.Null(esito.Risorsa);
    }

    [Fact]
    public void NormalizzaBozza_RisorsaDiSoliSpazi_DiventaNull()
    {
        var esito = CharacterFeatureRules.NormalizzaBozza(new CharacterFeature { Nome = "Ira", Risorsa = "   " });

        Assert.Null(esito.Risorsa);
    }

    [Fact]
    public void NormalizzaBozza_RisorsaConSpaziAiBordi_VieneRifilata()
    {
        var esito = CharacterFeatureRules.NormalizzaBozza(new CharacterFeature { Nome = "Ira", Risorsa = "  Ira  " });

        Assert.Equal("Ira", esito.Risorsa);
    }

    // -----------------------------------------------------------------------------------
    // AzioneSuggerita
    // -----------------------------------------------------------------------------------

    [Fact]
    public void AzioneSuggerita_ClasseOPrivilegioAssenteDallaMappa_TornaNull()
    {
        Assert.Null(CharacterFeatureRules.AzioneSuggerita("Ladro", "Attacco furtivo"));
        Assert.Null(CharacterFeatureRules.AzioneSuggerita("Barbaro", "Privilegio inventato"));
        Assert.Null(CharacterFeatureRules.AzioneSuggerita(null, "Ira"));
        Assert.Null(CharacterFeatureRules.AzioneSuggerita("Barbaro", null));
    }

    [Fact]
    public void AzioneSuggerita_IgnoraMaiuscoleEAccentiNelNomeDellaClasseEDelPrivilegio()
    {
        Assert.Equal("bonus", CharacterFeatureRules.AzioneSuggerita("BARBARO", "ira"));
        Assert.Equal("bonus", CharacterFeatureRules.AzioneSuggerita("  Barbaro  ", "  Ira  "));
    }

    /// <summary>La rete che tiene questa tabella onesta: ogni nome suggerito deve esistere DAVVERO
    /// fra i features del pacchetto SRD della sua classe. Se un nome viene ribattezzato nel
    /// pacchetto, questo test diventa rosso da solo — senza che nessuno debba ricordarsene.
    /// Stessa costruzione di ClassResourceRulesTests sulla mappa delle risorse.</summary>
    [Fact]
    public void AzioneSuggerita_OgniNomeInTabellaEsisteNelPacchetto()
    {
        var pacchetto = CaricaPacchetto();

        foreach (var (nomeClasse, privilegi) in CharacterFeatureRules.TabellaPerTest)
        {
            var classe = pacchetto.Classes.FirstOrDefault(
                c => CatalogKey.NormalizeName(c.Name) == nomeClasse);
            Assert.True(classe is not null, $"Classe non nel pacchetto: {nomeClasse}");

            var featuresDelPacchetto = classe!.Levels
                .SelectMany(l => l.Features)
                .Select(CatalogKey.NormalizeName)
                .ToHashSet(StringComparer.Ordinal);

            foreach (var nomePrivilegio in privilegi.Keys)
            {
                Assert.True(featuresDelPacchetto.Contains(nomePrivilegio),
                    $"«{nomePrivilegio}» non esiste fra i features di {classe.Name} nel pacchetto SRD.");
            }
        }
    }

    // -----------------------------------------------------------------------------------
    // TagAmmessi
    // -----------------------------------------------------------------------------------

    [Fact]
    public void TagAmmessi_ContieneICinqueValoriNellOrdineDiRaggruppamento()
    {
        Assert.Equal(new[] { "azione", "bonus", "reazione", "passivo", "turno" },
                     CharacterFeatureRules.TagAmmessi);
    }

    // -----------------------------------------------------------------------------------
    // EtichettaTag — il menu «Quando si usa», guardia contro un tag aggiunto senza etichetta
    // -----------------------------------------------------------------------------------

    [Fact]
    public void EtichettaTag_OgniTagAmmesso_HaUnEtichettaNonVuota()
    {
        // Il ramo di default di EtichettaTag è "_ => tag ?? string.Empty": se un caso viene
        // cancellato dallo switch, il metodo ricade sul tag grezzo invece di sollevare o tornare
        // vuoto. Controllare solo IsNullOrWhiteSpace non lo vedrebbe (il tag grezzo non è vuoto):
        // serve pretendere anche che l'etichetta sia DIVERSA dal tag grezzo, altrimenti il default
        // supera il test travestito da caso gestito.
        foreach (var tag in CharacterFeatureRules.TagAmmessi)
        {
            var etichetta = CharacterFeatureRules.EtichettaTag(tag);
            Assert.False(string.IsNullOrWhiteSpace(etichetta),
                $"«{tag}» non ha un'etichetta: il menu lo renderebbe come stringa vuota.");
            Assert.NotEqual(tag, etichetta);
        }
    }

    [Fact]
    public void EtichettaTag_Passivo_EAlSingolare()
    {
        // Al singolare perché descrive UNA voce nel menu, diverso dal plurale "Passivi" che
        // intitola il gruppo in CharacterFeatureJoin.
        Assert.Equal("Passivo", CharacterFeatureRules.EtichettaTag("passivo"));
    }

    // -----------------------------------------------------------------------------------
    // ÈImpalcatura — le voci di tabella che non sono capacità usabili
    // -----------------------------------------------------------------------------------

    [Fact]
    public void ÈImpalcatura_IncrementoPunteggioCaratteristica_ETrue()
    {
        Assert.True(CharacterFeatureRules.ÈImpalcatura("Incremento punteggio caratteristica"));
    }

    [Fact]
    public void ÈImpalcatura_IgnoraMaiuscoleEAccenti()
    {
        Assert.True(CharacterFeatureRules.ÈImpalcatura("INCREMENTO PUNTEGGIO CARATTERISTICA"));
    }

    /// <summary>Le voci di sottoclasse sono impalcatura anche loro, riusando
    /// <see cref="ClassProgression.RiguardaSottoclasse"/> — questo test le tiene onesta rispetto a
    /// quella funzione, non ne ripete i marcatori.</summary>
    [Fact]
    public void ÈImpalcatura_VoceDiSottoclasse_ERiusaRiguardaSottoclasse()
    {
        Assert.True(CharacterFeatureRules.ÈImpalcatura("Sottoclasse del Barbaro"));
        Assert.True(CharacterFeatureRules.ÈImpalcatura("Privilegio di sottoclasse"));
    }

    [Fact]
    public void ÈImpalcatura_UnaCapacitaVera_EFalse()
    {
        Assert.False(CharacterFeatureRules.ÈImpalcatura("Ira"));
    }

    [Fact]
    public void ÈImpalcatura_NullOVuoto_EFalse()
    {
        Assert.False(CharacterFeatureRules.ÈImpalcatura(null));
        Assert.False(CharacterFeatureRules.ÈImpalcatura("  "));
    }
}
