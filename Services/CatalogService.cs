using DndCompanion.Models;
using DndCompanion.Models.Packages;
using DndCompanion.Services.Repositories;

namespace DndCompanion.Services;

/// <summary>Un catalogo come lo vede la UI: le righe della campagna (tutte, sempre) più le voci
/// di pacchetto che nessuna di esse già copre.</summary>
public sealed record CatalogView<TRow, TPkg>(
    IReadOnlyList<TRow> DbRows,
    IReadOnlyList<TPkg> PackageEntries);

public interface ICatalogService
{
    /// <summary>Il pacchetto distribuito con l'app, scaricato al primo uso e tenuto in memoria.
    /// Null se assente o illeggibile: l'app funziona lo stesso, con i soli dati di campagna.</summary>
    Task<CatalogPackage?> GetPackageAsync();

    /// <summary>Talenti del solo pacchetto: non hanno tabella, quindi non c'è nulla da unire (§6).
    /// Vuota finché il pacchetto non è stato caricato.</summary>
    IReadOnlyList<PackageFeat> Feats { get; }

    /// <summary>Esito dell'ultima lettura del pacchetto: distingue un pacchetto **malformato**
    /// (valorizzato, con gli errori dentro) da uno **assente** (resta null, perché non c'è stato
    /// nulla da leggere). Null finché una lettura non è andata a buon fine — un fallimento di
    /// rete o un 404 non lo valorizzano.</summary>
    ParseResult? LastParse { get; }

    /// <summary>Background della campagna uniti alle voci di pacchetto non già coperte (§6).</summary>
    Task<CatalogView<Background, PackageBackground>> GetBackgroundsAsync(string campaignId);
}

/// <summary>Unione fra il pacchetto dell'app e i cataloghi di campagna (§6 dello spec).
/// Legge il database SOLO attraverso i repository, mai con From&lt;T&gt; diretto; il pacchetto
/// arriva via HttpClient.
///
/// La composizione sta qui e non nelle pagine: in Fase 2 gli altri quattro cataloghi
/// aggiungeranno il proprio metodo accanto a GetBackgroundsAsync, invece di replicare
/// l'orchestrazione in cinque .razor.</summary>
public class CatalogService : ICatalogService
{
    /// <summary>Percorso relativo alla base dell'app: funziona sia in locale sia sotto il
    /// sottopercorso di GitHub Pages.</summary>
    private const string PackagePath = "data/srd-2024-it.json";

    private readonly HttpClient _http;
    private readonly IBackgroundRepository _backgrounds;

    // Protegge il caricamento da chiamate concorrenti: in Fase 2 più cataloghi potranno chiamare
    // GetPackageAsync in parallelo (es. un Task.WhenAll di più GetXxxAsync su una stessa pagina).
    // Senza questo gate, due chiamate partite prima che la prima completi il download vedrebbero
    // entrambe _loaded=false e scaricherebbero il pacchetto due volte, contraddicendo "scaricato
    // al primo uso". Un SemaphoreSlim, non un lock, perché la sezione protetta contiene degli await.
    private readonly SemaphoreSlim _gate = new(1, 1);
    private CatalogPackage? _package;
    private bool _loaded;

    public CatalogService(HttpClient http, IBackgroundRepository backgrounds)
        => (_http, _backgrounds) = (http, backgrounds);

    public IReadOnlyList<PackageFeat> Feats
        => _package?.Feats ?? (IReadOnlyList<PackageFeat>)Array.Empty<PackageFeat>();

    public ParseResult? LastParse { get; private set; }

    public async Task<CatalogPackage?> GetPackageAsync()
    {
        // Si ricorda solo il successo: un fallimento di rete non deve disattivare il pacchetto
        // per tutta la sessione, perché la rete può tornare.
        if (_loaded) return _package;

        await _gate.WaitAsync();
        try
        {
            // Doppio controllo: mentre questa chiamata attendeva il gate, un'altra potrebbe aver
            // già completato il download. Senza ripeterlo qui, la seconda chiamata scaricherebbe
            // comunque il pacchetto una seconda volta.
            if (_loaded) return _package;

            var response = await _http.GetAsync(PackagePath);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();

            // Si conserva l'esito completo, non solo il pacchetto: senza errori e avvisi, un
            // pacchetto MALFORMATO diventa indistinguibile da uno ASSENTE, e in Fase 3 un errore
            // di traduzione si manifesterebbe come "cataloghi senza voci di manuale", senza un
            // appiglio per capire perché. La schermata di import di Fase 2 li mostrerà.
            LastParse = CatalogPackageParser.Parse(json);
            _package = LastParse.Package;
            _loaded = true;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // Nessun pacchetto: l'app resta utilizzabile con i soli dati di campagna. Oltre agli
            // errori di rete veri e propri, TaskCanceledException copre anche il timeout di
            // HttpClient, che altrimenti sfuggirebbe a questo catch e si propagherebbe al chiamante.
            return null;
        }
        finally
        {
            _gate.Release();
        }

        return _package;
    }

    public async Task<CatalogView<Background, PackageBackground>> GetBackgroundsAsync(string campaignId)
    {
        var dbRows = await _backgrounds.GetBackgroundsForCampaignAsync(campaignId);
        var package = await GetPackageAsync();

        if (package is null)
            return new CatalogView<Background, PackageBackground>(dbRows, Array.Empty<PackageBackground>());

        var nascoste = CatalogMerge.HiddenPackageIds(
            package.Backgrounds, p => p.Id, p => p.Name,
            dbRows, r => r.SourceId, r => r.Name);

        var visibili = package.Backgrounds.Where(b => !nascoste.Contains(b.Id)).ToList();
        return new CatalogView<Background, PackageBackground>(dbRows, visibili);
    }
}
