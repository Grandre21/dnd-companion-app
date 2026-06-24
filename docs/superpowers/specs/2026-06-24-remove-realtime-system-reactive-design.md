# Spec — Rimuovere Realtime / System.Reactive (split standalone supabase-csharp)

> Stato: **design approvato** (2026-06-24). DA-FARE §2.
> Brainstorming → questa spec → piano (`writing-plans`) → implementazione subagent-driven.

## 1. Obiettivo e confini

Oggi il progetto dipende dal **meta-pacchetto** `supabase-csharp 0.16.2`, che include `realtime-csharp` →
`System.Reactive` + `Websocket.Client` (~504 KB pre-compressione) **anche se Realtime è disattivato**
(`AutoConnectRealtime = false`) e il combat usa **polling**, non Realtime.

**Obiettivo:** sostituire il meta-pacchetto con i soli pacchetti standalone `postgrest-csharp` (dati) +
`gotrue-csharp` (auth) (`supabase-core` arriva transitivo), eliminando `realtime-csharp`, lo storage,
`System.Reactive` e `Websocket.Client` dal bundle. **A comportamento invariato.**

**Fattibilità (verificata 2026-06-24):** `gotrue-csharp` e `postgrest-csharp` dipendono solo da
`Newtonsoft.Json` + `Supabase.Core` (+ JWT per Gotrue); **nessuno dei due usa System.Reactive/Websocket.Client**
(quelli vengono solo da `realtime-csharp`). I namespace `Postgrest` e `Supabase.Gotrue` usati oggi nel codice
sono già quelli degli standalone (il meta li ri-esporta) → cambiano solo i `PackageReference`, non i `using`.

**Fuori scope:** upgrade a `Supabase` 1.x (namespace `Supabase.Postgrest`, deprecazione del meta) — è separato;
gli standalone 0.16.x-compatibili (gotrue 4.2.7 / postgrest 3.5.1) bastano ora. Nessun cambio a DB/RLS, modelli,
repository, UI.

**Criterio di successo:** build 0/0; i 111 test verdi; **verifica manuale** completa (login/OAuth/reload/CRUD/
combat) ok; bundle Release senza `System.Reactive`/`Websocket.Client`/`Supabase.Realtime`.

## 2. Blast radius (verificato)

Unico file che referenzia il meta (`Supabase.Client`, `Supabase.SupabaseOptions`): **`Services/SupabaseService.cs`**.
Tutti gli altri usano solo la **superficie** restituita da `GetClientAsync()`:
- 11 repository: `client.From<T>()…`, e `CampaignRepository` usa `client.Rpc<string>("join_campaign", args)`.
- `AuthStateService`: `client.Auth.CurrentSession` / `.SignOut()`.
- `Login`/`Home`/`AuthRedirect`: `GetClientAsync()` (Login usa `client.Auth` per il sign-in).

`BrowserSessionHandler` implementa già `Supabase.Gotrue.IGotrueSessionPersistence<Session>` (parla Gotrue diretto).

## 3. Architettura: facade a superficie invariata

`SupabaseService` costruisce internamente:
- un **`Supabase.Gotrue.Client`** (auth) con URL `…/auth/v1`, header `apikey`, e `SessionPersistence = BrowserSessionHandler`;
- un **`Postgrest.Client`** (dati) con URL `…/rest/v1` e `ClientOptions.GetHeaders` = delegato per-richiesta che
  ritorna `apikey: anon` + `Authorization: Bearer {gotrue.CurrentSession?.AccessToken ?? anon}` (così l'RLS vede
  il token corrente, esattamente come fa oggi il meta-client).

`GetClientAsync()` ritorna una **facade** (piccola classe) che espone la superficie già usata:
- `From<T>()` → `_postgrest.Table<T>()` (stesso `IPostgrestTable<T>` su cui i repo concatenano Where/Get/Insert/Update/Delete/Upsert/Filter);
- `Rpc<T>(name, args)` → `_postgrest.Rpc<T>(name, args)`;
- `Auth` → il `Supabase.Gotrue.Client`.

