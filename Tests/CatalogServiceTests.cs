using System.Net;
using DndCompanion.Models;
using DndCompanion.Services;
using DndCompanion.Services.Repositories;

namespace DndCompanion.Tests;

public class CatalogServiceTests
{
    private sealed class CountingHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }
        private readonly string _payload = string.Empty;
        private readonly HttpStatusCode _status = HttpStatusCode.OK;
        private readonly Exception? _toThrow;
        private readonly TaskCompletionSource<bool>? _hold;

        public CountingHandler(string payload, HttpStatusCode status = HttpStatusCode.OK)
            => (_payload, _status) = (payload, status);

        // Per i test che simulano un fallimento di rete vero (non un 404): l'handler lancia
        // invece di rispondere, come fa HttpClient su un errore di connessione o su un timeout.
        public CountingHandler(Exception toThrow) => _toThrow = toThrow;

        // Per il test di concorrenza: la richiesta resta "in volo" finché il test non sblocca
        // il gate, così due chiamate avviate senza await fra loro si sovrappongono davvero,
        // invece di completare l'una prima che l'altra parta.
        public CountingHandler(string payload, TaskCompletionSource<bool> hold)
            => (_payload, _hold) = (payload, hold);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            if (_toThrow is not null) throw _toThrow;
            if (_hold is not null) await _hold.Task;
            return new HttpResponseMessage(_status) { Content = new StringContent(_payload) };
        }
    }

    // Repository finto: restituisce le righe che gli si passano, senza toccare la rete.
    private sealed class FakeBackgroundRepository : IBackgroundRepository
    {
        private readonly List<Background> _rows;
        public FakeBackgroundRepository(params Background[] rows) => _rows = rows.ToList();

        public Task<List<Background>> GetBackgroundsForCampaignAsync(string campaignId)
            => Task.FromResult(_rows);
        public Task<Background?> CreateBackgroundAsync(Background b) => Task.FromResult<Background?>(b);
        public Task<Background?> UpdateBackgroundAsync(Background b) => Task.FromResult<Background?>(b);
        public Task DeleteBackgroundAsync(string id) => Task.CompletedTask;
        public Task<List<Background>> CreateManyAsync(List<Background> rows) => Task.FromResult(rows);
        public Task DeleteByIdsAsync(List<string> ids) => Task.CompletedTask;
    }

    private const string Package = """
    {
      "schemaVersion": 1, "id": "srd-2024-it", "name": "SRD", "edition": "2024",
      "language": "it", "version": "1.0.0",
      "feats": [ { "id": "srd-2024-it/artigiano-talento", "name": "Artefice", "description": "…" } ],
      "backgrounds": [
        { "id": "srd-2024-it/artigiano", "name": "Artigiano" },
        { "id": "srd-2024-it/soldato", "name": "Soldato" }
      ]
    }
    """;

    private static CatalogService Service(
        CountingHandler handler, params Background[] dbRows)
        => new(new HttpClient(handler) { BaseAddress = new Uri("https://esempio.test/") },
               new FakeBackgroundRepository(dbRows));

    [Fact]
    public async Task GetPackageAsync_ScaricaUnaVoltaSola()
    {
        var handler = new CountingHandler(Package);
        var service = Service(handler);

        await service.GetPackageAsync();
        await service.GetPackageAsync();

        Assert.Equal(1, handler.Calls);
    }

    // Unica rete automatica contro un futuro refactor che tolga il gate e rimetta un controllo
    // nudo su _loaded: senza il SemaphoreSlim, due chiamate avviate senza await fra loro
    // vedrebbero entrambe _loaded=false e scaricherebbero il pacchetto due volte.
    [Fact]
    public async Task GetPackageAsync_ChiamateConcorrenti_ScaricaUnaVoltaSola()
    {
        var hold = new TaskCompletionSource<bool>();
        var handler = new CountingHandler(Package, hold);
        var service = Service(handler);

        // Avviate senza await fra loro: se il gate non ci fosse, entrambe partirebbero prima
        // che la prima completi la propria richiesta HTTP.
        var t1 = service.GetPackageAsync();
        var t2 = service.GetPackageAsync();

        hold.SetResult(true); // sblocca la richiesta rimasta "in volo"
        await Task.WhenAll(t1, t2);

        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task GetPackageAsync_PacchettoAssente_RestituisceNullSenzaLanciare()
    {
        var service = Service(new CountingHandler("", HttpStatusCode.NotFound));

        Assert.Null(await service.GetPackageAsync());
    }

    // Un fallimento non deve disattivare il pacchetto per l'intera sessione: la rete può tornare.
    [Fact]
    public async Task GetPackageAsync_DopoUnFallimento_Riprova()
    {
        var handler = new CountingHandler("", HttpStatusCode.NotFound);
        var service = Service(handler);

        await service.GetPackageAsync();
        await service.GetPackageAsync();

        Assert.Equal(2, handler.Calls);
    }

    // Il catch di GetPackageAsync copre HttpRequestException, ma CountingHandler prima d'ora
    // rispondeva sempre con uno status code e non lanciava mai: senza questo test, un domani
    // qualcuno potrebbe spostare l'assegnazione di LastParse prima della chiamata di rete, o
    // rendere CatalogPackageParser.Parse non più puro, e la suite resterebbe verde comunque.
    [Fact]
    public async Task GetPackageAsync_ErroreDiRete_RestituisceNullSenzaValorizzareLastParseERiprova()
    {
        var handler = new CountingHandler(new HttpRequestException("connessione rifiutata"));
        var service = Service(handler);

        Assert.Null(await service.GetPackageAsync());
        Assert.Null(service.LastParse);

        // La rete può tornare: la chiamata successiva riprova, non resta bloccata sul fallimento.
        await service.GetPackageAsync();
        Assert.Equal(2, handler.Calls);
    }

    // Stesso principio del test precedente, ma per il ramo TaskCanceledException (il timeout di
    // HttpClient), aggiunto al catch insieme a HttpRequestException.
    [Fact]
    public async Task GetPackageAsync_Timeout_RestituisceNullSenzaValorizzareLastParseERiprova()
    {
        var handler = new CountingHandler(new TaskCanceledException("timeout"));
        var service = Service(handler);

        Assert.Null(await service.GetPackageAsync());
        Assert.Null(service.LastParse);

        await service.GetPackageAsync();
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task Feats_PrimaDelCaricamento_ListaVuota()
    {
        var service = Service(new CountingHandler(Package));

        Assert.Empty(service.Feats);

        await service.GetPackageAsync();

        Assert.Single(service.Feats);
    }

    [Fact]
    public async Task GetBackgroundsAsync_SenzaRigheDiDatabase_MostraTutteLeVociDiPacchetto()
    {
        var vista = await Service(new CountingHandler(Package)).GetBackgroundsAsync("campagna-1");

        Assert.Empty(vista.DbRows);
        Assert.Equal(2, vista.PackageEntries.Count);
    }

    [Fact]
    public async Task GetBackgroundsAsync_RigaOmonimaSenzaProvenienza_OscuraLaVoceDiPacchetto()
    {
        var riga = new Background { Id = "uuid-1", Name = "Artigiano", CampaignId = "campagna-1" };

        var vista = await Service(new CountingHandler(Package), riga).GetBackgroundsAsync("campagna-1");

        Assert.Single(vista.DbRows);
        Assert.Single(vista.PackageEntries);
        Assert.Equal("srd-2024-it/soldato", vista.PackageEntries[0].Id);
    }

    // La ragion d'essere di LastParse: un pacchetto rotto non deve assomigliare a un pacchetto
    // che non c'è. Senza questo test nulla fallirebbe se l'assegnazione sparisse.
    [Fact]
    public async Task LastParse_PacchettoMalformato_ConservaGliErroriENonRiprova()
    {
        var rotto = """{ "schemaVersion": 99, "id": "srd-2024-it", "name": "SRD" }""";
        var handler = new CountingHandler(rotto);
        var service = Service(handler);

        Assert.Null(await service.GetPackageAsync());
        Assert.NotNull(service.LastParse);
        Assert.NotEmpty(service.LastParse!.Errors);

        // Un file rotto non si aggiusta da solo: inutile riscaricarlo a ogni richiesta.
        await service.GetPackageAsync();
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task LastParse_PacchettoAssente_RestaNull()
    {
        var service = Service(new CountingHandler("", HttpStatusCode.NotFound));

        await service.GetPackageAsync();

        Assert.Null(service.LastParse);
    }

    [Fact]
    public async Task GetBackgroundsAsync_PacchettoAssente_RestituisceSoloLeRigheDiCampagna()
    {
        var riga = new Background { Id = "uuid-1", Name = "Mio background", CampaignId = "campagna-1" };
        var service = Service(new CountingHandler("", HttpStatusCode.NotFound), riga);

        var vista = await service.GetBackgroundsAsync("campagna-1");

        Assert.Single(vista.DbRows);
        Assert.Empty(vista.PackageEntries);
    }
}
