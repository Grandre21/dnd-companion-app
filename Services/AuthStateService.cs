using Microsoft.AspNetCore.Components;

namespace DndCompanion.Services;

/// <summary>
/// Stato di autenticazione basato su Supabase Auth (Gotrue), non più sulle
/// chiavi localStorage custom (player_id/nickname/role) del vecchio sistema.
/// La sessione è gestita e persistita da Gotrue tramite BrowserSessionHandler.
/// </summary>
public class AuthStateService
{
    private readonly SupabaseService _supabase;
    private readonly NavigationManager _navigation;

    public AuthStateService(SupabaseService supabase, NavigationManager navigation)
    {
        _supabase = supabase;
        _navigation = navigation;
    }

    public async Task<bool> IsLoggedInAsync()
    {
        var client = await _supabase.GetClientAsync();
        return client.Auth.CurrentSession?.User is not null;
    }

    /// <summary>Id dell'utente Gotrue (auth.users.id). Sostituisce GetPlayerIdAsync; sarà usato come owner_id.</summary>
    public async Task<string?> GetUserIdAsync()
    {
        var client = await _supabase.GetClientAsync();
        return client.Auth.CurrentUser?.Id;
    }

    /// <summary>Email Google dell'utente autenticato.</summary>
    public async Task<string?> GetEmailAsync()
    {
        var client = await _supabase.GetClientAsync();
        return client.Auth.CurrentUser?.Email;
    }

    /// <summary>Nome visualizzato: nome completo Google (UserMetadata) con fallback all'email.</summary>
    public async Task<string?> GetDisplayNameAsync()
    {
        var client = await _supabase.GetClientAsync();
        var user = client.Auth.CurrentUser;
        if (user is null) return null;

        if (user.UserMetadata is not null)
        {
            foreach (var key in new[] { "full_name", "name" })
            {
                if (user.UserMetadata.TryGetValue(key, out var value)
                    && value is string s && !string.IsNullOrWhiteSpace(s))
                {
                    return s;
                }
            }
        }
        return user.Email;
    }

    // TODO(campagne): il ruolo non è più globale ma dipende dalla campagna
    // (campaign_members.role). Verrà implementato nel prossimo step; per ora null.
    public Task<string?> GetRoleAsync() => Task.FromResult<string?>(null);

    public async Task LogoutAsync()
    {
        var client = await _supabase.GetClientAsync();
        await client.Auth.SignOut();
        _navigation.NavigateTo("login");
    }
}
