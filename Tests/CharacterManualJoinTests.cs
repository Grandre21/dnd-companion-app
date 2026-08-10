using DndCompanion.Models.Packages;
using DndCompanion.Services;
using Xunit;

namespace DndCompanion.Tests;

/// <summary>
/// Test di <see cref="CharacterManualJoin"/>: riconoscimento dei talenti nel testo libero di
/// <c>Character.Feats</c> e del background dal nome singolo di <c>Character.Background</c>.
/// </summary>
public class CharacterManualJoinTests
{
    private static PackageFeat Talento(string nome) => new() { Id = nome, Name = nome, Description = $"Regole di {nome}" };
    private static PackageBackground Background(string nome) => new() { Id = nome, Name = nome, Description = $"Storia di {nome}" };

    // ---- TalentiRiconosciuti ----

    [Fact]
    public void Due_nomi_separati_da_virgola_sono_riconosciuti_nell_ordine_del_testo()
    {
        // Il catalogo è nell'ordine INVERSO rispetto al testo: se il test passasse anche ordinando
        // per catalogo invece che per posizione nel testo, non proverebbe nulla sull'ordinamento.
        var catalogo = new List<PackageFeat> { Talento("Fortunato"), Talento("Attento") };

        var esito = CharacterManualJoin.TalentiRiconosciuti("Attento, Fortunato", catalogo);

        Assert.Equal(new[] { "Attento", "Fortunato" }, esito.Select(f => f.Name));
    }

    [Fact]
    public void Nomi_su_righe_separate_sono_riconosciuti()
    {
        var catalogo = new List<PackageFeat> { Talento("Attento"), Talento("Fortunato") };

        var esito = CharacterManualJoin.TalentiRiconosciuti("Attento\nFortunato", catalogo);

        Assert.Equal(new[] { "Attento", "Fortunato" }, esito.Select(f => f.Name));
    }

    [Fact]
    public void Nome_dentro_una_parola_piu_lunga_non_e_riconosciuto()
    {
        // "attento" compare davvero come sottostringa di "disattento" (indice 11): senza il
        // controllo sui confini di parola verrebbe riconosciuto per errore. "Attentato" NON va
        // bene come esempio: dopo "attent" prosegue con "a" invece che "o", quindi IndexOf
        // restituisce -1 e il controllo sui confini non viene mai esercitato.
        var catalogo = new List<PackageFeat> { Talento("Attento") };

        var esito = CharacterManualJoin.TalentiRiconosciuti("Oggi sono un po' disattento", catalogo);

        Assert.Empty(esito);
    }

    [Fact]
    public void Nome_composto_da_piu_parole_combacia_comunque()
    {
        var catalogo = new List<PackageFeat> { Talento("Iniziato alla magia") };

        var esito = CharacterManualJoin.TalentiRiconosciuti("Ho preso Iniziato alla magia al 4° livello", catalogo);

        Assert.Equal(new[] { "Iniziato alla magia" }, esito.Select(f => f.Name));
    }

    [Fact]
    public void Differenze_di_maiuscole_e_minuscole_sono_riconosciute_lo_stesso()
    {
        var catalogo = new List<PackageFeat> { Talento("Attento") };

        var esito = CharacterManualJoin.TalentiRiconosciuti("ATTENTO", catalogo);

        Assert.Equal(new[] { "Attento" }, esito.Select(f => f.Name));
    }

