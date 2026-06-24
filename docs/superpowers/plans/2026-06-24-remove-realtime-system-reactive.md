# Rimuovere Realtime / System.Reactive — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) o superpowers:executing-plans. Step in checkbox (`- [ ]`).
>
> Spec: [`../specs/2026-06-24-remove-realtime-system-reactive-design.md`](../specs/2026-06-24-remove-realtime-system-reactive-design.md).

**Goal:** Sostituire il meta-pacchetto `supabase-csharp` con i soli standalone `postgrest-csharp` + `gotrue-csharp`, eliminando `realtime-csharp`/`System.Reactive`/`Websocket.Client`, a comportamento invariato.

**Architecture:** Solo `Services/SupabaseService.cs` referenzia il meta. Lo riscriviamo per costruire un `Supabase.Gotrue.Client` (auth) + un `Postgrest.Client` (dati) e restituire da `GetClientAsync()` una **facade** (`SupabaseClient`) con la stessa superficie usata oggi (`From<T>()`, `Rpc<T>()`, `Auth`). Così i 11 repository, `AuthStateService`, `Login`/`Home`/`AuthRedirect` **non cambiano**.

**Tech Stack:** Blazor WebAssembly .NET 10; `postgrest-csharp 3.5.1`, `gotrue-csharp 4.2.7` (namespace `Postgrest` / `Supabase.Gotrue`, già usati oggi); xUnit.

## Global Constraints

- Solo branch `main`; **push = deploy**. **Commit/push solo su ok esplicito dell'utente**; lo strato auth/dati **non è verificabile in automatico né dall'agente** → la **verifica manuale completa è dell'utente** prima del push.
- Build: `dotnet build -c Debug` → **0 errori / 0 avvisi** (è il gate principale: conferma che tutti i call-site compilano contro la facade). Test: `dotnet test Tests/DndCompanion.Tests.csproj` → **111 verdi** (logica pura; non coprono questo strato).
- **Comportamento invariato.** Stessa sequenza di bootstrap auth, stessa sicurezza (niente token/URL nei log).
- **Build-as-oracle per le firme di libreria:** dove la firma esatta dello standalone differisce dal meta (posizione di `GetHeaders`, firma di `Rpc<T>`, eventuale init di Gotrue, overload di `GetSessionFromUrl`), l'implementer **itera sugli errori di compilazione** finché 0/0, mantenendo il comportamento. Riferimento concettuale: ciò che faceva `Supabase.Client` (glue auth-token → header Postgrest).
- Namespace invariati: i Model usano `Postgrest.Attributes`/`Postgrest.Models`; i repo usano `Postgrest.Constants.Operator`/`Postgrest.QueryOptions`. Non cambiarli.

## File Structure

- `DndCompanion.csproj` — modifica: rimuovere `supabase-csharp`; aggiungere `postgrest-csharp 3.5.1` + `gotrue-csharp 4.2.7`.
- `Services/SupabaseClient.cs` — **nuovo**. La facade (`From<T>`/`Rpc<T>`/`Auth`).
- `Services/SupabaseService.cs` — riscrittura interna (Gotrue + Postgrest + token wiring + bootstrap), `GetClientAsync()` ritorna la facade.
- `docs/DA-FARE.md`, `docs/DIARIO.md` — modifica: marcare §2.
- (Invariati: 11 repository, `AuthStateService`, `BrowserSessionHandler`, `Login`/`Home`/`AuthRedirect`, Program.cs.)

---

### Task 1: Swap pacchetti + riscrittura `SupabaseService` con facade

**Files:**
- Modify: `DndCompanion.csproj`
- Create: `Services/SupabaseClient.cs`
- Modify: `Services/SupabaseService.cs`

**Interfaces:**
- Consumes (libreria): `Supabase.Gotrue.Client`, `Supabase.Gotrue.ClientOptions<Session>`, `Postgrest.Client`, `Postgrest.ClientOptions`, `BrowserSessionHandler` (esistente, `IGotrueSessionPersistence<Session>`).
- Produces: `SupabaseService.GetClientAsync() -> Task<SupabaseClient>`; `SupabaseClient.From<T>() -> Postgrest.Table<T>`; `SupabaseClient.Rpc<T>(string, Dictionary<string,object>) -> Task<T?>`; `SupabaseClient.Auth -> Supabase.Gotrue.Client`. (Questi sono usati, invariati, da repo e AuthStateService.)

- [ ] **Step 1: Scambiare i PackageReference nel csproj**

