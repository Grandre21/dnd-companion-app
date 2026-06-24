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
    private bool _initialized;

    public CampaignStateService(IJSRuntime js, ICampaignRepository campaigns, AuthStateService auth)
    {
        _js = js;
        _campaigns = campaigns;
        _auth = auth;
    }

    public string? ActiveCampaignId { get; private set; }

    /// <summary>Ruolo dell'utente NELLA campagna attiva: "master" | "player" | null.</summary>
    public string? ActiveCampaignRole { get; private set; }

    public bool IsMaster => string.Equals(ActiveCampaignRole, "master", StringComparison.OrdinalIgnoreCase);

    /// <summary>Notifica le pagine quando cambia la campagna attiva (o il suo ruolo).</summary>
    public event Action? OnActiveCampaignChanged;

    /// <summary>Carica la campagna attiva da localStorage. Idempotente.</summary>
    public async Task InitializeAsync()
    {
        if (_initialized) return;
        _initialized = true;

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
        OnActiveCampaignChanged?.Invoke();
    }

    public async Task ClearActiveCampaign()
    {
        ActiveCampaignId = null;
        ActiveCampaignRole = null;
        await _js.InvokeVoidAsync("localStorage.removeItem", ActiveCampaignKey);
        OnActiveCampaignChanged?.Invoke();
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
