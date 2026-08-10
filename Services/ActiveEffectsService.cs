using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.JSInterop;

namespace DndCompanion.Services;

/// <summary>Contesto di serializzazione generato a compile-time: il progetto pubblica con
/// TrimMode=full, dove gli overload a reflection di System.Text.Json producono warning. Stesso
/// pattern di <see cref="CatalogPackageJsonContext"/> in <c>CatalogPackageParser</c>.</summary>
[JsonSerializable(typeof(Dictionary<string, List<string>>))]
internal partial class ActiveEffectsJsonContext : JsonSerializerContext { }

/// <summary>
/// Ricorda quali privilegi sono accesi <b>adesso</b> — «sono in Ira» — per personaggio.
///
/// <b>Perché non su <c>characters</c>:</b> l'Ira si accende e si spegne più volte a sessione. Ogni
/// interruttore sarebbe un <c>Update</c> della riga intera da ~110 colonne, last-write-wins, su rete
/// mobile, e un rifiuto RLS non solleva eccezioni: PostgREST aggiorna zero righe e risponde
/// <c>[]</c>. <b>Perché non in sola memoria:</b> su un telefono al tavolo il sistema operativo
/// chiude la scheda appena l'utente guarda altro; <c>localStorage</c> non costa rete, non costa
/// migrazione, e sopravvive alla riapertura.
///
/// I nomi dei privilegi si confrontano <b>normalizzati</b> (<see cref="CatalogKey.NormalizeName"/>):
/// «Ira» acceso e «ira» spento sarebbe un difetto invisibile. Il nome si conserva però nella forma
/// in cui arriva, per non alterare ciò che l'utente vede.
/// </summary>
public sealed class ActiveEffectsService
{
    private const string StorageKey = "dnd_active_effects";

    private readonly IJSRuntime _js;
    private Task? _initialization;
    private Dictionary<string, List<string>> _state = new();

    public ActiveEffectsService(IJSRuntime js)
    {
        _js = js;
    }

    public event Action? Changed;

    /// <summary>
    /// Carica lo stato attivo da localStorage. Idempotente: i chiamanti concorrenti condividono lo
    /// stesso caricamento, non ne avviano uno a testa.
    /// </summary>
    /// <remarks>
    /// Si tiene il <see cref="Task"/> e non un flag booleano perché il caricamento **può fallire**
    /// (l'accesso a <c>localStorage</c> passa da IJSRuntime). Con il flag alzato prima dell'await,
    /// un fallimento lascerebbe lo stato vuoto **per tutta la sessione** e ogni chiamata successiva
    /// sarebbe un no-op. Scartando il Task fallito, il chiamante successivo riprova. Stessa
    /// struttura di <see cref="CampaignStateService.InitializeAsync"/>.
    /// </remarks>
    public async Task EnsureLoadedAsync()
    {
        var pending = _initialization ??= LoadStateAsync();
        try
        {
            await pending;
        }
        catch
        {
            // Lo scarto avviene qui e non dentro LoadStateAsync: se quel metodo fallisse prima di
            // cedere il controllo, il campo non sarebbe ancora stato assegnato e il Task fallito
            // finirebbe comunque in cache. Il confronto per riferimento evita di buttare via un
            // caricamento nuovo, avviato nel frattempo da un altro chiamante.
            if (ReferenceEquals(_initialization, pending)) _initialization = null;
            throw;
        }
    }

    public bool IsActive(string characterId, string nomePrivilegio) =>
        _state.TryGetValue(characterId, out var attivi) && ContieneNome(attivi, nomePrivilegio);

    public IReadOnlyCollection<string> ActiveFor(string characterId) =>
        _state.TryGetValue(characterId, out var attivi) ? attivi.ToArray() : Array.Empty<string>();

    public async Task ToggleAsync(string characterId, string nomePrivilegio)
    {
        _state.TryGetValue(characterId, out var attiviAttuali);
        var aggiornati = ToggleNome(attiviAttuali, nomePrivilegio);

        if (aggiornati.Count == 0) _state.Remove(characterId);
        else _state[characterId] = aggiornati;

        await SaveStateAsync();
        Changed?.Invoke();
    }

    public async Task ClearAsync(string characterId)
    {
        if (!_state.Remove(characterId)) return;

        await SaveStateAsync();
        Changed?.Invoke();
    }

    private async Task LoadStateAsync()
    {
        var json = await _js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
        _state = ParseOrEmpty(json);
    }

    private async Task SaveStateAsync()
    {
        var json = JsonSerializer.Serialize(_state, ActiveEffectsJsonContext.Default.DictionaryStringListString);
        await _js.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
    }

    /// <summary>localStorage corrotto o JSON illeggibile → si riparte da vuoto, mai
    /// un'eccezione: una scheda che non si apre è peggio di uno stato perso. Stessa filosofia di
    /// <c>ClassResourceRules.Normalizza</c>.</summary>
    private static Dictionary<string, List<string>> ParseOrEmpty(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try
        {
            return JsonSerializer.Deserialize(json, ActiveEffectsJsonContext.Default.DictionaryStringListString)
                   ?? new();
        }
        catch (JsonException)
        {
            return new();
        }
    }

    /// <summary>Vero se <paramref name="nomePrivilegio"/> compare fra <paramref name="attivi"/>,
    /// per nome normalizzato. Pura, senza IJSRuntime: è la parte del servizio effettivamente
    /// testabile in isolamento.</summary>
    internal static bool ContieneNome(IReadOnlyCollection<string>? attivi, string nomePrivilegio)
    {
        if (attivi is null || attivi.Count == 0) return false;
        var chiave = CatalogKey.NormalizeName(nomePrivilegio);
        return attivi.Any(n => CatalogKey.NormalizeName(n) == chiave);
    }

    /// <summary>Aggiunge <paramref name="nomePrivilegio"/> se non è già presente (confronto
    /// normalizzato), altrimenti rimuove la voce già presente — nella forma in cui era stata
    /// salvata, non in quella normalizzata. Pura, senza IJSRuntime.</summary>
    internal static List<string> ToggleNome(IReadOnlyCollection<string>? attivi, string nomePrivilegio)
    {
        var risultato = attivi is null ? new List<string>() : new List<string>(attivi);
        var chiave = CatalogKey.NormalizeName(nomePrivilegio);
        var esistente = risultato.FirstOrDefault(n => CatalogKey.NormalizeName(n) == chiave);

        if (esistente is not null) risultato.Remove(esistente);
        else risultato.Add(nomePrivilegio);

        return risultato;
    }
}