In `DndCompanion.csproj`, sostituire la riga `<PackageReference Include="supabase-csharp" Version="0.16.2" />` con:

```xml
    <PackageReference Include="postgrest-csharp" Version="3.5.1" />
    <PackageReference Include="gotrue-csharp" Version="4.2.7" />
```

Lasciare invariati gli altri (`Microsoft.AspNetCore.Components.WebAssembly`, `Newtonsoft.Json` arriva transitivo come oggi, `Microsoft.IdentityModel.JsonWebTokens`, `System.IdentityModel.Tokens.Jwt`) e i `TrimmerRootAssembly` (`Supabase.Gotrue`, `Supabase.Postgrest`).

- [ ] **Step 2: Restore e verifica della rimozione transitiva**

Run: `dotnet restore`
Poi: `dotnet build -c Debug 2>&1` — **atteso: FALLISCE** (errori in `SupabaseService.cs`: `Supabase.Client`/`SupabaseOptions` non più risolti). Questo conferma che solo quel file dipende dal meta.

- [ ] **Step 3: Creare la facade `Services/SupabaseClient.cs`**

```csharp
using Supabase.Gotrue;

namespace DndCompanion.Services;

/// <summary>
/// Facade che preserva la superficie del vecchio meta-client (<c>From</c>/<c>Rpc</c>/<c>Auth</c>),
/// così repository e AuthStateService restano invariati dopo lo split standalone
/// (postgrest-csharp + gotrue-csharp al posto di supabase-csharp).
/// </summary>
public sealed class SupabaseClient
{
    private readonly Postgrest.Client _postgrest;

    public SupabaseClient(Client auth, Postgrest.Client postgrest)
    {
        Auth = auth;
        _postgrest = postgrest;
    }

    /// <summary>Il client Gotrue (auth): CurrentSession, SignIn, SignOut, GetSessionFromUrl, ...</summary>
    public Client Auth { get; }

    /// <summary>Tabella tipizzata (Postgrest). Sostituisce il vecchio <c>Supabase.Client.From&lt;T&gt;()</c>.</summary>
    public Postgrest.Table<T> From<T>() where T : Postgrest.Models.BaseModel, new()
        => _postgrest.Table<T>();

    /// <summary>Chiamata RPC (stored procedure). Usata da CampaignRepository.join_campaign.</summary>
    public Task<T?> Rpc<T>(string procedureName, Dictionary<string, object> parameters)
        => _postgrest.Rpc<T>(procedureName, parameters);
}
```

> ⚠️ **Build-as-oracle:** se in postgrest-csharp 3.5.1 il metodo tabella o la firma di `Rpc<T>` differiscono
> (es. `Rpc` ritorna un response da spacchettare, o `Table<T>` ha vincoli diversi), adattare il corpo della
> facade **mantenendo le firme pubbliche** `From<T>()`/`Rpc<T>(string, Dictionary<string,object>)`/`Auth` (i
> call-site dipendono da queste). Il tipo restituito da `From<T>()` deve restare quello su cui i repo concatenano
> `Where/Get/Insert/Update/Delete/Upsert/Filter` (è `Postgrest.Table<T>`).

- [ ] **Step 4: Riscrivere `Services/SupabaseService.cs`**

Sostituire l'intero contenuto con (porting 1:1 della logica di bootstrap, su Gotrue diretto + Postgrest con token dinamico):

```csharp
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

        // Auth (Gotrue). La persistenza sessione su localStorage resta BrowserSessionHandler.
        _auth = new Client(new ClientOptions<Session>
        {
            Url = $"{url}/auth/v1",
            Headers = new Dictionary<string, string> { { "apikey", anonKey } },
            SessionPersistence = new BrowserSessionHandler((IJSInProcessRuntime)js),
        });

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
                        var session = await _auth.GetSessionFromUrl(new Uri(uri));
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
```

> ⚠️ **Build-as-oracle (punti da confermare contro i pacchetti installati, iterando sul build):**
> - `ClientOptions<Session>` campo persistenza: qui `SessionPersistence`. Se in gotrue-csharp 4.2.7 il nome è
>   diverso (es. `SessionPersistor`/`SessionHandler`), usarlo — deve accettare il `BrowserSessionHandler`
>   (`IGotrueSessionPersistence<Session>`).
> - `Postgrest.ClientOptions` / `Postgrest.Client`: il delegato header dinamico (`GetHeaders`) può essere su
>   `ClientOptions` o una proprietà del `Client`. Collegarlo come sopra (token corrente per-richiesta).
> - `GetSessionFromUrl`: usare l'overload disponibile; se richiede `storeSession`, passarlo `true`.
> - Se `Client` (Gotrue) richiede un init asincrono prima dell'uso, chiamarlo dentro `GetClientAsync()` prima di
>   `LoadSession()` (oggi lo faceva `_client.InitializeAsync()` del meta).
> - Mantenere il comportamento: niente token/URL nei log; sequenza identica.

