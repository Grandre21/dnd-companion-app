using DndCompanion.Models.Packages;
using DndCompanion.Services;
using Xunit;

namespace DndCompanion.Tests;

/// <summary>Il formato testuale della colonna <c>classes.subclasses</c>. Il difetto che chiude: le
/// sottoclassi non avevano dove stare — il file ne portava, il parser le leggeva e l'import le
/// buttava, perché la tabella <c>classes</c> non aveva una colonna per tenerle. Da qui in poi una
/// classe del tavolo può avere le proprie.</summary>
public class SubclassTextTests
{
    private static PackageSubclass Sottoclasse(
        string nome, string? id = null, string descrizione = "", params (int Livello, string Privilegio)[] privilegi)
        => new()
        {
            Id = id ?? string.Empty,
            Name = nome,
            Description = descrizione,
            Levels = privilegi
                .GroupBy(p => p.Livello)
                .Select(g => new PackageClassLevel
                {
                    Level = g.Key,
                    Features = g.Select(p => p.Privilegio).ToList(),
                })
                .ToList(),
        };

    // ---- Andata e ritorno ----

    [Fact]
    public void Il_giro_completo_non_perde_niente()
    {
        var partenza = new[]
        {
            Sottoclasse("Cammino del berserker", "srd-2024-it/sottoclasse/berserker",
                "Chi percorre questo cammino incanala la furia.",
                (3, "Frenesia"), (6, "Ira incontenibile")),
            Sottoclasse("Cammino del guerriero totemico", null, "", (3, "Spirito totemico")),
        };

        var riletto = SubclassText.Leggi(SubclassText.Serializza(partenza));

        Assert.Equal(2, riletto.Count);
        Assert.Equal("Cammino del berserker", riletto[0].Name);
        Assert.Equal("srd-2024-it/sottoclasse/berserker", riletto[0].Id);
        Assert.Equal("Chi percorre questo cammino incanala la furia.", riletto[0].Description);
        Assert.Equal(new[] { 3, 6 }, riletto[0].Levels.Select(l => l.Level));
        Assert.Equal(new[] { "Frenesia" }, riletto[0].Levels[0].Features);
        Assert.Equal("Cammino del guerriero totemico", riletto[1].Name);
        Assert.Empty(riletto[1].Id);
        Assert.Empty(riletto[1].Description);
    }

    /// <summary>Il criterio che rende verificabile l'export: dal primo giro in poi il testo non deve
    /// più cambiare. Se serializzare ciò che si è appena letto producesse un testo diverso, ogni
    /// export darebbe un file diverso dal precedente senza che nessuno abbia toccato niente.</summary>
    [Fact]
    public void Serializzare_quel_che_si_e_letto_non_cambia_il_testo()
    {
        var canonico = SubclassText.Serializza(new[]
        {
            Sottoclasse("Invocatore", "x/sottoclasse/invocatore", "Sapienza dell'invocazione.",
                (3, "Recupero dell'invocazione"), (10, "Alterazione empirica")),
        });

        Assert.Equal(canonico, SubclassText.Serializza(SubclassText.Leggi(canonico)));
    }

    /// <summary>Il campo si scrive anche a mano in un textarea, e su Windows il browser manda
    /// "\r\n": un '\r' rimasto in coda alla riga sopravviverebbe al giro e cambierebbe il file
    /// esportato a ogni export.</summary>
    [Fact]
    public void I_ritorni_a_capo_di_Windows_non_sopravvivono_al_giro()
    {
        var testo = "## Campione\r\nAddestramento migliorato.\r\nL3 — Critico migliorato\r\n";

        var voci = SubclassText.Leggi(testo);

        Assert.Single(voci);
        Assert.Equal("Addestramento migliorato.", voci[0].Description);
        Assert.DoesNotContain('\r', SubclassText.Serializza(voci));
    }

    // ---- Lettura: quel che si tiene e quel che si ignora ----

    /// <summary>Il testo prima del primo blocco è di chi non conosce il formato: si ignora, e
    /// soprattutto non diventa una sottoclasse senza nome.</summary>
    [Fact]
    public void Il_testo_prima_del_primo_blocco_si_ignora()
    {
        var voci = SubclassText.Leggi("Da noi la sottoclasse si sceglie al 2°.\n\n## Campione\nL3 — Critico");

        Assert.Single(voci);
        Assert.Equal("Campione", voci[0].Name);
    }

    /// <summary>Un'intestazione senza nome chiude il blocco precedente e non ne apre uno: un blocco
    /// senza nome non sarebbe rileggibile, e il nome è la sola cosa che il personaggio sceglie.</summary>
    [Fact]
    public void Unintestazione_vuota_non_apre_un_blocco()
    {
        var voci = SubclassText.Leggi("## Campione\nL3 — Critico\n##\nL6 — Non di nessuno");

        Assert.Single(voci);
        Assert.Equal(new[] { 3 }, voci[0].Levels.Select(l => l.Level));
    }

