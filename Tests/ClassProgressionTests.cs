using DndCompanion.Models;
using DndCompanion.Models.Packages;
using DndCompanion.Services;
using Xunit;

namespace DndCompanion.Tests;

/// <summary>
/// Test della progressione di classe scritta dentro <c>CharacterClass.Features</c>.
///
/// Il formato è l'unico ponte fra i 20 livelli del pacchetto e la scheda del personaggio: se si
/// rompe, la scheda torna a non mostrare privilegi — esattamente il difetto che questo lavoro
/// chiude — senza che build o import segnalino alcunché, perché il conteggio delle righe di
/// catalogo resta identico.
/// </summary>
public class ClassProgressionTests
{
    private static PackageClassLevel Livello(int n, string[] privilegi, int[]? slot = null) => new()
    {
        Level = n,
        Features = privilegi.ToList(),
        SpellSlots = (slot ?? Array.Empty<int>()).ToList(),
    };

    [Fact]
    public void Serializza_scrive_una_riga_per_livello()
    {
        var testo = ClassProgression.Serializza(new[]
        {
            Livello(1, new[] { "Ira", "Difesa senza armatura" }),
            Livello(2, new[] { "Senso del pericolo" }),
        });

        Assert.Equal("L1 — Ira, Difesa senza armatura\nL2 — Senso del pericolo", testo);
    }

    [Fact]
    public void Serializza_ordina_per_livello_anche_se_la_sorgente_e_disordinata()
    {
        var testo = ClassProgression.Serializza(new[]
        {
            Livello(3, new[] { "Terzo" }),
            Livello(1, new[] { "Primo" }),
        });

        Assert.StartsWith("L1 — Primo", testo);
    }

    [Fact]
    public void Serializza_omette_i_livelli_senza_nulla_da_dire()
    {
        var testo = ClassProgression.Serializza(new[]
        {
            Livello(1, new[] { "Ira" }),
            Livello(2, Array.Empty<string>()),
            Livello(3, new[] { "Conoscenza primordiale" }),
        });

        Assert.DoesNotContain("L2", testo);
        Assert.Equal(2, ClassProgression.Leggi(testo).Count);
    }

    [Fact]
    public void Gli_slot_perdono_gli_zeri_finali_ma_non_quelli_interni()
    {
        var testo = ClassProgression.Serializza(new[]
        {
            Livello(3, new[] { "Sottoclasse" }, new[] { 4, 2, 0, 0, 0, 0, 0, 0, 0 }),
        });
        Assert.Contains("Slot 4/2", testo);

        var conBuco = ClassProgression.Serializza(new[]
        {
            Livello(5, new[] { "X" }, new[] { 4, 0, 2, 0, 0, 0, 0, 0, 0 }),
        });
        Assert.Contains("Slot 4/0/2", conBuco);
    }

    [Fact]
    public void Una_classe_senza_incantesimi_non_scrive_slot()
    {
        var testo = ClassProgression.Serializza(new[]
        {
            Livello(1, new[] { "Ira" }, new[] { 0, 0, 0, 0, 0, 0, 0, 0, 0 }),
        });

        Assert.DoesNotContain("Slot", testo);
        Assert.Empty(ClassProgression.Leggi(testo)[0].Slot);
    }

    /// <summary>Il caso che rompe l'implementazione ingenua: «Movimento senza armatura (+4,5 m)»
    /// è un privilegio reale del Monaco, e uno split sul carattere ',' lo spezzerebbe in due voci
    /// senza senso — in silenzio, perché il testo resta plausibile.</summary>
    [Fact]
    public void La_virgola_decimale_dentro_un_privilegio_non_lo_spezza()
    {
        var testo = ClassProgression.Serializza(new[]
        {
            Livello(6, new[] { "Movimento senza armatura (+4,5 m)", "Colpo potenziato" }),
        });

        var righe = ClassProgression.Leggi(testo);
        Assert.Equal(new[] { "Movimento senza armatura (+4,5 m)", "Colpo potenziato" }, righe[0].Privilegi);
    }