- [ ] **Step 5: Build fino a 0/0**

Run: `dotnet build -c Debug 2>&1 | tail -20`
Iterare sulle firme di libreria finché: **0 errori / 0 avvisi**. Il build verde conferma che tutti i call-site
(11 repo: `client.From<T>()`/`client.Rpc<string>(...)`; `AuthStateService`: `client.Auth.*`; `Login`/`Home`/
`AuthRedirect`: `GetClientAsync()`) compilano contro la facade senza modifiche.

- [ ] **Step 6: Test suite**

Run: `dotnet test Tests/DndCompanion.Tests.csproj 2>&1 | tail -3`
Expected: **111 verdi** (la logica pura non è toccata).

- [ ] **Step 7: Verifica assenza dipendenze (sanity)**

Run: `dotnet build -c Debug 2>&1 | grep -iE "Reactive|Websocket|Realtime" || echo "nessun riferimento a Reactive/Websocket/Realtime nel build"`
Expected: nessun riferimento (o solo, eventualmente, nei `TrimmerRootAssembly` che restano — quelli sono Gotrue/Postgrest, ok).

- [ ] **Step 8: [UTENTE] Verifica manuale (OBBLIGATORIA prima del commit/push)**

Su `https://localhost:7076`: login Google + ritorno OAuth (URL ripulito), reload (sessione persistita), refresh token (riapertura dopo scadenza), CRUD su **tutte** le entità (PG/incantesimi/mostri/note/razze/classi/inventario/campagne, create/update/delete) con RLS, join campagna via codice (RPC), combat (polling + import). Tutto identico a prima.

- [ ] **Step 9: [su ok utente] Commit**

```bash
git add DndCompanion.csproj Services/SupabaseClient.cs Services/SupabaseService.cs
git commit -m "refactor(deps): split standalone postgrest+gotrue, rimuovi Realtime/System.Reactive"
```

---

### Task 2: Documentazione + memoria

**Files:**
- Modify: `docs/DA-FARE.md` (§2), `docs/DIARIO.md`

- [ ] **Step 1: DA-FARE §2** — cambiare il bullet "🟠 **Eliminare Realtime / `System.Reactive`.**" in "✅ … — FATTO (2026-06-24)" con una riga sul risultato (split standalone gotrue+postgrest dietro facade `SupabaseClient`; rimossi realtime/storage/System.Reactive/Websocket.Client; comportamento invariato; verifica manuale). Aggiornare il caveat collegato in §8 (combat/Realtime) se necessario.

- [ ] **Step 2: DIARIO** — paragrafo "Rimozione Realtime/System.Reactive (2026-06-24)": meta-pacchetto sostituito dagli standalone, facade `SupabaseClient` a superficie invariata, token via `GetHeaders`, bundle alleggerito.

- [ ] **Step 3: [su ok utente] Commit**

```bash
git add docs/DA-FARE.md docs/DIARIO.md
git commit -m "docs: rimozione Realtime/System.Reactive completata (§2)"
```

---

## Self-Review

- **Copertura spec:** §1 obiettivo/fattibilità → Task 1 (Step 1-2); §2 blast radius (solo SupabaseService) → Task 1 build verde lo conferma; §3 facade → Step 3; §4 bootstrap port → Step 4; §5 deps/DI/trim → Step 1; §6 test/verifica → Step 6/7/8. Coperto.
- **Placeholder:** il codice di facade e SupabaseService è completo; i ⚠️ "build-as-oracle" non sono placeholder ma la procedura corretta per fissare le firme di una libreria esterna non eseguibile in fase di pianificazione (il build è l'oracolo).
- **Coerenza tipi/nomi:** `GetClientAsync(): Task<SupabaseClient>`; `SupabaseClient.From<T>()/Rpc<T>(string,Dictionary<string,object>)/Auth` usati identici dai consumer esistenti (verificati invariati: repo usano `.From<T>()`/`.Rpc<string>(...)`, AuthStateService usa `.Auth.*`).
- **Rischio chiave:** strato non auto-verificabile → gate di **verifica manuale utente** (Step 8) prima di commit/push; rollback = `git revert` del commit (torna al meta). Firme libreria → build-as-oracle.
