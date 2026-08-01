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

    // ---- La casa nei dati: le sottoclassi della riga di campagna ----

    private static CharacterClass Riga(string nome, string? sourceId, string sottoclassi, string id = "u1") => new()
    {
        Id = id,
        Name = nome,
        CampaignId = "c1",
        SourceId = sourceId,
        Subclasses = sottoclassi,
    };

    private const string ElencoDelTavolo = "## Sortilegio del sale\nL3 — Cristalli viventi\nL6 — Salamoia";

    /// <summary>Il difetto che questo chiude: le schermate guardavano il solo pacchetto, quindi una
    /// classe del tavolo non aveva modo di offrire le proprie sottoclassi — non c'era nemmeno dove
    /// scriverle.</summary>
    [Fact]
    public void Disponibili_preferisce_lelenco_della_riga_di_campagna()
    {
        var voci = SubclassCatalog.Disponibili(
            new[] { Riga("Mago", null, ElencoDelTavolo) }, Manuale, "Mago");

        Assert.Equal(new[] { "Sortilegio del sale" }, voci.Select(s => s.Name));
    }

    /// <summary>Una riga importata dal manuale e senza elenco è semplicemente vecchia — creata prima
    /// che l'import portasse le sottoclassi — e il pacchetto ne è la versione aggiornata.</summary>
    [Fact]
    public void Disponibili_ripiega_sul_pacchetto_per_una_riga_importata_dal_manuale()
    {
        var voci = SubclassCatalog.Disponibili(
            new[] { Riga("Mago", "srd-2024-it/classe/mago", string.Empty) }, Manuale, "Mago");

        Assert.Equal(new[] { "Invocatore" }, voci.Select(s => s.Name));
    }

    /// <summary>Una classe *del tavolo* senza sottoclassi proprie non eredita quelle SRD: offrire
    /// l'Invocatore per una «Mago» che quel tavolo ha deliberatamente sostituito farebbe dire alla
    /// stessa schermata due cose incoerenti.</summary>
    [Fact]
    public void Disponibili_non_presta_le_sottoclassi_del_manuale_a_una_classe_del_tavolo()
        => Assert.Empty(SubclassCatalog.Disponibili(
            new[] { Riga("Mago", null, string.Empty) }, Manuale, "Mago"));

    [Theory]
    [InlineData("Artefice")]
    [InlineData("")]
    [InlineData(null)]
    public void Disponibili_senza_corrispondenza_restituisce_una_lista_vuota(string? nome)
        => Assert.Empty(SubclassCatalog.Disponibili(null, Manuale, nome));

    [Fact]
    public void Disponibili_regge_le_collezioni_nulle()
        => Assert.Empty(SubclassCatalog.Disponibili(null, null, "Mago"));

    /// <summary>La scheda deve trovare i privilegi anche di una sottoclasse che il manuale non
    /// conosce: senza questo, la sottoclasse del tavolo si vedeva nel menu e poi non portava
    /// niente.</summary>
    [Fact]
    public void Trova_risolta_pesca_la_sottoclasse_del_tavolo_con_i_suoi_privilegi()
    {
        var righe = new[] { Riga("Mago", null, ElencoDelTavolo) };

        var voce = SubclassCatalog.Trova(righe, Manuale, "Mago", "sortilegio del sale");

        Assert.NotNull(voce);
        Assert.Equal("Sortilegio del sale", voce!.Name);
        Assert.Equal(new[] { 3 }, SubclassCatalog.PrivilegiFinoAl(voce, 5).Select(r => r.Livello));
        Assert.Equal(3, SubclassCatalog.PrimoLivello(voce));
    }

    [Fact]
    public void RisolviScelta_riconosce_nel_menu_la_sottoclasse_di_una_classe_del_tavolo()
    {
        var scelta = SubclassCatalog.RisolviScelta(
            Manuale, "Mago", "  SORTILEGIO DEL SALE ", new[] { Riga("Mago", null, ElencoDelTavolo) });

        Assert.Equal("Sortilegio del sale", scelta.Valore);
        Assert.False(scelta.ScrittaAMano);
    }

    /// <summary>Il ramo che cancella vale anche fra classi del tavolo: se la classe offre un elenco e
    /// il valore è la sottoclasse di un'altra classe, resterebbe altrimenti un valore che il menu non
    /// mostra e che al salvataggio successivo si risalva comunque.</summary>
    [Fact]
    public void RisolviScelta_toglie_la_sottoclasse_di_unaltra_classe_del_tavolo()
    {
        var righe = new[]
        {
            Riga("Mago", null, ElencoDelTavolo),
            Riga("Guerriero", null, "## Campione\nL3 — Critico migliorato", "u2"),
        };

        Assert.Equal(string.Empty,
            SubclassCatalog.RisolviScelta(Manuale, "Mago", "Campione", righe).Valore);
    }

    /// <summary>Il residuo di asimmetria che il gate ha trovato al primo giro: la classe del tavolo
    /// aveva un elenco proprio, quindi il ramo che cancella si attivava, e consultando il <b>manuale</b>
    /// trovava «Cammino del berserker» fra le sottoclassi del Barbaro — e lo cancellava. Ma se il
    /// tavolo ha sostituito quella classe con una propria, che quel nome sia del Barbaro SRD non dice
    /// niente su una sottoclasse inventata per una classe che di SRD non ha più nulla. Curioso e
    /// rivelatore: con l'elenco proprio <b>vuoto</b> il valore sopravviveva, con l'elenco pieno no.</summary>
    [Fact]
    public void RisolviScelta_non_consulta_il_manuale_per_una_classe_del_tavolo_con_elenco_proprio()
    {
        var scelta = SubclassCatalog.RisolviScelta(
            Manuale, "Mago", "Cammino del berserker", new[] { Riga("Mago", null, ElencoDelTavolo) });

        Assert.Equal("Cammino del berserker", scelta.Valore);
        Assert.True(scelta.ScrittaAMano);
    }

    /// <summary>La variante del caso precedente che il secondo giro del gate ha trovato: la prova «è
    /// di un'altra classe» non arrivava dal pacchetto ma da una riga <b>importata</b> dal manuale, che
    /// da quando l'import scrive la colonna porta lo stesso testo SRD. Guardando solo il pacchetto,
    /// l'esito dipendeva dal fatto che il tavolo avesse importato le classi: stesso contenuto, due
    /// risposte diverse — e in una delle due il valore veniva cancellato.</summary>
    [Fact]
    public void RisolviScelta_non_usa_una_riga_importata_come_prova_contro_una_classe_del_tavolo()
    {
        var righe = new[]
        {
            Riga("Mago", null, ElencoDelTavolo),
            Riga("Guerriero", "srd-2024-it/classe/guerriero", "## Campione\nL3 — Critico", "u2"),
        };

        var scelta = SubclassCatalog.RisolviScelta(Manuale, "Mago", "Campione", righe);

        Assert.Equal("Campione", scelta.Valore);
        Assert.True(scelta.ScrittaAMano);
    }

    /// <summary>Un nome che nessuna classe rivendica si tiene, anche quando la classe un elenco ce
    /// l'ha: può essere una sottoclasse inventata e non ancora messa a catalogo, e cancellarla
    /// sarebbe una perdita che si consuma alla sola apertura della modifica.</summary>
    [Fact]
    public void RisolviScelta_conserva_un_nome_che_nessuna_classe_rivendica()
    {
        var scelta = SubclassCatalog.RisolviScelta(
            Manuale, "Mago", "Sortilegio del pepe", new[] { Riga("Mago", null, ElencoDelTavolo) });

        Assert.Equal("Sortilegio del pepe", scelta.Valore);
        Assert.True(scelta.ScrittaAMano);
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
