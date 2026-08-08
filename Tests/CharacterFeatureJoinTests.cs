using DndCompanion.Models;
using DndCompanion.Models.Packages;
using DndCompanion.Services;
using Xunit;

namespace DndCompanion.Tests;

/// <summary>
/// Test di <see cref="CharacterFeatureJoin"/> — il JOIN puro fra i privilegi derivati dal pacchetto
/// SRD (classe, sottoclasse, talenti) e le annotazioni del giocatore.
/// </summary>
public class CharacterFeatureJoinTests
{
    // I livelli e i nomi qui sotto sono dati costruiti a mano (helper puro, nessun accesso al
    // pacchetto): "Senso del pericolo" prende il posto del livello 2 al posto dell'inesistente
    // "Attacco temerario" del piano originale — nel pacchetto SRD il Barbaro non ha una feature con
    // quel nome, mentre "Senso del pericolo" è reale (v. Services/CharacterFeatureRules.cs).
    private static string ProgressioneBarbaro() => ClassProgression.Serializza(new[]
    {
        new PackageClassLevel { Level = 1, Features = new() { "Ira", "Difesa senza armatura" } },
        new PackageClassLevel { Level = 2, Features = new() { "Senso del pericolo" } },
        new PackageClassLevel { Level = 5, Features = new() { "Attacco extra" } },
    });

    private static IReadOnlyList<VistaPrivilegio> Costruisci(
        IEnumerable<CharacterFeature>? annotazioni = null,
        IEnumerable<ClassResource>? contatori = null,
        int livello = 5) =>
        CharacterFeatureJoin.Costruisci(
            ProgressioneBarbaro(), livello, "Barbaro",
            sottoclasse: null, testoTalenti: null, catalogoTalenti: Array.Empty<PackageFeat>(),
            annotazioni, contatori);

    [Fact]
    public void Costruisci_DerivaINomiDallaProgressioneFinoAlLivello()
    {
        var voci = Costruisci(livello: 2);

        Assert.Equal(new[] { "Ira", "Difesa senza armatura", "Senso del pericolo" }.OrderBy(x => x),
                     voci.Select(v => v.Nome).OrderBy(x => x));
        Assert.DoesNotContain(voci, v => v.Nome == "Attacco extra");   // è del livello 5
    }

    [Fact]
    public void Costruisci_SenzaAnnotazioni_LaNotaEVuotaMaLaVoceCE()
    {
        var voce = Assert.Single(Costruisci(livello: 1), v => v.Nome == "Ira");

        Assert.Equal(string.Empty, voce.Nota);
        Assert.Equal("classe", voce.Origine);
        Assert.Equal(1, voce.SbloccatoAlLivello);
    }

    /// <summary>L'aggancio è per nome NORMALIZZATO: «IRA» annota «Ira». Il caso diverso non è un
    /// dettaglio estetico — è ciò che rende il test non vacuo.</summary>
    [Fact]
    public void Costruisci_AgganciaLAnnotazionePerNomeNormalizzato()
    {
        var voci = Costruisci(new[] { new CharacterFeature { Nome = "IRA", Nota = "3/riposo lungo" } });

        Assert.Equal("3/riposo lungo", voci.Single(v => v.Nome == "Ira").Nota);
    }

    /// <summary>Un'annotazione che non corrisponde a nessun privilegio derivato NON si scarta: è
    /// una voce propria. È il meccanismo con cui l'utente aggiunge i tratti di specie, che il
    /// pacchetto SRD non sa separare (spec D7).</summary>
    [Fact]
    public void Costruisci_AnnotazioneOrfana_DiventaVocePropriaENonSiPerde()
    {
        var voci = Costruisci(new[]
        {
            new CharacterFeature { Nome = "Scarica di adrenalina", Nota = "bonus action, +PF" },
        });

        var propria = Assert.Single(voci, v => v.Nome == "Scarica di adrenalina");
        Assert.Equal("propria", propria.Origine);
        Assert.Null(propria.SbloccatoAlLivello);
    }

    /// <summary>Il tag dell'utente vince sulla tabella curata. Il valore scelto è ciò che rende il
    /// test non vacuo: «Ira» in tabella è "bonus", quindi l'annotazione DEVE dire altro — con
    /// "bonus" il test passerebbe anche invertendo la precedenza.</summary>
    [Fact]
    public void Costruisci_IlTagDellUtenteVinceSullaTabellaCurata()
    {
        var voci = Costruisci(new[]
        {
            new CharacterFeature { Nome = "Ira", Azione = "azione" },   // la tabella dice "bonus"
        });

        Assert.Equal("azione", voci.Single(v => v.Nome == "Ira").Azione);
    }

    [Fact]
    public void Costruisci_SenzaTagDellUtente_UsaLaTabellaCurata()
    {
        Assert.Equal("bonus", Costruisci().Single(v => v.Nome == "Ira").Azione);
    }

    /// <summary>Con Risorsa null il contatore si aggancia per nome del privilegio: l'Ira trova da
    /// sola i propri pallini, senza che l'utente debba collegarli a mano.</summary>
    [Fact]
    public void Costruisci_ContatoreAgganciatoPerNomeQuandoRisorsaENull()
    {
        var voci = Costruisci(
            annotazioni: new[] { new CharacterFeature { Nome = "Ira", Risorsa = null } },
            contatori: new[] { new ClassResource { Nome = "Ira", Max = 3, Spesi = 1, Ricarica = "lungo" } });

        var ira = voci.Single(v => v.Nome == "Ira");
        Assert.NotNull(ira.Contatore);
        Assert.Equal(3, ira.Contatore!.Max);
    }

