# Spec — Import mostri nel combattimento

> Stato: **design approvato** (2026-06-24). Feature emersa dall'uso (DA-FARE §8).
> Brainstorming → questa spec → piano (`writing-plans`) → implementazione.

## 1. Obiettivo e confini

Oggi `Pages/Combat.razor` permette al Master solo di **importare i personaggi** della campagna come combattenti
(`ImportCharactersAsync`). Questa feature aggiunge l'**import dei mostri** creati nella tab Mostri: il Master può
sceglierne uno o più, e aggiungere **più copie dello stesso** mostro; il ritocco fine dei parametri avviene **dopo**
l'import coi controlli inline già presenti (iniziativa editabile, PF +/−, rimozione del combattente).

**Obiettivo:** un pannello inline "Importa mostri" che, dai mostri della campagna, crea `Combatant` e li aggiunge
al tracker, persistendo lo stato.

**Fuori scope:** editing per-istanza nel dialog (si usa la UI inline esistente); tirare i PF dai dadi; modifiche a
schema DB/RLS (i `Combatant` vivono già nel jsonb `combatants` di `combat_state`); il combat in Realtime.

**Criterio di successo:** il Master apre "Importa mostri", imposta le quantità, aggiunge → i combattenti compaiono
nel tracker con nome (numerato per le copie) e PF derivati; lo stato è persistito; build 0/0 + test verdi.

## 2. Decisioni (dal brainstorming)

- **Ritocco parametri:** dopo l'import, coi controlli inline esistenti (dialog di import semplice).
- **PF all'import:** primo intero nel testo PF del mostro (`Monster.HitPoints` è testo libero, es. `"45 (6d8+18)"`
  → 45); fallback **1** se non c'è un numero. Stesso valore per `CurrentHp` e `MaxHp`.
- **UI:** pannello **inline** a comparsa nei controlli del Master (stesso pattern del form "Aggiungi combattente"),
  non una modale.
- **Iniziativa:** 0 all'import (coerente con l'import PG; il Master la imposta dopo).
- **Nome copie:** quantità 1 → `Name`; quantità > 1 → `"{Name} {i}"` (es. "Goblin 1", "Goblin 2"). Nessuna
  deduplica contro i combattenti già presenti (il Master rimuove/ri-aggiunge se vuole).

## 3. Logica pura — `Services/CombatImport.cs` (testabile)

Helper statico senza I/O, così la logica è coperta da unit test (coerente con la suite attuale):

```csharp
public static class CombatImport
{
    // Primo intero nel testo; fallback 1 (mai < 1). Es. "45 (6d8+18)" -> 45, "" -> 1, "n/a" -> 1.
    public static int ParseLeadingHp(string? hitPointsText);

    // q copie di un Combatant dal mostro: Name (numerato se q>1), Initiative=0, CurrentHp=MaxHp=ParseLeadingHp(m.HitPoints).
    // q <= 0 -> sequenza vuota.
    public static IEnumerable<Combatant> FromMonster(Monster monster, int quantity);
}
```

- `ParseLeadingHp`: prima sequenza di cifre (regex `\d+`); se assente o non parsabile → 1; clamp a ≥ 1.
- `FromMonster`: per `i` da 1 a `quantity` crea un `Combatant` (nuovo `Id` Guid di default). Nome: `quantity == 1`
  → `monster.Name`; altrimenti `$"{monster.Name} {i}"`.

## 4. UI — modifiche a `Pages/Combat.razor`

- **Inietta** `IMonsterRepository`.
- **Stato locale:** `bool showMonsterImport`, `List<Monster> campaignMonsters`, `Dictionary<string,int> monsterQty`
  (per id mostro), `bool isLoadingMonsters`, `bool monstersLoaded`.
- **Controlli Master:** nuovo pulsante "👹 Importa mostri" accanto a "Importa personaggi", che fa toggle del pannello;
  alla prima apertura carica i mostri (lazy) e azzera le quantità.
- **Pannello** (dentro `@if (CurrentUser.IsMaster)`):
  - loading → `LoadingSpinner`; nessun mostro → "Nessun mostro nella campagna";
  - per ogni mostro: nome + PF derivato (anteprima) + stepper quantità (riusa stile `qty-btn`/`qty-value`);
  - pulsante "Aggiungi N combattenti" (N = somma quantità), **disabilitato** se N = 0.
- **Conferma import:** `AddSelectedMonstersAsync()` — per ogni mostro con qty > 0 fa
  `combatants.AddRange(CombatImport.FromMonster(m, qty))`, poi `await SaveCombatStateAsync()`, chiude il pannello e
  azzera le quantità. Master-only; errori → `errorMessage` (banner), come `ImportCharactersAsync`.

## 5. Errori e casi limite

- Nessun mostro nella campagna → messaggio nel pannello (nessun crash).
- Tutte le quantità a 0 → pulsante disabilitato (no-op).
- `Monster.HitPoints` senza numero / vuoto → PF = 1.
- Errore caricamento mostri o salvataggio → `errorMessage` nel banner; `isLoadingMonsters`/`isImporting` resettati.
- Non-master → azioni no-op (gate come il resto del Combat).

## 6. Test (xUnit, puri su `CombatImport`)

- `ParseLeadingHp`: `"45 (6d8+18)"`→45, `"7"`→7, `"  12 hp"`→12, `""`/`null`/`"n/a"`→1.
- `FromMonster`: quantità 1 → un combattente con nome semplice; quantità 3 → tre con nomi numerati e stesso PF;
  PF derivato da `HitPoints`; fallback PF=1; quantità 0/negativa → vuoto; `Initiative == 0`.

Nessun bUnit (la UI resta non testata a unità; rete di sicurezza: build + test puri + verifica locale).

## 7. Verifica locale

Su `https://localhost:7076` come Master con almeno un mostro nella campagna: aprire Combat → "Importa mostri" →
impostare quantità (incl. >1 di uno stesso) → Aggiungi → i combattenti compaiono con nomi numerati e PF corretti →
ritoccare iniziativa/PF inline → ricaricare (altro client/giocatore) e vedere lo stato persistito.

## 8. Rischi

- **PF da testo libero:** il parsing del primo intero copre il formato comune `"45 (...)"`; formati anomali → PF 1
  (il Master corregge). Accettato.
- **Naming copie** non deduplica contro combattenti esistenti: possibile "Goblin 1" duplicato se importati in due
  tornate; accettabile (il Master rimuove i doppioni). 
- **Coerenza con import PG:** stesso pattern (Initiative 0, persistenza via `SaveCombatStateAsync`), così il
  comportamento è prevedibile.
