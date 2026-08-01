using DndCompanion.Models;
using DndCompanion.Models.Packages;
using DndCompanion.Services;
using Xunit;

namespace DndCompanion.Tests;

/// <summary>
/// Le sottoclassi del manuale. Il difetto che chiudono: <c>Character.Subclass</c> era un campo di
/// testo e nient'altro — si poteva scrivere «Berserker» e non succedeva niente, perché il pacchetto
/// non portava né i nomi ufficiali né i privilegi che quella scelta concede.
/// </summary>
public class SubclassCatalogTests
{
    private static PackageClass Classe(string nome, params PackageSubclass[] sottoclassi) => new()
    {
        Id = "srd-2024-it/classe/" + nome.ToLowerInvariant(),
        Name = nome,
        Subclasses = sottoclassi.ToList(),
    };

    private static PackageSubclass Sottoclasse(string nome, params (int Livello, string Privilegio)[] privilegi)
        => new()
        {
            Id = "srd-2024-it/sottoclasse/" + nome.ToLowerInvariant().Replace(' ', '-'),
            Name = nome,
            Description = "Descrizione di " + nome,
            Levels = privilegi
                .GroupBy(p => p.Livello)
                .Select(g => new PackageClassLevel
                {
                    Level = g.Key,
                    Features = g.Select(p => p.Privilegio).ToList(),
                })
                .ToList(),
        };

    private static readonly PackageClass[] Manuale =
    {
        Classe("Barbaro", Sottoclasse("Cammino del berserker",
            (3, "Frenesia"), (6, "Ira incontenibile"), (10, "Ritorsione"))),
        Classe("Mago", Sottoclasse("Invocatore", (3, "Sapienza dell'invocazione"))),
        Classe("Ladro"),
    };

    [Fact]
    public void PerClasse_restituisce_le_sottoclassi_della_classe()
    {
        var voci = SubclassCatalog.PerClasse(Manuale, "Barbaro");

        Assert.Equal(new[] { "Cammino del berserker" }, voci.Select(s => s.Name));
    }

    [Theory]
    [InlineData("barbaro")]
    [InlineData("BARBARO")]
    [InlineData("  Barbaro ")]
    public void PerClasse_normalizza_il_nome_della_classe(string nome)
        => Assert.Single(SubclassCatalog.PerClasse(Manuale, nome));

    [Theory]
    [InlineData("Ladro")]        // classe nel manuale, ma senza sottoclassi
    [InlineData("Artefice")]     // classe che il manuale non ha
    [InlineData("")]
    [InlineData(null)]
    public void PerClasse_senza_corrispondenza_restituisce_una_lista_vuota(string? nome)
        => Assert.Empty(SubclassCatalog.PerClasse(Manuale, nome));

    [Fact]
    public void PerClasse_regge_una_collezione_nulla()
        => Assert.Empty(SubclassCatalog.PerClasse(null, "Barbaro"));

    [Fact]
    public void Trova_riconosce_la_sottoclasse_scelta()
    {
        var voce = SubclassCatalog.Trova(Manuale, "Barbaro", "cammino del berserker");

        Assert.NotNull(voce);
        Assert.Equal("Cammino del berserker", voce!.Name);
    }

    /// <summary>Un nome scritto a mano è legittimo — un tavolo può inventarsi la propria
    /// sottoclasse — e non deve essere scambiato per una del manuale.</summary>
    [Fact]
    public void Trova_su_un_nome_inventato_restituisce_null()
        => Assert.Null(SubclassCatalog.Trova(Manuale, "Barbaro", "Cammino del pescatore"));

    [Fact]
    public void PrivilegiFinoAl_taglia_al_livello_del_personaggio()
    {
        var voce = SubclassCatalog.Trova(Manuale, "Barbaro", "Cammino del berserker");

        Assert.Empty(SubclassCatalog.PrivilegiFinoAl(voce, 2));
        Assert.Equal(new[] { 3 }, SubclassCatalog.PrivilegiFinoAl(voce, 3).Select(r => r.Livello));
        Assert.Equal(new[] { 3, 6 }, SubclassCatalog.PrivilegiFinoAl(voce, 7).Select(r => r.Livello));
    }

    [Fact]
    public void PrivilegiFinoAl_di_una_sottoclasse_assente_non_esplode()
        => Assert.Empty(SubclassCatalog.PrivilegiFinoAl(null, 20));

    // ---- Che fare della scelta quando cambia la classe ----

    [Fact]
    public void RisolviScelta_tiene_la_sottoclasse_valida_per_la_classe()
    {
        var scelta = SubclassCatalog.RisolviScelta(Manuale, "Barbaro", "Cammino del berserker");

        Assert.Equal("Cammino del berserker", scelta.Valore);
        Assert.False(scelta.ScrittaAMano);
    }

    /// <summary>Il caso che il gate ha trovato: scelgo Barbaro e la sua sottoclasse, poi cambio in
    /// Mago. Il menu non contiene più quella voce e mostra «Nessuna», ma il campo la conservava —
    /// e si salvava un Mago con il Cammino del berserker.</summary>
    [Fact]
    public void RisolviScelta_toglie_la_sottoclasse_di_unaltra_classe()
    {
        var scelta = SubclassCatalog.RisolviScelta(Manuale, "Mago", "Cammino del berserker");

        Assert.Equal(string.Empty, scelta.Valore);
        Assert.False(scelta.ScrittaAMano);
    }