    [Fact]
    public void Leggi_accetta_i_trattini_alternativi()
    {
        var righe = ClassProgression.Leggi("L1 - Ira\nL2 – Senso del pericolo");

        Assert.Equal(2, righe.Count);
        Assert.Equal("Ira", righe[0].Privilegi[0]);
        Assert.Equal("Senso del pericolo", righe[1].Privilegi[0]);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("Appunti sparsi sulla classe, scritti a mano.")]
    [InlineData("Livello 3: sottoclasse")]
    [InlineData("L — senza numero")]
    [InlineData("Lx — numero non valido")]
    public void Un_testo_che_non_e_una_tabella_produce_una_lista_vuota(string? testo)
    {
        Assert.Empty(ClassProgression.Leggi(testo));
        Assert.False(ClassProgression.SembraProgressione(testo));
    }

    [Fact]
    public void Le_righe_estranee_non_impediscono_di_leggere_quelle_valide()
    {
        var righe = ClassProgression.Leggi("Nota mia\nL1 — Ira\nAltra nota");

        Assert.Single(righe);
        Assert.Equal(1, righe[0].Livello);
    }

    [Fact]
    public void FinoAl_taglia_i_livelli_non_ancora_raggiunti()
    {
        var testo = ClassProgression.Serializza(new[]
        {
            Livello(1, new[] { "A" }),
            Livello(3, new[] { "B" }),
            Livello(5, new[] { "C" }),
        });

        var raggiunti = ClassProgression.FinoAl(testo, 3);

        Assert.Equal(new[] { 1, 3 }, raggiunti.Select(r => r.Livello));
    }

    [Fact]
    public void FinoAl_a_livello_zero_non_restituisce_nulla()
        => Assert.Empty(ClassProgression.FinoAl("L1 — Ira", 0));

    /// <summary>Sette classi su dodici hanno livelli con i soli slot: un Mago al 7° non guadagna
    /// privilegi. Elencarli darebbe un «L7» con il vuoto accanto — l'aria di dato mancante che
    /// questo lavoro serve a togliere.</summary>
    [Fact]
    public void PrivilegiFinoAl_salta_i_livelli_di_soli_slot()
    {
        var testo = ClassProgression.Serializza(new[]
        {
            Livello(6, new[] { "Privilegio di tradizione arcana" }, new[] { 4, 3, 3 }),
            Livello(7, Array.Empty<string>(), new[] { 4, 3, 3, 1 }),
        });

        var raggiunti = ClassProgression.PrivilegiFinoAl(testo, 7);

        Assert.Equal(new[] { 6 }, raggiunti.Select(r => r.Livello));
        // Gli slot del livello 7 restano leggibili: li porta SlotFinoAl, non l'elenco.
        Assert.Equal(new[] { 4, 3, 3, 1 }, ClassProgression.SlotFinoAl(testo, 7));
    }

    [Fact]
    public void ProssimoDopo_salta_i_livelli_di_soli_slot()
    {
        var testo = ClassProgression.Serializza(new[]
        {
            Livello(6, new[] { "Qualcosa" }),
            Livello(7, Array.Empty<string>(), new[] { 4, 3, 3, 1 }),
            Livello(8, new[] { "Incremento punteggio caratteristica" }),
        });

        Assert.Equal(8, ClassProgression.ProssimoDopo(testo, 6)!.Livello);
    }

    [Fact]
    public void ProssimoDopo_al_ventesimo_livello_non_promette_nulla()
        => Assert.Null(ClassProgression.ProssimoDopo("L20 — Ultimo privilegio", 20));

