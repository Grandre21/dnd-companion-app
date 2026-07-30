using DndCompanion.Services.Repositories;
using Microsoft.JSInterop;

namespace DndCompanion.Services;

/// <summary>
/// Stato della campagna attiva, condiviso a livello app (Singleton).
/// La campagna attiva è ricordata in localStorage; il ruolo dell'utente nella
/// campagna ("master"/"player") è derivato da campaign_members.
/// </summary>
public class CampaignStateService
{
    private const string ActiveCampaignKey = "active_campaign_id";

    private readonly IJSRuntime _js;
    private readonly ICampaignRepository _campaigns;
    private readonly AuthStateService _auth;
    private Task? _initialization;

    public CampaignStateService(IJSRuntime js, ICampaignRepository campaigns, AuthStateService auth)
    {
        _js = js;
        _campaigns = campaigns;
        _auth = auth;
    }

    public string? ActiveCampaignId { get; private set; }

    /// <summary>Ruolo dell'utente NELLA campagna attiva: "master" | "player" | null.</summary>
    public string? ActiveCampaignRole { get; private set; }

    /// <summary>
    /// Scatta quando la campagna attiva cambia o viene azzerata. Serve ai componenti che vivono
    /// nel layout (la barra di navigazione) e che quindi non si ri-renderizzano per conto proprio
    /// quando è una pagina a cambiare campagna. Non scatta su <see cref="InitializeAsync"/>: lì il
    /// sottoscrittore ha già letto lo stato subito dopo l'await.
    /// </summary>
    public event Action? ActiveCampaignChanged;

    public bool IsMaster => string.Equals(ActiveCampaignRole, "master", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Carica la campagna attiva da localStorage e il ruolo dell'utente. Idempotente: i
    /// chiamanti concorrenti condividono lo stesso caricamento, non ne avviano uno a testa.
    /// </summary>
    /// <remarks>
    /// Si tiene il <see cref="Task"/> e non un flag booleano perché il caricamento **può
    /// fallire**: legge il ruolo dal database, quindi basta un avvio senza rete. Con il flag
    /// alzato prima degli await, un fallimento lasciava <see cref="ActiveCampaignRole"/> nullo
    /// **per tutta la sessione** e ogni chiamata successiva era un no-op — il master vedeva
    /// l'interfaccia da giocatore, senza errori a schermo e senza modo di rimediare se non
    /// ricaricando. Scartando il Task fallito, il chiamante successivo riprova.
    /// </remarks>
    public async Task InitializeAsync()
    {
        var pending = _initialization ??= LoadStateAsync();
        try
        {
            await pending;
        }
        catch
        {
            // Lo scarto avviene qui e non dentro LoadStateAsync: se quel metodo fallisse prima
            // di cedere il controllo, il campo non sarebbe ancora stato assegnato e il Task
            // fallito finirebbe comunque in cache. Il confronto per riferimento evita di
            // buttare via un caricamento nuovo, avviato nel frattempo da un altro chiamante.
            if (ReferenceEquals(_initialization, pending)) _initialization = null;
            throw;
        }
    }

    private async Task LoadStateAsync()
    {
        ActiveCampaignId = await _js.InvokeAsync<string?>("localStorage.getItem", ActiveCampaignKey);
        if (!string.IsNullOrEmpty(ActiveCampaignId))
        {
            await LoadRoleAsync();
        }
    }

    public Task<string?> GetActiveCampaignIdAsync() => Task.FromResult(ActiveCampaignId);

    public async Task SetActiveCampaignAsync(string campaignId)
    {
        ActiveCampaignId = campaignId;
        await _js.InvokeVoidAsync("localStorage.setItem", ActiveCampaignKey, campaignId);
        await LoadRoleAsync();
        ActiveCampaignChanged?.Invoke();
    }

    public async Task ClearActiveCampaign()
    {
        ActiveCampaignId = null;
        ActiveCampaignRole = null;
        await _js.InvokeVoidAsync("localStorage.removeItem", ActiveCampaignKey);
        ActiveCampaignChanged?.Invoke();
    }

    private async Task LoadRoleAsync()
    {
        var userId = await _auth.GetUserIdAsync();
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(ActiveCampaignId))
        {
            ActiveCampaignRole = null;
            return;
        }
        ActiveCampaignRole = await _campaigns.GetUserRoleInCampaignAsync(userId, ActiveCampaignId);
    }
}