    [Fact]
    public void Prosa_libera_che_non_nomina_alcun_talento_da_lista_vuota()
    {
        var catalogo = new List<PackageFeat> { Talento("Attento"), Talento("Fortunato") };

        var esito = CharacterManualJoin.TalentiRiconosciuti(
            "Cresciuto per le strade di Waterdeep, diffida degli sconosciuti.", catalogo);

        Assert.Empty(esito);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Testo_null_o_vuoto_da_lista_vuota_senza_eccezioni(string? testo)
    {
        var catalogo = new List<PackageFeat> { Talento("Attento") };

        var esito = CharacterManualJoin.TalentiRiconosciuti(testo, catalogo);

        Assert.Empty(esito);
    }

    [Fact]
    public void Catalogo_vuoto_da_lista_vuota_senza_eccezioni()
    {
        var esito = CharacterManualJoin.TalentiRiconosciuti("Attento, Fortunato", new List<PackageFeat>());

        Assert.Empty(esito);
    }

    [Fact]
    public void Nessun_duplicato_quando_lo_stesso_nome_compare_due_volte_nel_testo()
    {
        var catalogo = new List<PackageFeat> { Talento("Attento") };

        var esito = CharacterManualJoin.TalentiRiconosciuti("Attento e ancora Attento", catalogo);

        Assert.Single(esito);
    }

    // ---- BackgroundRiconosciuto ----

    [Fact]
    public void BackgroundRiconosciuto_con_match_esatto()
    {
        var catalogo = new List<PackageBackground> { Background("Soldato"), Background("Eremita") };

        var esito = CharacterManualJoin.BackgroundRiconosciuto("Soldato", catalogo);

        Assert.Equal("Soldato", esito?.Name);
    }

    [Fact]
    public void BackgroundRiconosciuto_con_maiuscole_diverse()
    {
        var catalogo = new List<PackageBackground> { Background("Soldato") };

        var esito = CharacterManualJoin.BackgroundRiconosciuto("SOLDATO", catalogo);

        Assert.Equal("Soldato", esito?.Name);
    }

    [Fact]
    public void BackgroundRiconosciuto_con_nome_assente_e_null()
    {
        var catalogo = new List<PackageBackground> { Background("Soldato") };

        var esito = CharacterManualJoin.BackgroundRiconosciuto("Nobile", catalogo);

        Assert.Null(esito);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BackgroundRiconosciuto_con_ingresso_null_o_vuoto_e_null(string? nomeBackground)
    {
        var catalogo = new List<PackageBackground> { Background("Soldato") };

        var esito = CharacterManualJoin.BackgroundRiconosciuto(nomeBackground, catalogo);

        Assert.Null(esito);
    }

    // ---- SpecieRiconosciuta ----

    [Fact]
    public void SpecieRiconosciuta_MatchEsattoNormalizzato()
    {
        var catalogo = new List<PackageSpecies>
        {
            new() { Id = "srd-2024-it/nano", Name = "Nano", Description = "…" },
            new() { Id = "srd-2024-it/elfo", Name = "Elfo", Description = "…" },
        };

        Assert.Equal("Nano", CharacterManualJoin.SpecieRiconosciuta("  nano ", catalogo)?.Name);
        Assert.Equal("Elfo", CharacterManualJoin.SpecieRiconosciuta("ELFO", catalogo)?.Name);
    }

    [Fact]
    public void SpecieRiconosciuta_NomeConAccento_SiRiconosce()
    {
        // Il progetto compila con InvariantGlobalization=true: String.Normalize è un no-op
        // SILENZIOSO, quindi il match deve passare da CatalogKey.NormalizeName, che piega gli
        // accenti con una mappa scritta a mano. Questo è il caso che rende il test non vacuo.
        var catalogo = new List<PackageSpecies>
        {
            new() { Id = "x/mezzelfo", Name = "Mezzelfo", Description = "…" },
        };

        Assert.NotNull(CharacterManualJoin.SpecieRiconosciuta("mezzélfo", catalogo));
    }

    [Fact]
    public void SpecieRiconosciuta_NomeScrittoAMano_TornaNull()
    {
        var catalogo = new List<PackageSpecies>
        {
            new() { Id = "x/nano", Name = "Nano", Description = "…" },
        };

        Assert.Null(CharacterManualJoin.SpecieRiconosciuta("Nanetto delle Colline", catalogo));
        Assert.Null(CharacterManualJoin.SpecieRiconosciuta("", catalogo));
        Assert.Null(CharacterManualJoin.SpecieRiconosciuta(null, catalogo));
    }
}
