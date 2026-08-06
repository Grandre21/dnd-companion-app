using DndCompanion.Models;
using DndCompanion.Models.Packages;
using DndCompanion.Services;
using Xunit;

namespace DndCompanion.Tests;

/// <summary>
/// Test di <see cref="SkillCatalog"/> — la corrispondenza SkillType ↔ nome italiano ↔ proprietà
/// bool su <see cref="Character"/>.
/// </summary>
public class SkillCatalogTests
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
    // Incrocio col pacchetto SRD — se una grafia cambia, questo test lo dice
    // -----------------------------------------------------------------------------------

    [Fact]
    public void Ogni_nome_di_skillChoices_delle_12_classi_mappa_su_una_SkillType()
    {
        var pacchetto = CaricaPacchetto();
        Assert.Equal(12, pacchetto.Classes.Count);

        foreach (var classe in pacchetto.Classes)
        {
            // Non un "continue" silenzioso: le 12 classi SRD hanno TUTTE skillChoices, quindi il
            // ramo "assente" non serve a nessun caso reale — e nascondeva esattamente il caso in
            // cui una classe lo perde (il vincolo sparirebbe e D11 smetterebbe di bloccare, senza
            // segnale: MINORE 7 del gate del 2026-08-06).
            Assert.True(classe.SkillChoices is not null,
                $"La classe «{classe.Name}» non ha skillChoices: il vincolo di scelta abilità " +
                "sparirebbe in silenzio per questa classe.");
            Assert.NotEmpty(classe.SkillChoices!.From);

            foreach (var nome in classe.SkillChoices.From)
                Assert.True(SkillCatalog.DaNome(nome) is not null,
                    $"«{nome}» (classe «{classe.Name}») non è riconosciuto da SkillCatalog.DaNome: " +
                    "il vincolo di scelta per questa classe degraderebbe silenziosamente al picker libero.");
        }
    }

    [Fact]
    public void Ogni_nome_di_skillProficiencies_dei_background_mappa_su_una_SkillType()
    {
        var pacchetto = CaricaPacchetto();
        Assert.NotEmpty(pacchetto.Backgrounds);

        foreach (var background in pacchetto.Backgrounds)
        {
            foreach (var nome in background.SkillProficiencies)
                Assert.True(SkillCatalog.DaNome(nome) is not null,
                    $"«{nome}» (background «{background.Name}») non è riconosciuto da SkillCatalog.DaNome.");
        }
    }

    // -----------------------------------------------------------------------------------
    // Round-trip dell'enum — reclama da sola la prossima abilità aggiunta
    // -----------------------------------------------------------------------------------

    [Fact]
    public void Tutte_contiene_esattamente_i_valori_dichiarati_dallenum()
    {
        var dallEnum = (SkillType[])Enum.GetValues(typeof(SkillType));
        Assert.Equal(dallEnum.OrderBy(s => s), SkillCatalog.Tutte.OrderBy(s => s));
        Assert.Equal(18, SkillCatalog.Tutte.Count);
    }

    [Theory]
    [MemberData(nameof(TutteLeAbilita))]
    public void DaNome_di_Nome_torna_lo_stesso_valore(SkillType abilita)
        => Assert.Equal(abilita, SkillCatalog.DaNome(SkillCatalog.Nome(abilita)));

    public static IEnumerable<object[]> TutteLeAbilita()
        => ((SkillType[])Enum.GetValues(typeof(SkillType))).Select(s => new object[] { s });

    // -----------------------------------------------------------------------------------
    // DaNome — tollerante a maiuscole, spazi e accenti
    // -----------------------------------------------------------------------------------

    [Theory]
    [InlineData("Atletica", SkillType.Athletics)]
    [InlineData("ATLETICA", SkillType.Athletics)]
    [InlineData("  Atletica  ", SkillType.Athletics)]
    [InlineData("rapidita di mano", SkillType.SleightOfHand)]
    [InlineData("Rapidità di Mano", SkillType.SleightOfHand)]
    [InlineData("rapidità   di   mano", SkillType.SleightOfHand)]
    [InlineData("furtivita", SkillType.Stealth)]
    public void DaNome_riconosce_maiuscole_spazi_e_accenti(string nome, SkillType atteso)
        => Assert.Equal(atteso, SkillCatalog.DaNome(nome));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Abilità inventata al tavolo")]
    public void DaNome_di_un_nome_non_riconosciuto_torna_null(string? nome)
        => Assert.Null(SkillCatalog.DaNome(nome));

    // -----------------------------------------------------------------------------------
    // DaElenco / DaElencoDiNomi
    // -----------------------------------------------------------------------------------

    [Fact]
    public void DaElenco_riconosce_le_voci_separate_da_virgola_nellordine()
    {
        var risultato = SkillCatalog.DaElenco("Atletica, Sopravvivenza");
        Assert.Equal(new[] { SkillType.Athletics, SkillType.Survival }, risultato);
    }

    [Fact]
    public void DaElenco_scarta_le_voci_non_riconosciute_senza_fermarsi()
    {
        var risultato = SkillCatalog.DaElenco("Atletica, Cucina, Sopravvivenza");
        Assert.Equal(new[] { SkillType.Athletics, SkillType.Survival }, risultato);
    }

    [Fact]
    public void DaElenco_non_duplica_le_ripetizioni()
    {
        var risultato = SkillCatalog.DaElenco("Atletica, atletica, ATLETICA");
        Assert.Equal(new[] { SkillType.Athletics }, risultato);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void DaElenco_di_testo_vuoto_torna_lista_vuota(string? testo)
        => Assert.Empty(SkillCatalog.DaElenco(testo));

    [Fact]
    public void DaElencoDiNomi_di_null_torna_lista_vuota()
        => Assert.Empty(SkillCatalog.DaElencoDiNomi(null));

    [Fact]
    public void DaElencoDiNomi_riconosce_scarta_e_non_duplica_come_DaElenco()
    {
        var risultato = SkillCatalog.DaElencoDiNomi(new[] { "Intuizione", "Cucina", "Intuizione", "Religione" });
        Assert.Equal(new[] { SkillType.Insight, SkillType.Religion }, risultato);
    }

    // -----------------------------------------------------------------------------------
    // Competente / ImpostaCompetenza / Esperto / ImpostaEsperienza — tutte e 18, non a campione
    // -----------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(TutteLeAbilita))]
    public void ImpostaCompetenza_scrive_e_Competente_legge_la_proprieta_giusta(SkillType abilita)
    {
        var pg = new Character();

        Assert.False(SkillCatalog.Competente(pg, abilita));
        SkillCatalog.ImpostaCompetenza(pg, abilita, true);
        Assert.True(SkillCatalog.Competente(pg, abilita));

        // Nessun'altra abilità deve essersi accesa: un ramo di switch copiaincollato male
        // scriverebbe sulla proprietà della skill vicina.
        foreach (var altra in SkillCatalog.Tutte.Where(s => s != abilita))
            Assert.False(SkillCatalog.Competente(pg, altra),
                $"ImpostaCompetenza({abilita}, true) ha acceso anche «{altra}».");

        SkillCatalog.ImpostaCompetenza(pg, abilita, false);
        Assert.False(SkillCatalog.Competente(pg, abilita));
    }

    [Theory]
    [MemberData(nameof(TutteLeAbilita))]
    public void ImpostaEsperienza_scrive_e_Esperto_legge_la_proprieta_giusta(SkillType abilita)
    {
        var pg = new Character();

        Assert.False(SkillCatalog.Esperto(pg, abilita));
        SkillCatalog.ImpostaEsperienza(pg, abilita, true);
        Assert.True(SkillCatalog.Esperto(pg, abilita));

        foreach (var altra in SkillCatalog.Tutte.Where(s => s != abilita))
            Assert.False(SkillCatalog.Esperto(pg, altra),
                $"ImpostaEsperienza({abilita}, true) ha acceso anche «{altra}».");

        SkillCatalog.ImpostaEsperienza(pg, abilita, false);
        Assert.False(SkillCatalog.Esperto(pg, abilita));
    }

    [Fact]
    public void Competenza_ed_esperienza_sono_indipendenti()
    {
        var pg = new Character();
        SkillCatalog.ImpostaEsperienza(pg, SkillType.Stealth, true);

        Assert.True(SkillCatalog.Esperto(pg, SkillType.Stealth));
        Assert.False(SkillCatalog.Competente(pg, SkillType.Stealth));
    }
}