    /// <summary>Un nome che il manuale non conosce si tiene: può essere la sottoclasse inventata
    /// dal tavolo, e cancellarla sarebbe peggio che mostrarla nel campo libero. È anche il caso di
    /// ogni personaggio creato prima che le sottoclassi esistessero.</summary>
    [Theory]
    [InlineData("Barbaro", "Cammino del pescatore")]
    [InlineData("Ladro", "Berserker")]
    [InlineData("Artefice", "Alchimista")]
    public void RisolviScelta_conserva_un_nome_scritto_a_mano(string classe, string sottoclasse)
    {
        var scelta = SubclassCatalog.RisolviScelta(Manuale, classe, sottoclasse);

        Assert.Equal(sottoclasse, scelta.Valore);
        Assert.True(scelta.ScrittaAMano);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void RisolviScelta_su_un_campo_vuoto_non_inventa_nulla(string? sottoclasse)
    {
        var scelta = SubclassCatalog.RisolviScelta(Manuale, "Barbaro", sottoclasse);

        Assert.Equal(string.Empty, scelta.Valore);
        Assert.False(scelta.ScrittaAMano);
    }

    /// <summary>Il secondo giro del gate: se la classe **non** è nel manuale, il confronto «questa
    /// sottoclasse è di un'altra classe» non ha senso e non deve cancellare niente. Il «Guerriero del
    /// sale» di un tavolo può chiamare «Cammino del berserker» la propria sottoclasse; togliergliela
    /// era una perdita che si consumava alla sola apertura della modifica, senza toccare nulla.</summary>
    [Theory]
    [InlineData("Guerriero del sale", "Cammino del berserker")]
    [InlineData("Barbaro del ghiaccio", "Invocatore")]
    public void RisolviScelta_non_tocca_la_sottoclasse_di_una_classe_fuori_dal_manuale(
        string classe, string sottoclasse)
    {
        var scelta = SubclassCatalog.RisolviScelta(Manuale, classe, sottoclasse);

        Assert.Equal(sottoclasse, scelta.Valore);
        Assert.True(scelta.ScrittaAMano);
    }

    /// <summary>La variante che è sopravvissuta a due giri: la classe del tavolo porta il nome di
    /// una del manuale («Mago»), ma è sua, e la sua sottoclasse si chiama come quella di un'altra
    /// classe SRD. Guardando il solo manuale il nome combacia e il valore veniva cancellato
    /// all'apertura della modifica, mentre il menu — che pone la domanda giusta — non era nemmeno
    /// stato offerto.</summary>
    [Fact]
    public void RisolviScelta_non_tocca_la_sottoclasse_di_una_classe_del_tavolo_omonima()
    {
        var righeDelTavolo = new[]
        {
            new CharacterClass { Id = "u1", Name = "Mago", CampaignId = "c1", SourceId = null },
        };

        var scelta = SubclassCatalog.RisolviScelta(
            Manuale, "Mago", "Cammino del berserker", righeDelTavolo);

        Assert.Equal("Cammino del berserker", scelta.Valore);
        Assert.True(scelta.ScrittaAMano);

        // Una riga importata dal manuale, invece, è la stessa classe: lì lo scollegamento resta.
        var righeImportate = new[]
        {
            new CharacterClass
            {
                Id = "u2", Name = "Mago", CampaignId = "c1",
                SourceId = "srd-2024-it/classe/mago",
            },
        };

        Assert.Equal(string.Empty, SubclassCatalog
            .RisolviScelta(Manuale, "Mago", "Cammino del berserker", righeImportate).Valore);
    }

    /// <summary>Il valore torna nella grafia del manuale, non in quella che si aveva in mano: il
    /// confronto normalizza maiuscole, accenti e spazi, ma il `select` accosta le stringhe per
    /// intero — un «invocatore» salvato a mano lasciava il menu senza selezione pur essendo la
    /// scelta giusta, e al salvataggio successivo il campo si svuotava.</summary>
    [Theory]
    [InlineData("invocatore")]
    [InlineData("INVOCATORE")]
    [InlineData("  Invocatore  ")]
    public void RisolviScelta_restituisce_il_nome_come_lo_scrive_il_manuale(string scritto)
    {
        var scelta = SubclassCatalog.RisolviScelta(Manuale, "Mago", scritto);

        Assert.Equal("Invocatore", scelta.Valore);
        Assert.False(scelta.ScrittaAMano);
    }

    /// <summary>Voci nulle dentro le liste: il parser le lascia passare (normalizza le liste, non i
    /// loro elementi), e il ramo «è di un'altra classe» le attraversava senza guardia.</summary>
    [Fact]
    public void RisolviScelta_regge_le_voci_nulle_di_un_pacchetto_di_terzi()
    {
        var sporco = new[]
        {
            null!,
            new PackageClass { Id = "x/classe/mago", Name = "Mago", Subclasses = new() { null! } },
        };

        var scelta = SubclassCatalog.RisolviScelta(sporco, "Mago", "Invocatore");

        Assert.Equal("Invocatore", scelta.Valore);
        Assert.True(scelta.ScrittaAMano);
        Assert.Empty(SubclassCatalog.PerClasse(sporco, "Mago"));
    }

    [Fact]
    public void PrimoLivello_dice_da_quando_la_sottoclasse_conta()
    {
        Assert.Equal(3, SubclassCatalog.PrimoLivello(
            SubclassCatalog.Trova(Manuale, "Barbaro", "Cammino del berserker")));
        Assert.Null(SubclassCatalog.PrimoLivello(null));
        Assert.Null(SubclassCatalog.PrimoLivello(new PackageSubclass { Name = "Vuota" }));
    }
}
