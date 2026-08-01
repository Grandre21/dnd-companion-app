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

    /// <summary>Specie della campagna unite alle voci di pacchetto non già coperte (§6).</summary>
    Task<CatalogView<Race, PackageSpecies>> GetRacesAsync(string campaignId);

    /// <summary>Classi della campagna unite alle voci di pacchetto non già coperte (§6).</summary>
    Task<CatalogView<CharacterClass, PackageClass>> GetClassesAsync(string campaignId);

    /// <summary>Incantesimi della campagna uniti alle voci di pacchetto non già coperte (§6).</summary>
    Task<CatalogView<Spell, PackageSpell>> GetSpellsAsync(string campaignId);

    /// <summary>Mostri della campagna uniti alle voci di pacchetto non già coperte (§6).</summary>
    Task<CatalogView<Monster, PackageMonster>> GetMonstersAsync(string campaignId);

    /// <summary>Le cinque liste come stanno nel database, senza unione né oscuramenti: import ed
    /// export ragionano su ciò che esiste davvero, non su ciò che la UI mostra.</summary>
    Task<CampaignCatalogs> GetCampaignCatalogsAsync(string campaignId);
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
    private readonly IRaceRepository _races;
    private readonly IClassRepository _classes;
    private readonly ISpellRepository _spells;
    private readonly IMonsterRepository _monsters;

    // Protegge il caricamento da chiamate concorrenti: in Fase 2 più cataloghi potranno chiamare
    // GetPackageAsync in parallelo (es. un Task.WhenAll di più GetXxxAsync su una stessa pagina).
    // Senza questo gate, due chiamate partite prima che la prima completi il download vedrebbero
    // entrambe _loaded=false e scaricherebbero il pacchetto due volte, contraddicendo "scaricato
    // al primo uso". Un SemaphoreSlim, non un lock, perché la sezione protetta contiene degli await.
    private readonly SemaphoreSlim _gate = new(1, 1);
    private CatalogPackage? _package;
    private bool _loaded;

    public CatalogService(
        HttpClient http,
        IBackgroundRepository backgrounds,
        IRaceRepository races,
        IClassRepository classes,
        ISpellRepository spells,
        IMonsterRepository monsters)
    {
        _http = http;
        _backgrounds = backgrounds;
        _races = races;
        _classes = classes;
        _spells = spells;
        _monsters = monsters;
    }

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
            LastParse = CatalogPackageParser.Parse(json, èIlManualeDellApp: true);
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
        => await MergeAsync(
            await _backgrounds.GetBackgroundsForCampaignAsync(campaignId),
            p => p.Backgrounds, p => p.Id, p => p.Name,
            r => r.SourceId, r => r.Name);

    public async Task<CatalogView<Race, PackageSpecies>> GetRacesAsync(string campaignId)
        => await MergeAsync(
            await _races.GetRacesForCampaignAsync(campaignId),
            p => p.Species, p => p.Id, p => p.Name,
            r => r.SourceId, r => r.Name);

    public async Task<CatalogView<CharacterClass, PackageClass>> GetClassesAsync(string campaignId)
        => await MergeAsync(
            await _classes.GetClassesForCampaignAsync(campaignId),
            p => p.Classes, p => p.Id, p => p.Name,
            r => r.SourceId, r => r.Name);

    public async Task<CatalogView<Spell, PackageSpell>> GetSpellsAsync(string campaignId)
        => await MergeAsync(
            await _spells.GetSpellsForCampaignAsync(campaignId),
            p => p.Spells, p => p.Id, p => p.Name,
            r => r.SourceId, r => r.Name);

    public async Task<CatalogView<Monster, PackageMonster>> GetMonstersAsync(string campaignId)
        => await MergeAsync(
            await _monsters.GetMonstersForCampaignAsync(campaignId),
            p => p.Monsters, p => p.Id, p => p.Name,
            r => r.SourceId, r => r.Name);

    public async Task<CampaignCatalogs> GetCampaignCatalogsAsync(string campaignId)
    {
        // In parallelo: sono cinque letture indipendenti, e la schermata di import le attende tutte.
        var razze = _races.GetRacesForCampaignAsync(campaignId);
        var classi = _classes.GetClassesForCampaignAsync(campaignId);
        var incantesimi = _spells.GetSpellsForCampaignAsync(campaignId);
        var mostri = _monsters.GetMonstersForCampaignAsync(campaignId);
        var background = _backgrounds.GetBackgroundsForCampaignAsync(campaignId);

        await Task.WhenAll(razze, classi, incantesimi, mostri, background);

        return new CampaignCatalogs
        {
            Races = razze.Result,
            Classes = classi.Result,
            Spells = incantesimi.Result,
            Monsters = mostri.Result,
            Backgrounds = background.Result,
        };
    }

    // L'unione è la stessa per tutti e cinque i cataloghi: le righe di database si mostrano sempre
    // tutte, le voci di pacchetto solo se nessuna riga già le copre (§4.3).
    private async Task<CatalogView<TRow, TPkg>> MergeAsync<TRow, TPkg>(
        List<TRow> dbRows,
        Func<CatalogPackage, List<TPkg>> sectionOf,
        Func<TPkg, string> packageIdOf,
        Func<TPkg, string> packageNameOf,
        Func<TRow, string?> sourceIdOf,
        Func<TRow, string> nameOf)
    {
        var package = await GetPackageAsync();
        if (package is null)
            return new CatalogView<TRow, TPkg>(dbRows, Array.Empty<TPkg>());

        var voci = sectionOf(package);
        var nascoste = CatalogMerge.HiddenPackageIds(
            voci, packageIdOf, packageNameOf, dbRows, sourceIdOf, nameOf);

        var visibili = voci.Where(v => !nascoste.Contains(packageIdOf(v))).ToList();
        return new CatalogView<TRow, TPkg>(dbRows, visibili);
    }
}
