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
    private readonly BrowserSessionHandler _sessionHandler;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _initialized;
    private DateTime? _ultimoRefreshFallito;

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
        // Teniamo un riferimento all'handler di persistenza: serve a forzare la pulizia di localStorage
        // (DestroySession) nel bootstrap quando la sessione scaduta non si riesce a rinfrescare.
        _sessionHandler = new BrowserSessionHandler((IJSInProcessRuntime)js);
        _auth.SetPersistence(_sessionHandler);

        // Dati (Postgrest). Il token entra per-richiesta via GetHeaders: apikey + Bearer del token corrente
        // (anon se non loggato), così l'RLS vede sempre il token valido — come faceva il meta-client.
        _postgrest = new Postgrest.Client($"{url}/rest/v1", new Postgrest.ClientOptions
        {
            Headers = new Dictionary<string, string> { { "apikey", anonKey } },
        })
        {
            GetHeaders = () =>
            {
                // Inviamo il Bearer dell'utente SOLO se la sessione esiste e NON è scaduta: un access token
                // scaduto verrebbe rifiutato dal gateway (403 bad_jwt) — in quel caso usiamo l'anon key.
                var session = _auth.CurrentSession;
                var bearer = session is not null && !session.Expired()
                    ? session.AccessToken
                    : anonKey;
                return new Dictionary<string, string>
                {
                    { "apikey", anonKey },
                    { "Authorization", $"Bearer {bearer}" },
                };
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
        if (_initialized)
        {
            // Ogni chiamata dati passa di qui (i repository fanno await GetClientAsync()): è il solo
            // punto asincrono comune, e quindi il posto giusto per garantire un token vivo. GetHeaders
            // è sincrono e non potrebbe rinfrescare nulla.
            var corrente = _auth.CurrentSession;
            if (corrente is not null && SessionFreshness.VaRinfrescata(corrente.ExpiresAt(), DateTime.UtcNow))
                await RinnovaSessioneSeServeAsync();
            return _facade;
        }

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
                    // Sessione ripristinata ma access token scaduto (o prossimo a scadere) → refresh;
                    // se il refresh token non è recuperabile, logout pulito. Vincolo: NIENTE in questo
                    // ramo può propagare un'eccezione fuori da GetClientAsync, altrimenti il login si
                    // blocca (Login.razor mostra "Errore di accesso"). Chiama la versione SENZA lock:
                    // questo ramo gira già dentro _initLock, che non è rientrante.
                    await RinnovaSessioneAsync();
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

    /// <summary>
    /// Rinnova la sessione usando il refresh token, se serve. Usa l'overload a DUE argomenti di
    /// <c>RefreshToken</c>: quello senza argomenti (e <c>RefreshSession</c>) in gotrue-csharp 4.2.7
    /// lancia "Session expired" quando l'access token è GIÀ scaduto — cioè si rifiuta di lavorare
    /// proprio nel caso per cui esiste. Non usare <c>SetSession</c> come alternativa: la sua prima
    /// riga distrugge la sessione locale, quindi un fallimento per assenza di rete sloggerebbe un
    /// utente il cui refresh token è ancora buono.
    /// Non propaga MAI eccezioni: è chiamato da GetClientAsync, e il vincolo di riga 119-121 vale.
    /// </summary>
    /// <remarks>
    /// Prende <see cref="_initLock"/>: da chiamare SOLO fuori dal lock (il fast-path di
    /// <see cref="GetClientAsync"/>). <see cref="SemaphoreSlim"/> non è rientrante — il bootstrap,
    /// che gira già dentro il lock, chiama invece <see cref="RinnovaSessioneAsync"/> direttamente.
    /// </remarks>
    private async Task RinnovaSessioneSeServeAsync()
    {
        await _initLock.WaitAsync();
        try
        {
            await RinnovaSessioneAsync();
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <summary>
    /// Corpo della logica di rinnovo, SENZA prendere <see cref="_initLock"/>: ricontrolla da sé se
    /// serve ancora rinfrescare (double-check, per il caso di due chiamate concorrenti) e se ha
    /// senso ritentare dopo un fallimento recente. Chiamato sia da
    /// <see cref="RinnovaSessioneSeServeAsync"/> (che il lock lo prende) sia dal bootstrap di
    /// <see cref="GetClientAsync"/> (che gira già dentro il lock).
    /// </summary>
    private async Task RinnovaSessioneAsync()
    {
        var session = _auth.CurrentSession;
        if (session is null
            || string.IsNullOrEmpty(session.AccessToken)
            || string.IsNullOrEmpty(session.RefreshToken)
            || !SessionFreshness.VaRinfrescata(session.ExpiresAt(), DateTime.UtcNow))
            return;

        if (!SessionFreshness.SiPuoRitentare(_ultimoRefreshFallito, DateTime.UtcNow))
            return;

        try
        {
            // Overload a due argomenti: niente guardia su "già scaduto" (v. XML doc sopra). Notifica
            // già da sé TokenRefreshed (ripersiste la sessione), ma NON riarma il timer interno di
            // auto-refresh di gotrue (TokenRefresh.ManageAutoRefresh ignora TokenRefreshed): per
            // quello serve il NotifyAuthStateChange(SignedIn) esplicito qui sotto.
            await _auth.RefreshToken(session.AccessToken, session.RefreshToken);
            _ultimoRefreshFallito = null;
            _auth.NotifyAuthStateChange(Supabase.Gotrue.Constants.AuthState.SignedIn);
        }
        catch (Supabase.Gotrue.Exceptions.GotrueException ex) when (
            ex.Reason == Supabase.Gotrue.Exceptions.FailureHint.Reason.ExpiredRefreshToken
            || ex.Reason == Supabase.Gotrue.Exceptions.FailureHint.Reason.InvalidRefreshToken)
        {
            // Refresh token morto/invalido: non recuperabile, logout pulito.
            Console.Error.WriteLine($"[Auth] Refresh token non valido, eseguo il logout: {ex.Message}");
            await SignOutAsync();
        }
        catch (Exception ex)
        {
            // Rete assente, 5xx, timeout: NON sloggare (l'app è offline-capable, il refresh token può
            // essere ancora valido). Segna il fallimento per non ritentare subito e riprova più avanti.
            Console.Error.WriteLine($"[Auth] Refresh sessione fallito, riprovo più avanti: {ex.Message}");
            _ultimoRefreshFallito = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Porta l'app a uno stato di logout pulito senza MAI propagare eccezioni: dopo questa chiamata
    /// <c>_auth.CurrentSession</c> è null (AuthStateService riporta "non loggato") e la sessione
    /// persistita in localStorage è rimossa.
    /// </summary>
    /// <remarks>
    /// gotrue-csharp 4.2.7 NON espone un sign-out solo-locale: <c>Client.DestroySession()</c> e
    /// <c>Client.UpdateSession()</c> sono privati, e <c>Client.SignOut()</c> chiama prima il server
    /// (<c>_api.SignOut(accessToken)</c>) — con un token scaduto il gateway risponde 403 bad_jwt e
    /// l'eccezione impedisce di arrivare al successivo <c>UpdateSession(null)</c>, lasciando la sessione
    /// in memoria. Quindi: (1) proviamo <c>SignOut()</c> avvolto (sul percorso felice azzera già tutto);
    /// (2) garantiamo l'azzeramento in-memory con <c>SetSession("","")</c> avvolto — la sua PRIMA riga è
    /// il <c>DestroySession()</c> privato (<c>UpdateSession(null)</c> → CurrentSession=null + evento
    /// SignedOut che pulisce localStorage), poi lancia prima di qualsiasi chiamata di rete; (3) per
    /// sicurezza forziamo comunque <c>DestroySession()</c> dell'handler di persistenza su localStorage.
    /// </remarks>
    public async Task SignOutAsync()
    {
        // (1) Tentativo di logout lato server (revoca il refresh token). Avvolto: su token scaduto lancia.
        try
        {
            await _auth.SignOut();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Auth] SignOut lato server fallito, procedo con la pulizia locale: {ex.Message}");
        }

        // (2) Azzeramento in-memory garantito: SetSession("","") esegue DestroySession() (CurrentSession=null)
        // e poi lancia subito (token vuoti), prima di qualunque rete. Avvolto: l'eccezione attesa è ignorata.
        if (_auth.CurrentSession is not null)
        {
            try
            {
                await _auth.SetSession(string.Empty, string.Empty);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[Auth] Pulizia sessione in-memory: {ex.Message}");
            }
        }

        // (3) Rimozione difensiva della sessione persistita (idempotente).
        try
        {
            _sessionHandler.DestroySession();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Auth] Pulizia localStorage fallita: {ex.Message}");
        }
    }
}
