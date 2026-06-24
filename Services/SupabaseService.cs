using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Microsoft.JSInterop;

namespace DndCompanion.Services;

/// <summary>
/// Provider del client Supabase e gestore della sessione (init + ripristino da localStorage +
/// processing del ritorno OAuth + refresh token). NON contiene più accesso ai dati: ogni aggregato
/// ha il suo repository in <c>DndCompanion.Services.Repositories</c>, che ottiene il client da qui
/// tramite <see cref="GetClientAsync"/>.
/// </summary>
public class SupabaseService
{
    private readonly Supabase.Client _client;
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

        var options = new Supabase.SupabaseOptions
        {
            AutoConnectRealtime = false,
            // Persistenza sessione su localStorage: il meta-client collega questo
            // handler a Gotrue e ne carica la sessione durante InitializeAsync().
            // In WASM IJSRuntime è anche IJSInProcessRuntime (sync), richiesto dall'handler.
            SessionHandler = new BrowserSessionHandler((IJSInProcessRuntime)js)
        };

        _client = new Supabase.Client(url, anonKey, options);
    }

    /// <summary>
    /// Punto unico di bootstrap della sessione. Serializzato da _initLock: il PRIMO chiamante
    /// (Home o AuthRedirect, non importa l'ordine) esegue init + ripristino sessione persistita
    /// + eventuale processing del ritorno OAuth; tutti gli altri ricevono un client con la
    /// sessione GIÀ risolta. Questo elimina la race tra AuthRedirect e l'init delle pagine.
    /// </summary>
    public async Task<Supabase.Client> GetClientAsync()
    {
        if (_initialized) return _client;

        await _initLock.WaitAsync();
        try
        {
            if (!_initialized)
            {
                await _client.InitializeAsync();

                // 1) Ripristina la sessione persistita (localStorage) → l'utente resta loggato al reload.
                //    LoadSession è sincrono e non fa rete: nessuna superficie di errore qui.
                _client.Auth.LoadSession();

                // 2) Ritorno OAuth (flusso Implicit, default libreria): i token sono nel fragment
                //    #access_token=...; un fallimento in error_description=. Va processato QUI,
                //    così la sessione è pronta prima che qualunque pagina legga l'identità.
                var uri = _navigation.Uri;
                // Flusso Implicit (default libreria): i token sono nel fragment #access_token=.
                var isOAuthReturn = uri.Contains("access_token=") || uri.Contains("error_description=");
                if (isOAuthReturn)
                {
                    try
                    {
                        var session = await _client.Auth.GetSessionFromUrl(new Uri(uri), storeSession: true);
                        if (session?.User is null)
                        {
                            // Nessuna eccezione ma sessione/utente non risolti: va reso visibile,
                            // altrimenti l'utente risulta "non loggato" senza alcun segnale.
                            // NB: NON loggare l'URL (contiene l'access token nel fragment).
                            Console.Error.WriteLine("[OAuth] Login non completato: sessione senza utente.");
                        }
                    }
                    catch (Exception ex)
                    {
                        // Non silenziare: un fallimento del processing OAuth deve essere diagnosticabile.
                        // Solo il messaggio: niente stack completo né URL (dati sensibili).
                        Console.Error.WriteLine($"[OAuth] Errore nel processing del ritorno OAuth: {ex.Message}");
                    }
                }
                else
                {
                    // Sessione ripristinata da localStorage: se l'access token è SCADUTO, LoadSession
                    // (sincrono, no rete) l'ha caricata comunque scaduta → ogni chiamata REST darebbe
                    // "JWT expired". La rinnoviamo col refresh token; se anche quello è scaduto/invalido
                    // azzeriamo la sessione, così l'utente finisce al login invece di restare bloccato.
                    var current = _client.Auth.CurrentSession;
                    if (current is not null && current.Expired())
                    {
                        try
                        {
                            await _client.Auth.RefreshSession();
                        }
                        catch (Exception ex)
                        {
                            Console.Error.WriteLine($"[Auth] Refresh sessione fallito, eseguo il logout: {ex.Message}");
                            await _client.Auth.SignOut();
                        }
                    }
                }

                _initialized = true;

                // 3) Pulisce l'URL dai token DOPO aver marcato _initialized (le ri-renderizzazioni
                //    indotte dalla navigazione trovano già il client pronto, niente nuovo bootstrap).
                if (isOAuthReturn)
                {
                    _navigation.NavigateTo(_navigation.BaseUri, forceLoad: false, replace: true);
                }
            }
        }
        finally
        {
            _initLock.Release();
        }

        return _client;
    }
}
