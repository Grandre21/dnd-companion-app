# Spec — Fase 2B: estrazione dei tab di `Characters.razor` in componenti

> Stato: **design approvato** (2026-06-24), in attesa di review dello spec prima del piano.
> Contesto: DA-FARE §3 (architettura). Sotto-progetto B della Fase 2 (refactor). Indipendente da:
> A (SupabaseService → repository) e C (stato auth/ruolo).

## 1. Obiettivo e confini

`Pages/Characters.razor` è ~2.4k righe: una sola pagina che fa tab-bar, 5 tab (Combat/Stats/Bio/Items/Magic),
form di modifica e tutto il `@code`. Difficile da tenere in testa e da modificare.

**Obiettivo:** estrarre i **5 tab** in componenti figli riutilizzabili, **a comportamento identico**
(estrazione pura di markup + codice, nessun cambio di logica o aspetto). Il genitore resta l'orchestratore
(tab-bar, `selected`, persistenza).

**Fuori scope (follow-up separati):**
- Il **form di modifica** (`editDraft` + accordion `formSections` + `NormalizeDraft` + scelta classe/razza):
  blocco grosso e diverso, merita il suo spec.
- Il **data layer** (sotto-fase A): qui i tab che fanno I/O iniettano ancora `SupabaseService`.

**Criterio di successo:** la pagina Characters si comporta e appare **identica** a prima; `Characters.razor`
è sensibilmente più corto; ogni tab è un componente comprensibile in isolamento.

## 2. Pattern di estrazione (già provato da `StatCard`)

`Shared/StatCard.razor` è il precedente in produzione: prende `Character`, `IsEditMode`, `OnChanged`
(EventCallback) e lo Stats tab lo usa già 6 volte (`Characters.razor:283-288`). Si replica quel pattern.

Ogni componente-tab espone:
- `[Parameter, EditorRequired] Character Character` — l'oggetto **vivo** (il genitore resta proprietario di
  `selected`; il figlio muta i campi e notifica).
- `[Parameter] bool CanEdit` — abilita i controlli di modifica.
- `[Parameter] EventCallback OnChanged` — invocato dal figlio **dopo** una modifica a un campo di `Character`;
  il **genitore** persiste (`SaveCharacterAsync`) e mostra il toast.

Regole:
- Lo **stato UI locale** (draft tipo `notesDraft`, flag `isSavingHp`/`isSavingNotes`/`isEditingMoney`) vive
  **dentro** il figlio, non più nel genitore.
- I **metodi di formato puri** (`FormatBonus`, `SplitDefenses`, `SpellAbilityShort`) si spostano in un helper
  statico condiviso `Shared/CharacterTabs/CharacterView.cs` (DRY tra i tab). I **calcoli D&D** restano in
  `CharacterCalculations` (già puri e testati).
- Cartella dei nuovi componenti: **`Shared/CharacterTabs/`** (non `Shared/Character/`, per non creare un
  segmento di namespace `Character` che collide col tipo `Models.Character`).

## 3. I cinque componenti

Tutti in `Shared/CharacterTabs/`. Responsabilità e superficie:

| Componente | Contenuto attuale | Parametri | Note |
|---|---|---|---|
| `CharacterStatsTab` | bonus competenza + 6 `StatCard` (`:277-290`) | `Character`, `CanEdit`, `OnChanged` | Quasi triviale: incapsula i 6 `StatCard`. |
| `CharacterBioTab` | righe bio + blocchi (aspetto/storia/tratti/talenti) + textarea "note libere" (`:293-360`) | `Character`, `CanEdit`, `EventCallback OnSaveNotes` | Possiede `notesDraft`/`isSavingNotes`; il bottone "Salva note" resta dedicato: setta `Character.Notes` e invoca `OnSaveNotes` (il genitore persiste). |
| `CharacterCombatTab` | PF/PF temp/CA, dadi vita, tiri salvezza morte, INI/VEL/PERC/ispirazione, **armi (lette dall'inventario)**, difese (`:117-274`) | `Character`, `CanEdit`, `OnChanged`, `IReadOnlyList<InventoryItem> Weapons` | Possiede `isSavingHp`. Le armi arrivano dal genitore (inventario condiviso, §4). Usa `CharacterView` + `CharacterCalculations`. |
| `CharacterItemsTab` | inventario CRUD + denaro + sintonie (`:363-605`) | `Character`, `CanEdit`, `IReadOnlyList<InventoryItem> Items`, `EventCallback OnInventoryChanged` | Inietta `SupabaseService` per il CRUD; la **lista resta del genitore** (§4). Possiede draft/flag locali, `FormatMoney`, attunements. |
| `CharacterMagicTab` | stats incantesimo, slot, incantesimi noti via `SpellPicker` (`:608-694`) | `Character`, `CanEdit`, `OnChanged`, `IReadOnlyList<Spell> AllSpells` | Inietta `SupabaseService` per `character_spells` (privato, §4); slot = campi scalari → `OnChanged`; catalogo `AllSpells` dal genitore. |

## 4. Dati oltre i campi di `Character`: inventario (condiviso) vs incantesimi (privato)

Due tab gestiscono tabelle separate, ma con un'**asimmetria emersa leggendo il codice** (importante per non
rompere il comportamento):

