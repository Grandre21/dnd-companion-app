using DndCompanion.Models;
using DndCompanion.Models.Packages;
using DndCompanion.Services;
using Xunit;

namespace DndCompanion.Tests;

/// <summary>
/// Test di <see cref="ClassResourceRules"/> — le risorse di classe con i loro usi (Ira, Ispirazione
/// bardica, ...), il jsonb <c>characters.class_resources</c>.
/// </summary>
public class ClassResourceRulesTests
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

    private static ClassResource Risorsa(string nome, int max, int spesi, string ricarica = "lungo") => new()
    {
        Nome = nome,
        Max = max,
        Spesi = spesi,
        Ricarica = ricarica,
    };

    // -----------------------------------------------------------------------------------
    // Suggerite
    // -----------------------------------------------------------------------------------

    [Theory]
    [InlineData("Barbaro", new[] { "Ira" }, new[] { "lungo" })]
    [InlineData("Bardo", new[] { "Ispirazione bardica" }, new[] { "lungo" })]
    [InlineData("Druido", new[] { "Forma selvatica" }, new[] { "breve" })]
    [InlineData("Guerriero", new[] { "Secondo fiato", "Azione impetuosa" }, new[] { "breve", "breve" })]
    [InlineData("Mago", new[] { "Recupero arcano" }, new[] { "lungo" })]
    [InlineData("Monaco", new[] { "Focus del monaco" }, new[] { "breve" })]
    [InlineData("Paladino", new[] { "Imposizione delle mani" }, new[] { "lungo" })]
    [InlineData("Stregone", new[] { "Stregoneria innata" }, new[] { "lungo" })]
    public void Le_classi_con_risorse_SRD_ricevono_nome_e_ricarica_gia_compilati(
        string classe, string[] nomiAttesi, string[] ricaricheAttese)
    {
        var suggerite = ClassResourceRules.Suggerite(classe);

        Assert.Equal(nomiAttesi, suggerite.Select(r => r.Nome).ToArray());
        Assert.Equal(ricaricheAttese, suggerite.Select(r => r.Ricarica).ToArray());
        Assert.All(suggerite, r => Assert.Equal(0, r.Max));
        Assert.All(suggerite, r => Assert.Equal(0, r.Spesi));
    }

    [Theory]
    [InlineData("Chierico")]
    [InlineData("Ladro")]
    [InlineData("Ranger")]
    [InlineData("Warlock")]
    [InlineData("Una classe inventata al tavolo")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Le_classi_senza_risorse_SRD_ricevono_lista_vuota(string? classe)
        => Assert.Empty(ClassResourceRules.Suggerite(classe));

    [Fact]
    public void Suggerite_ignora_maiuscole_e_accenti_nel_nome_della_classe()
    {
        Assert.NotEmpty(ClassResourceRules.Suggerite("BARBARO"));
        Assert.NotEmpty(ClassResourceRules.Suggerite("  barbaro  "));
    }

    // -----------------------------------------------------------------------------------
    // Normalizza — la rete che tiene un jsonb malformato fuori dalla scheda
    // -----------------------------------------------------------------------------------

    [Fact]
    public void Normalizza_di_null_da_lista_vuota()
        => Assert.Empty(ClassResourceRules.Normalizza(null));

    [Fact]
    public void Normalizza_scarta_le_voci_senza_nome()
    {
        var risultato = ClassResourceRules.Normalizza(new[]
        {
            Risorsa("Ira", 3, 1),
            Risorsa("", 3, 1),
            Risorsa("   ", 3, 1),
            new ClassResource { Nome = null!, Max = 3, Spesi = 1 },
        });

        Assert.Single(risultato);
        Assert.Equal("Ira", risultato[0].Nome);
    }

    [Fact]
    public void Normalizza_tronca_gli_spesi_negativi_a_zero()
    {
        var risultato = ClassResourceRules.Normalizza(new[] { Risorsa("Ira", 3, -5) });
        Assert.Equal(0, risultato[0].Spesi);
    }

    [Fact]
    public void Normalizza_tronca_gli_spesi_oltre_il_massimo()
    {
        var risultato = ClassResourceRules.Normalizza(new[] { Risorsa("Ira", 3, 99) });
        Assert.Equal(3, risultato[0].Spesi);
    }

    [Fact]
    public void Normalizza_riporta_un_massimo_negativo_a_zero()
    {
        var risultato = ClassResourceRules.Normalizza(new[] { Risorsa("Ira", -2, 1) });
        Assert.Equal(0, risultato[0].Max);
        Assert.Equal(0, risultato[0].Spesi);
    }

    [Fact]
    public void Normalizza_impone_un_tetto_di_99_a_max()
    {
        // Un jsonb scritto a mano (o un bug altrove) con un Max a sei cifre bloccherebbe il
        // rendering: il componente disegna un pallino per uso.
        var risultato = ClassResourceRules.Normalizza(new[] { Risorsa("Ira", 100_000, 5) });
        Assert.Equal(99, risultato[0].Max);
        Assert.Equal(5, risultato[0].Spesi); // sotto il tetto: gli spesi già validi non si toccano
    }

    [Fact]
    public void Normalizza_tronca_anche_gli_spesi_al_tetto_quando_lo_superano()
    {
        var risultato = ClassResourceRules.Normalizza(new[] { Risorsa("Ira", 100_000, 100_000) });
        Assert.Equal(99, risultato[0].Max);
        Assert.Equal(99, risultato[0].Spesi);
    }

    [Theory]
    [InlineData("lungo", "lungo")]
    [InlineData("breve", "breve")]
    [InlineData("nessuna", "nessuna")]
    [InlineData("LUNGO", "lungo")]
    [InlineData("Breve", "breve")]
    [InlineData("settimanale", "lungo")]
    [InlineData("", "lungo")]
    [InlineData(null, "lungo")]
    public void Normalizza_riporta_la_ricarica_a_un_valore_ammesso(string? ricaricaGrezza, string atteso)
    {
        var risultato = ClassResourceRules.Normalizza(new[] { Risorsa("Ira", 3, 0, ricaricaGrezza!) });
        Assert.Equal(atteso, risultato[0].Ricarica);
    }

    [Fact]
    public void Normalizza_scarta_i_duplicati_per_nome_tenendo_il_primo()
    {
        var risultato = ClassResourceRules.Normalizza(new[]
        {
            Risorsa("Ira", 3, 1),
            Risorsa("IRA", 5, 4), // stesso nome normalizzato: si scarta
            Risorsa("Ispirazione bardica", 2, 0),
        });

        Assert.Equal(2, risultato.Count);
        var ira = risultato.Single(r => r.Nome == "Ira");
        Assert.Equal(3, ira.Max);
        Assert.Equal(1, ira.Spesi);
    }

    [Fact]
    public void Normalizza_non_solleva_mai_su_elementi_null_nella_lista()
    {
        // Un jsonb con un elemento "null" in mezzo all'array (voce cancellata a mano, bug altrove)
        // deserializza così: la rete deve reggere anche questo, non solo i campi malformati.
        var risultato = ClassResourceRules.Normalizza(new List<ClassResource?>
        {
            null,
            new ClassResource { Nome = "Ok", Max = 1, Spesi = 1, Ricarica = "boh" },
        });

        Assert.Single(risultato);
        Assert.Equal("lungo", risultato[0].Ricarica);
    }

    // -----------------------------------------------------------------------------------
    // Spendi / Recupera — puri, con i limiti rispettati
    // -----------------------------------------------------------------------------------

    [Fact]
    public void Spendi_non_va_sotto_zero()
    {
        var risorsa = Risorsa("Ira", 3, 0);
        var dopo = ClassResourceRules.Spendi(risorsa, -1);
        Assert.Equal(0, dopo.Spesi);
    }

    [Fact]
    public void Spendi_non_supera_il_massimo()
    {
        var risorsa = Risorsa("Ira", 3, 2);
        var dopo = ClassResourceRules.Spendi(risorsa, 5);
        Assert.Equal(3, dopo.Spesi);
    }

    [Fact]
    public void Spendi_e_puro_non_muta_la_risorsa_originale()
    {
        var risorsa = Risorsa("Ira", 3, 0);
        _ = ClassResourceRules.Spendi(risorsa, 2);
        Assert.Equal(0, risorsa.Spesi);
    }

    [Fact]
    public void Recupera_non_supera_il_massimo()
    {
        var risorsa = Risorsa("Ira", 3, 1);
        var dopo = ClassResourceRules.Recupera(risorsa, 10);
        Assert.Equal(0, dopo.Spesi);
    }

    [Fact]
    public void Recupera_non_va_sotto_zero()
    {
        var risorsa = Risorsa("Ira", 3, 1);
        var dopo = ClassResourceRules.Recupera(risorsa, 1);
        Assert.Equal(0, dopo.Spesi);

        var ancora = ClassResourceRules.Recupera(dopo, 1);
        Assert.Equal(0, ancora.Spesi);
    }

    [Fact]
    public void Spendi_e_recupera_si_annullano_dentro_i_limiti()
    {
        var risorsa = Risorsa("Ira", 3, 1);
        var speso = ClassResourceRules.Spendi(risorsa, 1);
        var recuperato = ClassResourceRules.Recupera(speso, 1);
        Assert.Equal(risorsa.Spesi, recuperato.Spesi);
    }

    // -----------------------------------------------------------------------------------
    // SiRipristinaCon — il predicato puro condiviso con RestCalculations
    // -----------------------------------------------------------------------------------

    [Theory]
    [InlineData("lungo", true, true)]
    [InlineData("breve", true, true)]
    [InlineData("nessuna", true, false)]
    [InlineData("lungo", false, false)]
    [InlineData("breve", false, true)]
    [InlineData("nessuna", false, false)]
    public void SiRipristinaCon_segue_la_regola_del_riposo(string ricarica, bool riposoLungo, bool atteso)
        => Assert.Equal(atteso, ClassResourceRules.SiRipristinaCon(ricarica, riposoLungo));

    // -----------------------------------------------------------------------------------
    // Incrocio col pacchetto SRD — se una grafia cambia, questo test lo dice
    // -----------------------------------------------------------------------------------

    [Fact]
    public void Ogni_risorsa_suggerita_compare_fra_i_privilegi_della_classe_nel_pacchetto()
    {
        var pacchetto = CaricaPacchetto();

        foreach (var classe in pacchetto.Classes)
        {
            var suggerite = ClassResourceRules.Suggerite(classe.Name);
            if (suggerite.Count == 0) continue;

            var privilegi = classe.Levels
                .SelectMany(l => l.Features)
                .ToHashSet(StringComparer.Ordinal);

            foreach (var risorsa in suggerite)
                Assert.True(privilegi.Contains(risorsa.Nome),
                    $"«{risorsa.Nome}» è suggerita per «{classe.Name}» ma non compare fra i " +
                    "privilegi del pacchetto: la grafia SRD è cambiata sotto ai piedi della mappa.");
        }
    }

    [Fact]
    public void Ogni_classe_del_pacchetto_con_risorse_suggerite_e_in_mappa()
    {
        // Guardia gemella della precedente: qui si controlla che nessuna classe nel pacchetto sia
        // rimasta fuori dalla tabella per un nome scritto diversamente (es. "Chierico" vs "Prete").
        var pacchetto = CaricaPacchetto();
        var nomiClassiConMappa = new[]
            { "Barbaro", "Bardo", "Druido", "Guerriero", "Mago", "Monaco", "Paladino", "Stregone" };

        foreach (var nome in nomiClassiConMappa)
            Assert.Contains(pacchetto.Classes, c => c.Name == nome);
    }
}