    /// <summary>Un'annotazione con una Risorsa che non corrisponde a NESSUNA ClassResource (per
    /// esempio perché l'utente l'ha cancellata dopo averla scritta) deve comunque portare il nome
    /// grezzo in RisorsaAnnotata: è il fatto che «Ira» non risolva a nessun contatore a rendere il
    /// test non vacuo — senza RisorsaAnnotata, il pannello di modifica precompilerebbe il campo
    /// vuoto e la scritta andrebbe persa al primo salvataggio.</summary>
    [Fact]
    public void Costruisci_RisorsaAnnotataCheNonRisolve_RestaSulValoreGrezzo()
    {
        var voci = Costruisci(
            annotazioni: new[] { new CharacterFeature { Nome = "Ira", Risorsa = "Ira" } },
            contatori: Array.Empty<ClassResource>());

        var ira = voci.Single(v => v.Nome == "Ira");
        Assert.Null(ira.Contatore);
        Assert.Equal("Ira", ira.RisorsaAnnotata);
    }

    [Fact]
    public void Raggruppa_MettePassiviPerUltimoEOmetteIGruppiVuoti()
    {
        var gruppi = CharacterFeatureJoin.Raggruppa(Costruisci());

        Assert.DoesNotContain(gruppi, g => g.Voci.Count == 0);
        Assert.Equal("passivo", gruppi[^1].Tag);      // «Difesa senza armatura» è passivo in tabella
    }

    // -----------------------------------------------------------------------------------
    // Impalcatura (CharacterFeatureRules.ÈImpalcatura) — potata dall'elenco, ma mai se annotata
    // -----------------------------------------------------------------------------------

    private static string ProgressioneConImpalcatura() => ClassProgression.Serializza(new[]
    {
        new PackageClassLevel { Level = 1, Features = new() { "Ira" } },
        new PackageClassLevel { Level = 4, Features = new() { "Incremento punteggio caratteristica" } },
    });

    [Fact]
    public void Costruisci_VoceDiImpalcatura_SenzaAnnotazione_NonEntraNellElenco()
    {
        var voci = CharacterFeatureJoin.Costruisci(
            ProgressioneConImpalcatura(), livello: 4, "Barbaro",
            sottoclasse: null, testoTalenti: null, catalogoTalenti: Array.Empty<PackageFeat>(),
            annotazioni: null, contatori: null);

        Assert.DoesNotContain(voci, v => v.Nome == "Incremento punteggio caratteristica");
        Assert.Contains(voci, v => v.Nome == "Ira");   // il resto della potatura non tocca le capacità vere
    }

    /// <summary>La potatura non deve mai far sparire una nota scritta dall'utente: se ha annotato
    /// «Incremento punteggio caratteristica» (per esempio per segnarsi quale caratteristica ha
    /// alzato), nasconderla cancellerebbe il suo testo senza dirglielo — la voce resta.</summary>
    [Fact]
    public void Costruisci_VoceDiImpalcatura_ConAnnotazioneDellUtente_Resta()
    {
        var voci = CharacterFeatureJoin.Costruisci(
            ProgressioneConImpalcatura(), livello: 4, "Barbaro",
            sottoclasse: null, testoTalenti: null, catalogoTalenti: Array.Empty<PackageFeat>(),
            annotazioni: new[] { new CharacterFeature { Nome = "Incremento punteggio caratteristica", Nota = "+2 Forza" } },
            contatori: null);

        var voce = Assert.Single(voci, v => v.Nome == "Incremento punteggio caratteristica");
        Assert.Equal("+2 Forza", voce.Nota);
    }

    // -----------------------------------------------------------------------------------
    // NotaDiCatalogo — vero solo sul ripiego di CostruisciTalento su talento.Description
    // -----------------------------------------------------------------------------------

    [Fact]
    public void Costruisci_VoceDiClasse_NotaDiCatalogoEFalse()
    {
        var voce = Costruisci(livello: 1).Single(v => v.Nome == "Ira");
        Assert.False(voce.NotaDiCatalogo);
    }

    [Fact]
    public void Costruisci_Talento_SenzaAnnotazione_UsaLaDescriptionDelCatalogo_NotaDiCatalogoETrue()
    {
        var catalogo = new List<PackageFeat>
        {
            new() { Id = "Fortunato", Name = "Fortunato", Description = "Regole di Fortunato" },
        };

        var voci = CharacterFeatureJoin.Costruisci(
            ProgressioneBarbaro(), livello: 5, "Barbaro",
            sottoclasse: null, testoTalenti: "Fortunato", catalogoTalenti: catalogo,
            annotazioni: null, contatori: null);

        var voce = Assert.Single(voci, v => v.Nome == "Fortunato");
        Assert.Equal("Regole di Fortunato", voce.Nota);
        Assert.True(voce.NotaDiCatalogo);
    }

    [Fact]
    public void Costruisci_Talento_ConAnnotazioneDellUtente_NotaDiCatalogoEFalse()
    {
        var catalogo = new List<PackageFeat>
        {
            new() { Id = "Fortunato", Name = "Fortunato", Description = "Regole di Fortunato" },
        };

        var voci = CharacterFeatureJoin.Costruisci(
            ProgressioneBarbaro(), livello: 5, "Barbaro",
            sottoclasse: null, testoTalenti: "Fortunato", catalogoTalenti: catalogo,
            annotazioni: new[] { new CharacterFeature { Nome = "Fortunato", Nota = "riroll 1" } },
            contatori: null);

        var voce = Assert.Single(voci, v => v.Nome == "Fortunato");
        Assert.Equal("riroll 1", voce.Nota);
        Assert.False(voce.NotaDiCatalogo);
    }
}