- **Inventario (`inventory`) è CONDIVISO.** Il Combat tab legge le **armi** dall'inventario
  (`Characters.razor:119`), mentre l'Items tab ne fa il CRUD. Perciò la **lista `inventoryItems` resta di
  proprietà del genitore** (caricata alla selezione del PG, come oggi). `CharacterItemsTab` inietta
  `SupabaseService`, fa il CRUD e poi invoca `EventCallback OnInventoryChanged` → il genitore **ricarica** la
  lista e ri-renderizza, così il Combat tab vede le armi aggiornate. `CharacterCombatTab` riceve il
  sottoinsieme `Weapons` (sola lettura), derivato dal genitore.
- **Incantesimi (`character_spells`) è PRIVATO del Magic tab.** Nessun altro tab li legge. `CharacterMagicTab`
  inietta `SupabaseService` e possiede la collezione end-to-end (lista, loading in `OnParametersSetAsync` al
  cambio di `Character`, add/remove/toggle-prepared). Riceve dal genitore il catalogo globale `AllSpells`
  (già caricato per il form). Gli slot incantesimo sono campi scalari di `Character` → mutati con `OnChanged`.

Migrazione futura (sotto-fase A): le iniezioni di `SupabaseService` passeranno alle interfacce repository
(`IInventoryRepository` / `ICharacterSpellRepository`) senza cambiare la superficie dei componenti.

## 5. Flusso dati e re-render

- Il genitore possiede `selected` (`Character?`) e lo passa come `Character` ai tab.
- Modifica a un campo scalare: il figlio muta `Character.<campo>` e invoca `OnChanged` → il genitore esegue
  `SaveCharacterAsync` (persistenza + toast) e ri-renderizza.
- Le sotto-collezioni (Items/Magic) si auto-gestiscono: il figlio chiama `StateHasChanged` dopo le proprie
  operazioni; non passano dal genitore.
- Nessun nuovo meccanismo di stato condiviso (niente cascading/servizi nuovi) → impianto minimale, YAGNI.

## 6. Slicing e verifica (dettaglio nel piano)

**Un tab per task.** Ogni task: estrai il componente → sostituisci il blocco in `Characters.razor` con
`<CharacterXTab … />` → sposta i metodi/campi pertinenti → `dotnet build -c Debug` pulito → `dotnet test`
(62 verdi, non-regressione) → **verifica manuale** su `https://localhost:7076` (il tab rende e si comporta
identico, sia da owner sia da non-owner per `CanEdit`) → commit.

Ordine: **Bio** (pattern-setter pulito, solo campi `Character` + note) → **Stats** (banale) → **Combat**
(interazioni su scalari) → **Items** → **Magic** (sotto-collezioni). Deploy dopo ogni tab verificato oppure a
fine giro (scelta dell'utente; `Characters.razor` è una sola pagina → raggio d'impatto contenuto, ma è la
feature principale → verifica locale **prima** di ogni push, come da regola di prudenza).

## 7. Testing

Nessun unit test nuovo in questa fase: i componenti `.razor` non sono testabili con xUnit senza bUnit, che
arriva con le interfacce della sotto-fase A. La rete di sicurezza è: **build pulita** + i **62 test esistenti**
(guardia su `CharacterCalculations`, che i tab continuano a usare) + la **prova manuale per-tab**.

## 8. Rischi e mitigazioni

- **Deriva di comportamento durante l'estrazione** → verifica manuale per-tab + test di non-regressione; un tab
  per commit, così un eventuale problema è isolato e revertibile.
- **Sincronizzazione di stato** (il figlio muta l'oggetto del genitore) → `OnChanged` forza il re-render del
  genitore; i tab a sotto-collezione fanno `StateHasChanged` autonomo. Pattern standard Blazor, già usato da
  `StatCard`.
- **Loading delle sotto-collezioni** → ricaricare in `OnParametersSetAsync` al cambio di `Character`, per non
  perdere il comportamento attuale (caricamento alla selezione).
- **CSS isolato (importante).** Gli stili in `Characters.razor.css` sono *scoped* alla pagina: spostando il
  markup di un tab in un figlio, quelle regole **smettono di applicarsi** (CSS isolation di Blazor) → aspetto
  rotto. **Decisione:** ogni componente porta con sé il proprio `*.razor.css`, dove si **spostano** (taglia e
  incolla) le regole di quel tab da `Characters.razor.css`, **senza toccare i selettori** → stesso aspetto, ora
  correttamente isolato nel figlio. Lo spostamento del blocco CSS fa parte del task del relativo tab; ciò che
  resta condiviso (layout della tab-bar, token) rimane nel foglio della pagina o in `app.css`.
