using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Microsoft.JSInterop;
using Supabase.Gotrue;

namespace DndCompanion.Services;

/// <summary>
/// Provider di sessione/client Supabase. Dopo lo split standalone non usa più il meta-client:
/// costruisce un Gotrue.Client (auth) + un Postgrest.Client (dati) e li espone tramite la facade
/// <see cref="SupabaseClient"/> (superficie invariata per repository/AuthStateService).
/// </summary>
public class SupabaseService
{
    private readonly Client _auth;
    private readonly Postgrest.Client _postgrest;
    private readonly SupabaseClient _facade;
    private readonly NavigationManager _navigation;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _initialized;

    public SupabaseService(IConfiguration configuration, IJSRuntime js, NavigationManager navigation)
    {
        _navigation = navigation;

        var url = configuration["Supabase:Url"]
            ?? throw new InvalidOperationException("Supabase:Url non configurato in appsettings.json");
        var anonKey = configuration["Supabase:AnonKey"]
            ?? throw new InvalidOperationException("Supabase:AnonKey non configurato in appsettings.json");

        // Auth (Gotrue). In gotrue-csharp 4.2.7 ClientOptions NON è generico e non espone una proprietà
        // di persistenza nell'initializer: la persistenza su localStorage (BrowserSessionHandler) si
        // collega con Client.SetPersistence(...). L'apikey va nel campo Headers delle opzioni.
        _auth = new Client(new ClientOptions
        {
            Url = $"{url}/auth/v1",
            Headers = new Dictionary<string, string> { { "apikey", anonKey } },
        });
        _auth.SetPersistence(new BrowserSessionHandler((IJSInProcessRuntime)js));

        // Dati (Postgrest). Il token entra per-richiesta via GetHeaders: apikey + Bearer del token corrente
        // (anon se non loggato), così l'RLS vede sempre il token valido — come faceva il meta-client.
        _postgrest = new Postgrest.Client($"{url}/rest/v1", new Postgrest.ClientOptions
        {
            Headers = new Dictionary<string, string> { { "apikey", anonKey } },
        })
        {
            GetHeaders = () => new Dictionary<string, string>
            {
                { "apikey", anonKey },
                { "Authorization", $"Bearer {_auth.CurrentSession?.AccessToken ?? anonKey}" },
            },
        };

        _facade = new SupabaseClient(_auth, _postgrest);
    }

    /// <summary>
    /// Bootstrap della sessione (idempotente, serializzato): ripristino da localStorage + processing
    /// del ritorno OAuth + refresh se scaduta. Stessa sequenza di prima, ora su Gotrue diretto.
    /// </summary>
    public async Task<SupabaseClient> GetClientAsync()
    {
        if (_initialized) return _facade;

        await _initLock.WaitAsync();
        try
        {
            if (!_initialized)
            {
                // 1) Ripristina la sessione persistita (sync, no rete) → utente loggato al reload.
                _auth.LoadSession();

                // 2) Ritorno OAuth (flusso Implicit): token nel fragment #access_token=.
                var uri = _navigation.Uri;
                var isOAuthReturn = uri.Contains("access_token=") || uri.Contains("error_description=");
                if (isOAuthReturn)
                {
                    try
                    {
                        // gotrue-csharp 4.2.7: l'unico overload richiede storeSession (lo passiamo true,
                        // come faceva il meta-client, così la sessione OAuth viene persistita).
                        var session = await _auth.GetSessionFromUrl(new Uri(uri), storeSession: true);
                        if (session?.User is null)
                            // NB: NON loggare l'URL (contiene l'access token nel fragment).
                            Console.Error.WriteLine("[OAuth] Login non completato: sessione senza utente.");
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"[OAuth] Errore nel processing del ritorno OAuth: {ex.Message}");
                    }
                }
                else
                {
                    // Sessione ripristinata ma access token scaduto → refresh; se fallisce, logout pulito.
                    var current = _auth.CurrentSession;
                    if (current is not null && current.Expired())
                    {
                        try
                        {
                            await _auth.RefreshSession();
                        }
                        catch (Exception ex)
                        {
                            Console.Error.WriteLine($"[Auth] Refresh sessione fallito, eseguo il logout: {ex.Message}");
                            await _auth.SignOut();
                        }
                    }
                }

                _initialized = true;

                // 3) Pulisce l'URL dai token dopo aver marcato _initialized.
                if (isOAuthReturn)
                    _navigation.NavigateTo(_navigation.BaseUri, forceLoad: false, replace: true);
            }
        }
        finally
        {
            _initLock.Release();
        }

        return _facade;
    }
}
