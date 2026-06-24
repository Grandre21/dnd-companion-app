# Sotto-fase A — `SupabaseService` → Repository per aggregato (Implementation Plan)

> Esecuzione autonoma (loop), skill `executing-plans`. Brainstorming saltato su richiesta utente.
> Spez./contesto: DA-FARE §3 (architettura). Estrazione **a comportamento invariato**.

**Goal:** spezzare il god-object `Services/SupabaseService.cs` (~43 metodi) in **11 repository per aggregato
dietro interfacce**, lasciando in `SupabaseService` solo l'infrastruttura sessione/client (`GetClientAsync` +
bootstrap OAuth/refresh). Obiettivo: file più piccoli, separazione delle responsabilità, e seam per i test (§4).

**Architettura:** ogni `XRepository : IXRepository` dipende da `SupabaseService` (per `GetClientAsync()`) e contiene
i metodi dati **identici** (corpi spostati verbatim). I consumatori iniettano l'interfaccia del repo invece di
`SupabaseService`. `SupabaseService` resta il provider di sessione/client (lo usano ancora AuthRedirect/Login/Home/
AuthStateService per `GetClientAsync`). DI: tutti Singleton (i repo dipendono dal Singleton `SupabaseService`).

**Tech:** Blazor WASM .NET 10; cartella nuova `Services/Repositories/`; namespace `DndCompanion.Services.Repositories`.

## Vincoli globali
- Solo `main`; **push = deploy → niente push senza ok esplicito**. Commit locali incrementali OK come checkpoint.
- Build `dotnet build -c Debug` 0/0 + `dotnet test` 62 verdi ad **ogni** step prima del commit.
- Comportamento/firme **invariati**: nomi metodo identici (es. `GetCharactersForCampaignAsync`), solo nuovo "owner".

## Mappa repository → metodi
1. **ICharacterRepository**: GetCharactersForCampaignAsync, CreateCharacterAsync, UpdateCharacterAsync
2. **ISpellRepository**: GetSpellsForCampaignAsync, SearchSpellsAsync, CreateSpellAsync, UpdateSpellAsync, DeleteSpellAsync
3. **IMonsterRepository**: GetMonstersForCampaignAsync, CreateMonsterAsync, UpdateMonsterAsync, DeleteMonsterAsync
4. **INoteRepository**: GetNotesForCampaignAsync, CreateNoteAsync, UpdateNoteAsync, DeleteNoteAsync
5. **ICombatStateRepository**: GetCombatStateAsync, SaveCombatStateAsync
6. **IProfileRepository**: GetProfilesAsync, EnsureProfileAsync
7. **IRaceRepository**: GetRacesForCampaignAsync, CreateRaceAsync, UpdateRaceAsync, DeleteRaceAsync
8. **IClassRepository**: GetClassesForCampaignAsync, CreateClassAsync, UpdateClassAsync, DeleteClassAsync
9. **IInventoryRepository**: GetInventoryForCharacterAsync, CreateInventoryItemAsync, UpdateInventoryItemAsync, DeleteInventoryItemAsync
10. **ICharacterSpellRepository**: GetCharacterSpellsAsync, AddSpellToCharacterAsync, UpdateCharacterSpellAsync, RemoveCharacterSpellAsync
11. **ICampaignRepository**: GetUserCampaignsAsync, CreateCampaignAsync, JoinCampaignAsync, GetCampaignMembersAsync, GetUserRoleInCampaignAsync, DeleteCampaignAsync, LeaveCampaignAsync (+ helper privati invite-code)

## Mappa consumatore → repo da iniettare
- `Pages/Characters.razor`: Character, Race, Class, Spell, Inventory
- `Shared/CharacterTabs/CharacterItemsTab.razor`: Inventory
- `Shared/CharacterTabs/CharacterMagicTab.razor`: CharacterSpell
- `Pages/Combat.razor`: CombatState, Character
- `Pages/Spells.razor`: Spell, Profile
- `Pages/Classes.razor`: Class, Profile
- `Pages/Races.razor`: Race, Profile
- `Pages/Notes.razor`: Note, Profile
- `Pages/Monsters.razor`: Monster
- `Pages/Home.razor`: Profile, Campaign (+ tiene `SupabaseService` per GetClientAsync)
- `Services/CampaignStateService.cs`: Campaign (sostituisce la dipendenza da SupabaseService)
- `Shared/AuthRedirect.razor`, `Pages/Login.razor`, `Services/AuthStateService.cs`: invariati (usano `GetClientAsync`)

## Step
- [ ] **1. Creare gli 11 repository** (`Services/Repositories/*.cs`, interfaccia+classe per file, corpi verbatim).
- [ ] **2. DI + import**: registrare gli 11 repo in `Program.cs` (Singleton); aggiungere `@using DndCompanion.Services.Repositories` a `_Imports.razor`. Build+test. **Commit** "add repository layer (additivo)".
- [ ] **3. Migrare i consumatori** (a gruppi, build+test+commit per gruppo):
  - 3a. Cluster scheda: Characters.razor + ItemsTab + MagicTab + Combat.razor
  - 3b. Pagine catalogo: Spells, Classes, Races, Notes, Monsters
  - 3c. Home + CampaignStateService
- [ ] **4. Snellire `SupabaseService`**: rimuovere i metodi dati (ora inutilizzati) e gli helper invite-code; resta `GetClientAsync` + bootstrap. Build+test. **Commit**.
- [ ] **5. Docs**: DIARIO (paragrafo sotto-fase A) + DA-FARE §3 (marcare ✅ il repository split). **Commit**.
- [ ] **6. [su ok utente] push** (resta solo C — stato auth — della §3).

## Note di rischio
- Lifetime DI: repo Singleton → OK perché dipendono dal Singleton `SupabaseService`.
- `CampaignStateService` perde la dipendenza diretta da `SupabaseService` (passa a `ICampaignRepository`); verificare il grafo DI (nessun ciclo: Campaign repo → SupabaseService; CampaignState → Campaign repo + AuthState).
- Namespace annidato: `DndCompanion.Services.Repositories` vede `SupabaseService` (in `DndCompanion.Services`) senza using.
- Pagine multi-aggregato: durante 3a/3b restano con doppia iniezione (SupabaseService + repo) finché tutti i loro metodi sono migrati; alla fine dello step la rimozione di SupabaseService dall'inject avviene solo se non resta alcun `GetClientAsync`.