Così **nessun consumer cambia** (usano `var client = await GetClientAsync()` + la superficie). Cambia solo
l'interno di `SupabaseService` + la nuova facade.

> Il wiring esatto (firme di costruzione di `Gotrue.Client`/`Postgrest.Client`, `ClientOptions`, `GetHeaders`,
> nomi `Table`/`Rpc`) sarà **replicato dal sorgente ufficiale `supabase-csharp` 0.16.2 `Client.cs`** in fase di
> piano/implementazione; il compilatore intercetta i mismatch di firma.

## 4. Bootstrap auth (porting)

`GetClientAsync()` mantiene la struttura attuale (serializzata da `_initLock`, idempotente con `_initialized`), ma
chiama Gotrue direttamente al posto di `_client.Auth` e di `_client.InitializeAsync()`:
1. inizializza/`LoadSession()` (ripristino sessione da localStorage tramite `BrowserSessionHandler`);
2. se è un **ritorno OAuth** (`access_token=`/`error_description=` nel fragment) → `GetSessionFromUrl(uri, storeSession: true)`, con logging dell'esito (senza loggare l'URL/token);
3. altrimenti, se `CurrentSession` è scaduta → `RefreshSession()`; se fallisce → `SignOut()`;
4. marca `_initialized`; se ritorno OAuth, pulisce l'URL (`NavigateTo(BaseUri, replace: true)`).

Comportamento identico a oggi (stessa sequenza, stessi commenti/sicurezza: niente token nei log).

## 5. Dipendenze / DI / trimming

- `DndCompanion.csproj`: **rimuovere** `supabase-csharp`; **aggiungere** `postgrest-csharp 3.5.1` + `gotrue-csharp 4.2.7`.
  `supabase-core` arriva transitivo. Spariscono `realtime-csharp`, `supabase-storage-csharp`, `System.Reactive`,
  `Websocket.Client`.
- `TrimmerRootAssembly` `Supabase.Gotrue` / `Supabase.Postgrest` restano (Newtonsoft via reflection).
  `Newtonsoft.Json` resta (serializzatore runtime dei Model).
- DI invariata: `SupabaseService` Singleton; nessun nuovo servizio registrato (Gotrue/Postgrest vivono dentro `SupabaseService`).

## 6. Test e verifica

- **Unit test:** i 111 esistenti (logica pura) restano la rete di sicurezza per il resto; **non** coprono lo strato
  auth/dati (è I/O). Nessun nuovo unit test sensato qui.
- **Build:** `dotnet build -c Debug` → 0/0.
- **Verifica manuale (utente, obbligatoria prima del push)** su `https://localhost:7076`:
  - login Google + **ritorno OAuth** (redirect pulito, niente token in URL dopo);
  - **reload** pagina → resta loggato (sessione persistita); token scaduto → refresh trasparente;
  - **CRUD** su ogni entità: personaggi, incantesimi, mostri, note, razze, classi, inventario, campagne
    (create/update/delete), incl. **RLS** (un dato altrui non è modificabile);
  - **campagne**: join via codice invito (RPC `join_campaign`), creazione, uscita;
  - **combat**: polling + import PG/mostri.
- **Bundle (opzionale):** build Release e confermare l'assenza di `System.Reactive.dll`/`Supabase.Realtime.dll`/
  `Websocket.Client.dll` in `wwwroot/_framework` e il calo di peso.

## 7. Rischi e mitigazioni

- **Strato non testabile in automatico:** un wiring errato rompe login/CRUD. Mitigazione: superficie facade isola
  il cambiamento a `SupabaseService`; wiring replicato dal sorgente 0.16.2; verifica manuale completa pre-push;
  rollback banale (revert del commit, le dipendenze tornano al meta).
- **Firme standalone diverse dal meta** (es. `Table` vs `From`, firma di `Rpc`): intercettate dal build; la facade
  adatta i nomi.
- **`GetHeaders` per-richiesta:** essenziale per l'RLS (token sempre fresco); va verificato a runtime che le REST
  partano con `Authorization: Bearer` corretto (parte della verifica CRUD).
- **Token scaduto / refresh:** stessa logica di oggi, portata su Gotrue diretto; coperto dalla verifica "reload".