    [Theory]
    [InlineData("##Campione")]     // senza spazio
    [InlineData("### Campione")]   // con più cancelletti
    [InlineData("##   Campione  ")]
    public void Lintestazione_si_riconosce_nelle_grafie_che_una_persona_scrive(string riga)
        => Assert.Equal("Campione", Assert.Single(SubclassText.Leggi(riga + "\nL3 — Critico")).Name);

    /// <summary>L'`id:` esiste solo per la fedeltà dell'export e vale come **prima** riga del blocco.
    /// Altrove è descrizione: senza questo vincolo una frase che comincia per «id:» in mezzo al testo
    /// riscriverebbe l'identificatore della voce.</summary>
    [Fact]
    public void Lid_vale_solo_come_prima_riga_del_blocco()
    {
        var voci = SubclassText.Leggi("## Campione\nid: x/campione\nid: non sono un id\nL3 — Critico");

        Assert.Equal("x/campione", voci[0].Id);
        Assert.Equal("id: non sono un id", voci[0].Description);
    }

    /// <summary>Le righe vuote non chiudono un blocco (solo l'intestazione successiva lo fa) e dentro
    /// la descrizione si <b>conservano</b>: sono gli stacchi di capoverso. Tutte e dodici le
    /// descrizioni di sottoclasse del manuale ne hanno da cinque a sette, e scartarle appiattiva
    /// quattromila caratteri in un blocco unico — nel file esportato e nella scheda, che li rende con
    /// <c>white-space: pre-wrap</c>. Peggio: la stessa sottoclasse si leggeva in due modi diversi a
    /// seconda che il master avesse importato le classi o no.</summary>
    [Fact]
    public void Le_righe_vuote_dentro_la_descrizione_sono_capoversi_e_si_conservano()
    {
        var voci = SubclassText.Leggi("## Campione\nPrimo capoverso.\n\nSecondo capoverso.\nL3 — Critico");

        Assert.Single(voci);
        Assert.Equal("Primo capoverso.\n\nSecondo capoverso.", voci[0].Description);
        Assert.Single(voci[0].Levels);
    }

    /// <summary>Quelle in coda invece si tolgono — la riga vuota fra descrizione e livelli, o dopo
    /// l'ultimo livello — altrimenti il testo crescerebbe di una riga a ogni giro di export.</summary>
    [Fact]
    public void Le_righe_vuote_in_coda_alla_descrizione_non_si_accumulano()
    {
        const string conStacchi = "## Campione\nPrimo capoverso.\n\nSecondo capoverso.\n\nL3 — Critico";

        var canonico = SubclassText.Serializza(SubclassText.Leggi(conStacchi));

        Assert.Equal("Primo capoverso.\n\nSecondo capoverso.",
            Assert.Single(SubclassText.Leggi(canonico)).Description);
        Assert.Equal(canonico, SubclassText.Serializza(SubclassText.Leggi(canonico)));
    }

    /// <summary>Gli slot usano la stessa sintassi della tabella di classe: il formato di
    /// serializzazione dei privilegi resta uno solo.</summary>
    [Fact]
    public void I_livelli_riusano_la_sintassi_della_tabella_di_classe()
    {
        var voci = SubclassText.Leggi("## Mistico\nL3 — Trucchetti · Slot 4/2");

        Assert.Equal(new[] { "Trucchetti" }, voci[0].Levels[0].Features);
        Assert.Equal(new[] { 4, 2 }, voci[0].Levels[0].SpellSlots);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Solo prosa, nessun blocco.")]
    public void Un_testo_che_non_e_un_elenco_da_una_lista_vuota(string? testo)
    {
        Assert.Empty(SubclassText.Leggi(testo));
        Assert.False(SubclassText.SembraElenco(testo));
    }

    [Fact]
    public void Serializza_regge_il_nulla_e_scarta_le_voci_senza_nome()
    {
        Assert.Empty(SubclassText.Serializza(null));
        Assert.Empty(SubclassText.Serializza(new PackageSubclass[] { null!, new() { Name = "  " } }));
    }

    // ---- La guardia che autorizza un re-import a riscrivere il campo ----

    /// <summary>`SoloElenco` è la condizione che permette a un re-import di riscrivere la colonna.
    /// Con `SembraElenco` basterebbe un blocco riconosciuto perché una nota scritta in cima venisse
    /// cancellata insieme al resto, in silenzio: il conteggio delle righe di catalogo non cambia, e
    /// nessuna verifica a vista se ne accorgerebbe.</summary>
    [Fact]
    public void SoloElenco_distingue_lelenco_puro_da_quello_con_una_nota_in_cima()
    {
        const string puro = "## Campione\nL3 — Critico";
        const string conNota = "Da noi si sceglie al 2°.\n## Campione\nL3 — Critico";

        Assert.True(SubclassText.SoloElenco(puro));
        Assert.True(SubclassText.SembraElenco(conNota));
        Assert.False(SubclassText.SoloElenco(conNota));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Solo prosa.")]
    [InlineData("##\nniente nome")]
    public void SoloElenco_e_falso_quando_non_c_e_un_elenco(string? testo)
        => Assert.False(SubclassText.SoloElenco(testo));
}
