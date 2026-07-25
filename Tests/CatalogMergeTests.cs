using DndCompanion.Services;

namespace DndCompanion.Tests;

public class CatalogMergeTests
{
    // Riga di database e voce di pacchetto, ridotte a ciò che serve al merge.
    private sealed record Row(string Id, string? SourceId, string Name);
    private sealed record Pkg(string Id, string Name);

    private static HashSet<string> Nascoste(IEnumerable<Pkg> pacchetto, IEnumerable<Row> db)
        => CatalogMerge.HiddenPackageIds(
            pacchetto, p => p.Id, p => p.Name,
            db, r => r.SourceId, r => r.Name);

    [Fact]
    public void HiddenPackageIds_RigaConLoStessoSourceId_OscuraLaVoceDiPacchetto()
    {
        var db = new[] { new Row("uuid-1", "srd-2024-it/elfo", "Elfo") };
        var pacchetto = new[] { new Pkg("srd-2024-it/elfo", "Elfo"), new Pkg("srd-2024-it/nano", "Nano") };

        var nascoste = Nascoste(pacchetto, db);

        Assert.Contains("srd-2024-it/elfo", nascoste);
        Assert.DoesNotContain("srd-2024-it/nano", nascoste);
    }

    [Fact]
    public void HiddenPackageIds_NessunaCorrispondenza_NonNascondeNulla()
    {
        var db = new[] { new Row("uuid-1", null, "Mezzorco") };
        var pacchetto = new[] { new Pkg("srd-2024-it/elfo", "Elfo") };

        Assert.Empty(Nascoste(pacchetto, db));
    }

    // Una riga CON provenienza copre anche una voce di pacchetto omonima ma con un ID diverso dal
    // proprio SourceId: il confronto per nome non vale solo per le righe senza provenienza.
    [Fact]
    public void HiddenPackageIds_RigaConProvenienzaDiversaMaOmonima_OscuraLAltraVoce()
    {
        var db = new[] { new Row("uuid-1", "pacchetto-a/elfo", "Elfo") };
        var pacchetto = new[] { new Pkg("pacchetto-b/elfo", "Elfo") };

        Assert.Contains("pacchetto-b/elfo", Nascoste(pacchetto, db));
    }

    // Il pacchetto può correggere una traduzione: l'id resta lo stesso, il nome cambia. La riga che
    // l'utente ha già in campagna mantiene il vecchio nome ma lo stesso SourceId: solo il confronto
    // per id la riconosce. Nessuno degli altri test lo dimostra, perché ovunque altrove id e nome
    // normalizzato concordano o discordano insieme — qui devono divergere apposta.
    [Fact]
    public void HiddenPackageIds_RigaConLoStessoSourceIdENomeDiverso_OscuraLaVoceDiPacchettoPerId()
    {
        var db = new[] { new Row("uuid-1", "srd-2024-it/elfo", "Elfo delle Foreste") };
        var pacchetto = new[] { new Pkg("srd-2024-it/elfo", "Elfo") };

        Assert.Contains("srd-2024-it/elfo", Nascoste(pacchetto, db));
    }

    // Il caso che la pagina produce davvero: "duplica e modifica" crea una riga SENZA provenienza
    // e con lo stesso nome. Senza il confronto per nome l'utente vedrebbe due "Artigiano".
    [Fact]
    public void HiddenPackageIds_RigaSenzaProvenienzaMaOmonima_OscuraLaVoceDiPacchetto()
    {
        var db = new[] { new Row("uuid-1", null, "Artigiano") };
        var pacchetto = new[] { new Pkg("srd-2024-it/artigiano", "Artigiano") };

        Assert.Contains("srd-2024-it/artigiano", Nascoste(pacchetto, db));
    }

    // Le righe di database sono dati dell'utente: il merge non ne nasconde mai nessuna (§4.3).
    // Il rappresentante serve solo a decidere chi aggiorna un import e chi riusa la materializzazione.
    [Fact]
    public void Representative_PreferisceLaRigaSenzaSourceId()
    {
        var righe = new[]
        {
            new Row("uuid-a", "srd-2024-it/elfo", "Elfo"),
            new Row("uuid-b", null, "Elfo"),
        };

        var scelta = CatalogMerge.Representative(righe, r => r.SourceId, r => r.Id);

        Assert.Equal("uuid-b", scelta!.Id);
    }

    [Fact]
    public void Representative_APariMerito_PrendeLIdOrdinalmenteMinore()
    {
        var righe = new[]
        {
            new Row("uuid-b", null, "Elfo"),
            new Row("uuid-a", null, "Elfo"),
        };

        var scelta = CatalogMerge.Representative(righe, r => r.SourceId, r => r.Id);

        Assert.Equal("uuid-a", scelta!.Id);
    }

    [Fact]
    public void Representative_ListaVuota_RestituisceNull()
        => Assert.Null(CatalogMerge.Representative(
            Array.Empty<Row>(), r => r.SourceId, r => r.Id));

    [Fact]
    public void HiddenPackageIds_ConfrontoPerNomeIgnoraAccentiEMaiuscole()
    {
        var db = new[] { new Row("uuid-1", null, "  INVISIBILITA  ") };
        var pacchetto = new[] { new Pkg("srd-2024-it/invisibilita", "Invisibilità") };

        // Il confronto passa per il nome normalizzato, ma il risultato contiene l'ID della voce:
        // è quello che la pagina usa per filtrare.
        Assert.Contains("srd-2024-it/invisibilita", Nascoste(pacchetto, db));
    }
}
