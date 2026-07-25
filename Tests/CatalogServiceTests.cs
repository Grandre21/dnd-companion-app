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
        private readonly string _payload;
        private readonly HttpStatusCode _status;

        public CountingHandler(string payload, HttpStatusCode status = HttpStatusCode.OK)
            => (_payload, _status) = (payload, status);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(_status)
            {
                Content = new StringContent(_payload)
            });
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