    [Theory]
    [InlineData("L1 — Ira\nL3 — Sottoclasse del Barbaro", true)]
    [InlineData("L1 — Ira\n\nL3 — Sottoclasse del Barbaro", true)]   // le righe vuote non contano
    [InlineData("L1 — Ira\nNota: da noi il 3° arriva a fine capitolo", false)]
    [InlineData("Appunti a mano", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void SoloProgressione_distingue_la_tabella_pura_dal_contenuto_misto(string? testo, bool atteso)
        => Assert.Equal(atteso, ClassProgression.SoloProgressione(testo));

    /// <summary>Le tabelle dichiarano gli slot solo quando cambiano: al livello 4 valgono ancora
    /// quelli scritti al 3, e prendere la riga esatta darebbe "nessuno slot".</summary>
    [Fact]
    public void SlotFinoAl_usa_l_ultima_riga_che_li_dichiara()
    {
        var testo = ClassProgression.Serializza(new[]
        {
            Livello(3, new[] { "Sottoclasse" }, new[] { 4, 2 }),
            Livello(4, new[] { "Incremento punteggio caratteristica" }),
        });

        Assert.Equal(new[] { 4, 2 }, ClassProgression.SlotFinoAl(testo, 4));
        Assert.Empty(ClassProgression.SlotFinoAl(testo, 2));
    }

    [Theory]
    [InlineData("Sottoclasse del Barbaro", true)]
    [InlineData("Sottoclasse del ranger", true)]     // il pacchetto alterna maiuscola e minuscola
    [InlineData("Privilegio di sottoclasse", true)]
    // Le tre classi che non usano la parola generica: senza queste, un Mago non vedrebbe segnalata
    // la scelta che al 3° livello definisce il personaggio.
    [InlineData("Tradizione arcana", true)]
    [InlineData("Privilegio di tradizione arcana", true)]
    [InlineData("Tradizione monastica", true)]
    [InlineData("Giuramento sacro", true)]
    [InlineData("Ira", false)]
    [InlineData("Attacco extra", false)]
    [InlineData(null, false)]
    public void RiguardaSottoclasse_riconosce_le_voci_di_sottoclasse(string? privilegio, bool atteso)
        => Assert.Equal(atteso, ClassProgression.RiguardaSottoclasse(privilegio));

    // ---- Risoluzione della classe: campagna prima del pacchetto ----

    private static CharacterClass Riga(string id, string nome, string? features, string? sourceId = null)
        => new() { Id = id, Name = nome, Features = features ?? string.Empty, SourceId = sourceId };

    private static PackageClass VoceDiPacchetto(string nome) => new()
    {
        Id = "srd-2024-it/classe/" + nome.ToLowerInvariant(),
        Name = nome,
        Levels = new List<PackageClassLevel>
        {
            new() { Level = 1, Features = new List<string> { "Dal pacchetto" } },
        },
    };

    [Fact]
    public void Risolvi_preferisce_la_riga_di_campagna()
    {
        var testo = ClassProgression.Risolvi(
            new[] { Riga("u1", "Barbaro", "L1 — Dalla campagna") },
            new[] { VoceDiPacchetto("Barbaro") },
            "Barbaro");

        Assert.Contains("Dalla campagna", testo);
    }

    /// <summary>Il percorso più frequente: chi non ha mai importato le classi non ha righe in
    /// campagna, e tutto arriva dal manuale.</summary>
    [Fact]
    public void Risolvi_senza_righe_di_campagna_legge_il_pacchetto()
    {
        var pacchetto = new[] { VoceDiPacchetto("Barbaro") };

        Assert.Contains("Dal pacchetto",
            ClassProgression.Risolvi(Array.Empty<CharacterClass>(), pacchetto, "Barbaro"));
        Assert.Contains("Dal pacchetto",
            ClassProgression.Risolvi(null, pacchetto, "Barbaro"));
    }

    /// <summary>Chi ha importato le classi prima che l'import portasse i livelli ha righe senza
    /// tabella ma con la provenienza del manuale: sono solo vecchie, e il pacchetto ne è la
    /// versione aggiornata.</summary>
    [Fact]
    public void Risolvi_ripiega_sul_pacchetto_per_una_riga_importata_senza_tabella()
    {
        var testo = ClassProgression.Risolvi(
            new[] { Riga("u1", "Barbaro", string.Empty, sourceId: "srd-2024-it/classe/barbaro") },
            new[] { VoceDiPacchetto("Barbaro") },
            "Barbaro");

        Assert.Contains("Dal pacchetto", testo);
    }

    /// <summary>Il caso opposto, e il motivo per cui il ripiego non può essere incondizionato: una
    /// classe scritta dal tavolo oscura la voce di manuale nella pagina Classi, e la scheda non può
    /// mostrare i privilegi SRD di una classe deliberatamente sostituita.</summary>
    [Fact]
    public void Risolvi_non_ripiega_sul_pacchetto_per_una_classe_del_tavolo()
    {
        var testo = ClassProgression.Risolvi(
            new[] { Riga("u1", "Barbaro", "Il nostro barbaro, regole nostre.") },
            new[] { VoceDiPacchetto("Barbaro") },
            "Barbaro");

        Assert.Null(testo);
    }

    /// <summary>Se una delle omonime la tabella ce l'ha, vince lei: il ripiego non entra in gioco
    /// e la riga senza tabella non la nasconde.</summary>
    [Fact]
    public void Risolvi_preferisce_l_omonima_che_ha_la_tabella()
    {
        var testo = ClassProgression.Risolvi(
            new[]
            {
                Riga("u1", "Barbaro", "Nessuna tabella qui"),
                Riga("u2", "Barbaro", "L1 — Dalla campagna"),
            },
            new[] { VoceDiPacchetto("Barbaro") },
            "Barbaro");

        Assert.Contains("Dalla campagna", testo);
    }

    /// <summary>Due omonime — l'importata più una copia da "duplica e modifica" — devono dare
    /// sempre lo stesso esito: l'ordine di lettura dal database non è definito.</summary>
    [Fact]
    public void Risolvi_e_deterministico_fra_omonime()
    {
        var importata = Riga("u2", "Barbaro", "L1 — Importata", sourceId: "srd-2024-it/classe/barbaro");
        var copia = Riga("u1", "Barbaro", "L1 — Copia del tavolo");

        var unOrdine = ClassProgression.Risolvi(new[] { importata, copia }, null, "Barbaro");
        var altroOrdine = ClassProgression.Risolvi(new[] { copia, importata }, null, "Barbaro");

        Assert.Equal(unOrdine, altroOrdine);
        // Representative privilegia la riga senza SourceId: è quella "del tavolo".
        Assert.Contains("Copia del tavolo", unOrdine);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("Classe inventata")]
    public void Risolvi_senza_corrispondenza_restituisce_null(string? nome)
        => Assert.Null(ClassProgression.Risolvi(
            new[] { Riga("u1", "Barbaro", "L1 — Ira") }, new[] { VoceDiPacchetto("Barbaro") }, nome));

    [Fact]
    public void Risolvi_regge_le_collezioni_nulle()
        => Assert.Null(ClassProgression.Risolvi(null, null, "Barbaro"));

    /// <summary>Il formato di scambio lascia passare i null dentro <c>levels</c> (il parser
    /// normalizza le liste, non i loro elementi): un file di terze parti non deve far fallire
    /// l'intero import con una NullReferenceException.</summary>
    [Fact]
    public void Serializza_ignora_i_livelli_nulli()
    {
        var livelli = new List<PackageClassLevel?> { null, Livello(1, new[] { "Ira" }), null };

        var testo = ClassProgression.Serializza(livelli!);

        Assert.Equal("L1 — Ira", testo);
    }

    [Fact]
    public void Il_giro_completo_conserva_privilegi_e_slot()
    {
        var origine = new[]
        {
            Livello(1, new[] { "Lanciare incantesimi", "Ordine divino" }, new[] { 2, 0, 0, 0, 0, 0, 0, 0, 0 }),
            Livello(3, new[] { "Sottoclasse del Chierico" }, new[] { 4, 2, 0, 0, 0, 0, 0, 0, 0 }),
        };

        var righe = ClassProgression.Leggi(ClassProgression.Serializza(origine));

        Assert.Equal(2, righe.Count);
        Assert.Equal(new[] { "Lanciare incantesimi", "Ordine divino" }, righe[0].Privilegi);
        Assert.Equal(new[] { 2 }, righe[0].Slot);
        Assert.Equal(new[] { 4, 2 }, righe[1].Slot);
    }
}
