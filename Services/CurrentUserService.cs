namespace DndCompanion.Services;

/// <summary>
/// Facade dello stato utente per le pagine: raccoglie l'identità (da <see cref="AuthStateService"/>) e il
/// contesto di campagna/ruolo (da <see cref="CampaignStateService"/>) dietro un'unica
/// <see cref="EnsureLoadedAsync"/>, eliminando il boilerplate ripetuto in ogni pagina
/// (InitializeAsync + lettura di userId / isMaster / campaignId).
/// </summary>
public class CurrentUserService
{
    private readonly AuthStateService _auth;
    private readonly CampaignStateService _campaign;

    public CurrentUserService(AuthStateService auth, CampaignStateService campaign)
    {
        _auth = auth;
        _campaign = campaign;
    }

    /// <summary>Id utente Gotrue (auth.users.id). Valorizzato da <see cref="EnsureLoadedAsync"/>.</summary>
    public string? UserId { get; private set; }

    /// <summary>Nome visualizzato (full name Google, fallback email). Valorizzato da <see cref="EnsureLoadedAsync"/>.</summary>
    public string? DisplayName { get; private set; }

    /// <summary>Campagna attiva: delega allo stato condiviso (sempre aggiornato al cambio campagna).</summary>
    public string? CampaignId => _campaign.ActiveCampaignId;

    /// <summary>Ruolo master nella campagna attiva: delega allo stato condiviso.</summary>
    public bool IsMaster => _campaign.IsMaster;

    /// <summary>
    /// Bootstrap idempotente: inizializza lo stato di campagna e carica l'identità una sola volta.
    /// Da chiamare in <c>OnInitializedAsync</c> prima di leggere le proprietà.
    /// </summary>
    public async Task EnsureLoadedAsync()
    {
        await _campaign.InitializeAsync();
        UserId ??= await _auth.GetUserIdAsync();
        DisplayName ??= await _auth.GetDisplayNameAsync();
    }
}
