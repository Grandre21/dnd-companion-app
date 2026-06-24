# Spec — Wizard di creazione scheda PG

> Stato: **design approvato** (2026-06-24). Feature da DA-FARE §8 ("Redesign del flusso scheda / wizard").
> Brainstorming → questa spec → piano (`writing-plans`) → implementazione.

## 1. Obiettivo e confini

Oggi creare e modificare un personaggio passano dallo **stesso** form: `Shared/CharacterTabs/CharacterEditForm.razor`,
un accordion a 7 sezioni aperto da `Pages/Characters.razor` (`ViewMode.Form`). Il dolore di §8 è la **prima creazione**:
foglio bianco, nessuna scaletta, e i bonus derivati (razza, classe) sono **mostrati ma non applicati** — l'utente li
ricopia a mano nelle caratteristiche, mette le competenze dei tiri salvezza, calcola i PF.

**Obiettivo:** un **wizard guidato di sola creazione** che porta a un PG subito giocabile in pochi step, applicando
automaticamente i bonus derivati puliti (numerici) e suggerendo quelli da testo libero.

**Scope (deciso nel brainstorming):**
- **Solo creazione.** La **modifica** di un PG esistente resta sull'accordion attuale (per i ritocchi mirati è più
  veloce: si salta dritti alla sezione). Vedi tabella entry point in §3.
- **Automazione intermedia.** Applica in automatico ciò che è numerico/pulito (bonus razza → caratteristiche; dado vita
  pre-compilato dal dado-vita della classe + livello); suggerisce con un tap ciò che è semi-automatico (PF massimi) o da
  testo libero (tiri salvezza della classe). Niente parsing fragile applicato d'ufficio.
- **Velocità non auto-applicata** (vedi §10): il catalogo Razze memorizza la velocità in una scala (0–120, default 30,
  di fatto piedi) **incoerente** con la velocità del PG (in metri, default 9). Come l'accordion attuale, il wizard
  **mostra** la velocità della razza come info ma **non** la scrive nel PG. Normalizzare le unità è fuori scope.
- **Caratteristiche: a mano + aiuto array standard.** Stepper +/− con modificatore dal vivo, più un pulsante opzionale
  "Carica array standard" (15,14,13,12,10,8). Niente point buy (YAGNI per il gruppo attuale).
- **Ampiezza essenziale.** Il wizard copre il minimo per un PG giocabile; aspetto/storia, denari, difese, sintonie si
  aggiungono **dopo** dall'accordion/tab.

**Fuori scope:** modifiche a schema DB e RLS (**zero**); point buy; tiro dei dadi per le caratteristiche; sostituzione
dell'accordion per l'edit; bUnit/test dei componenti; i18n.

**Criterio di successo:** dal "+"/"Crea Personaggio" parte il wizard; scegliendo razza i bonus caratteristica si
applicano da soli e scegliendo classe il dado vita si pre-compila; PF e tiri salvezza si suggeriscono con un tap; al
"Salva" il PG nasce giocabile e si apre la sua scheda. Build 0/0, test (helper puri) verdi, verifica a vista in locale.

## 2. Decisioni (dal brainstorming)

| Tema | Decisione |
|---|---|
| Ruolo del wizard | **Solo creazione**; edit invariato sull'accordion (opzione 1). |
| Livello di automazione | **Intermedio**: numerico applicato in auto, testo libero suggerito. |
| Caratteristiche | **A mano + "Carica array standard"** (no point buy). |
| Ampiezza | **Essenziale → PG giocabile** (6 step, no sezioni non urgenti). |
| Architettura | **Ibrido (C)**: wizard a sé per struttura/navigazione; automazioni in **helper puri testabili**. |

## 3. Architettura e punto d'ingresso

**Entry point — cambio minimo in `Pages/Characters.razor`:** nuovo `ViewMode.Wizard`.

| Azione | Prima | Dopo |
|---|---|---|
| "+" FAB / "Crea Personaggio" (`OpenCreateForm`) | accordion (`ViewMode.Form`) | **wizard** (`ViewMode.Wizard`) |
| ✎ Modifica PG esistente (`OpenEditForm`) | accordion | accordion (invariato) |

**Riuso della persistenza (niente duplicazione):** il genitore continua a possedere `editDraft`. `OpenCreateForm`
pre-popola `editDraft` come oggi (`OwnerId`, `CampaignId`, livello 1, CA 10, PF 10, caratteristiche a 10) e imposta
`mode = ViewMode.Wizard`. Il wizard riceve quell'`editDraft` come `Draft` e lo muta passo per passo. A fine flusso
chiama `OnSave` = **l'attuale `SaveFormAsync`** (che già valida nome/PF, chiama `CharacterNormalizer.Normalize`, crea
via `CharacterRepository.CreateCharacterAsync`, aggiorna la lista e apre la detail). `OnCancel` = **l'attuale
`CancelForm`**. L'unica modifica strutturale al genitore è aggiungere il `case ViewMode.Wizard` allo switch e far
puntare `OpenCreateForm` lì.

**Nuovi file:**

1. **`Shared/CharacterTabs/CharacterWizard.razor`** — il componente wizard.
   - Parametri: `[Parameter, EditorRequired] Character Draft`, `List<CharacterClass> Classes`, `List<Race> Races`,
     `bool IsBusy`, `EventCallback OnSave`, `EventCallback OnCancel`.
   - Stato locale: `int currentStep` (0–5); `int[] baseScores` (le 6 caratteristiche base); `bool isClassCustom`,
     `bool isRaceCustom` (stesso meccanismo dell'accordion).
   - Reset all'apertura via `OnParametersSet` + confronto `ReferenceEquals(Draft, _lastDraft)` (come fa
     `CharacterEditForm`): azzera `currentStep`, inizializza `baseScores` dai valori correnti di `Draft` (10 in
     creazione), ricalcola `isClassCustom`/`isRaceCustom`.
2. **`Services/CharacterWizardLogic.cs`** — helper puri statici (vedi §5).
3. **`Shared/CharacterTabs/CharacterWizard.razor.css`** — stile scoped (barra progresso, layout step), a tema con i
   design token esistenti.

**Estrazione condivisa (approccio C, solo se conviene):** in implementazione valuto se la **lista competenze** (6 TS +
18 abilità, blocco ~120 righe identico all'accordion) merita di diventare un componente `ProficiencyFields.razor`
riusato sia dal wizard sia dall'accordion. Se sì lo estraggo e l'accordion lo adotta (cambio localizzato, comportamento
invariato); se il costo/rischio non vale, il wizard ha il suo blocco e l'accordion resta intatto. Gli altri campi nel
wizard hanno layout diverso dall'accordion (step vs sezione; base-score vs valore unico): lì non c'è copia, è markup
diverso.

## 4. Gli step

Header: "Passo N di 6" + barra di progresso con **pallini cliccabili** (salto libero, i dati persistono nel `Draft` e
nello stato del wizard). "← Indietro" / "Avanti →"; all'ultimo step "Avanti" è sostituito da "Salva personaggio".
"Annulla" in cima → `OnCancel`. **Navigazione permissiva**: nessun blocco su "Avanti" (coerente con l'accordion).

1. **Identità** — Nome (obbligatorio, marca soft con asterisco/hint, non blocca), Classe (select catalogo ordinato +
   "Altro (testo libero)"), Sottoclasse (testo opz.), Razza (select + "Altro"), Livello (1–20), Background (testo),
   Allineamento (select, le 9 voci già presenti nell'accordion). Info inline come l'accordion: dado vita/abilità primaria
   della classe, bonus razziali + velocità della razza. Scegliere classe/razza qui alimenta le automazioni a valle.

2. **Caratteristiche** — sei righe; ogni riga mostra: **base** (stepper +/−, range 1–30) · **+ bonus razza** (sola
   lettura, dalla razza scelta) · **= totale** col **modificatore** del totale. Pulsante **"Carica array standard"**
   (riempie i base con 15,14,13,12,10,8 nell'ordine FOR→CAR, da riassegnare). Riquadro derivato: bonus competenza dal
   livello. A ogni modifica di base o cambio razza i finali `Draft.Strength…Charisma` = `FinalAbilityScores(base, race)`.

3. **Vitalità & combattimento** — Classe Armatura (number, default 10), **PF Massimi** (number) col pulsante
   **"Usa suggerito (N)"** = `SuggestMaxHp(classHitDie, modCOS, livello)`, mostrato **solo** quando il dado vita della
   classe è riconoscibile (altrimenti niente suggerimento); PF correnti impostati = max in creazione, **Velocità**
   (number, dal default del `Draft`; la velocità della razza è solo info, non applicata — vedi §1/§10), Taglia (select,
   default "Media"), **Dado Vita** (testo, pre-compilato con `BuildHitDice(classHitDie, livello)` es. `3d12`,
   modificabile). Riquadro derivato: iniziativa, percezione passiva (come l'accordion).

4. **Competenze** — Tiri salvezza competenti: 6 checkbox + chip suggerimento **"Suggeriti dalla classe: Forza,
   Costituzione  [Applica]"** da `ParseSaveProficiencies(class.SavingThrows)` (il chip appare solo se il parse trova
   qualcosa; "Applica" spunta i relativi `ProfSave*`). Abilità competenti/expertise: la lista come nell'accordion;
   `class.SkillChoices` mostrato come hint testuale ("La classe permette di scegliere: …"). Nota su expertise senza
   competenza (come l'accordion).

5. **Incantesimi** (step sempre presente) — "Caratteristica da incantatore" (Nessuna / Intelligenza / Saggezza /
   Carisma). Se **Nessuna** → step vuoto (PG non incantatore; in scheda il tab Magic non comparirà). Se valorizzata →
   riquadro derivato CD/attacco + slot **massimi** liv 1–9 (gli "usati" si gestiscono in sessione dal tab Magic).

6. **Riepilogo & salva** — card di sola lettura dei campi chiave (nome, classe/sottoclasse, razza, livello;
   caratteristiche con modificatori; CA, PF, velocità; tiri salvezza e abilità competenti; incantatore sì/no) +
   **"Salva personaggio"** (disabilitato se `IsBusy`) e "← Indietro". Al salva → `OnSave` (= `SaveFormAsync`).

## 5. Logica pura — `Services/CharacterWizardLogic.cs` (testabile)

Helper statici senza I/O (stesso pattern di `CharacterCalculations`/`CharacterNormalizer`/`FormValidation`):

```csharp
public static class CharacterWizardLogic
{
    // Finali = base + bonus razza (per le 6 caratteristiche, ordine FOR,DES,COS,INT,SAG,CAR), clamp 1..30.
    // race null -> restituisce i base (clampati). baseScores deve avere 6 elementi.
    public static int[] FinalAbilityScores(int[] baseScores, Race? race);

    // "d12" + livello 3 -> "3d12". Dado vuoto/non riconosciuto -> "" (l'utente scrive a mano). livello < 1 trattato 1.
    public static string BuildHitDice(string? classHitDie, int level);

    // PF suggeriti, metodo medio 5e: liv 1 = dado pieno + modCOS; ogni livello oltre += (media del dado, arrotondata
    // per eccesso) + modCOS. Minimo 1. Dado NON riconosciuto -> 0 (sentinella: la UI nasconde "Usa suggerito").
    public static int SuggestMaxHp(string? classHitDie, int conModifier, int level);

    // Mappa il testo libero dei tiri salvezza sulle chiavi caratteristica ("strength","dexterity",...). Tollerante a
    // maiuscole/spazi/separatori (virgola). Testo non riconosciuto -> lista vuota (nessun falso positivo).
    public static IReadOnlyList<string> ParseSaveProficiencies(string? savingThrowsText);
}
```

- Il **modificatore COS** per `SuggestMaxHp` si ricava da `CharacterCalculations.GetModifier(Draft.Constitution)` (già
  esistente), passando il **totale** (base + bonus razza).
- Parsing dado vita: estrae l'intero dopo `d`/`D` (es. `d12`, `D8`, `1d6` → 12/8/6); robusto a spazi.
- `ParseSaveProficiencies`: split su virgola, trim, lowercase, match sui nomi italiani delle caratteristiche
  (Forza/Destrezza/Costituzione/Intelligenza/Saggezza/Carisma) → chiavi inglesi usate per spuntare i `ProfSave*`.

**Caratteristiche base — scelta deliberata.** Il model `Character` salva solo i valori **finali** (`Strength` ecc.), non
i base. I base vivono **solo nello stato locale del wizard**: impalcatura per applicare/togliere i bonus razza in modo
**idempotente** (cambio razza A→B → i totali si ricalcolano, niente bonus sommati due volte). Dopo la creazione i base
si scartano (l'accordion modifica i finali direttamente; D&D non richiede i base post-creazione). **Niente nuove colonne
DB → nessun impatto su schema/RLS.**

## 6. Flusso dati e salvataggio

- Il genitore possiede `editDraft`; il wizard lo muta scrivendo sempre i **finali** dentro `Draft`, così a qualunque
  step `Draft` è coerente e il **Riepilogo** legge da lì.
- All'apertura `baseScores` si inizializza dai valori di `Draft` (10 in creazione) e `currentStep = 0`.
- Ogni modifica a base-score o razza ricalcola i finali via `FinalAbilityScores` e li riscrive in `Draft`.
- Al "Salva personaggio" → `OnSave` = `SaveFormAsync` esistente: valida (nome obbligatorio, PF max ≥ 1, clamp PF
  correnti ≤ max), `CharacterNormalizer.Normalize(editDraft)`, `CreateCharacterAsync`, aggiorna `characters` e apre la
  detail. **Nessuna logica di salvataggio nuova.**

## 7. Errori e validazione (riuso del pattern esistente)

- Validazione al **Salva**, dentro `SaveFormAsync`: nome obbligatorio e PF max ≥ 1 → `Toasts.ShowError` (toast
  `.app-toast`); PF correnti clampati. `CharacterNormalizer` fa da rete (trim/clamp, incl. caratteristiche 1–30).
- Errori di rete/salvataggio → `try/catch` del genitore → `errorMessage` nel `DbErrorBanner` ("Ripara e ricarica"),
  identico a oggi.
- Casi limite: razza/classe non selezionate → nessuna automazione applicata, campi a default (PG comunque salvabile col
  nome); classe "Altro" senza dado vita → `BuildHitDice` = "" e `SuggestMaxHp` = 0 → niente pulsante "Usa suggerito";
  `SavingThrows` vuoto/non riconosciuto → nessun chip suggerimento.

## 8. Test (xUnit, puri su `CharacterWizardLogic`)

Nessun bUnit (UI non testata a unità, coerente con la suite attuale; rete: build + test puri + verifica locale).

- `FinalAbilityScores`: razza null → base invariati; bonus positivi/negativi sommati nel giusto ordine; clamp a 30
  (base 30 + bonus → 30); array di lunghezza errata gestito (difensivo).
- `BuildHitDice`: `d12`+3 → `3d12`; `D8`+1 → `1d8`; `1d6`+5 → `5d6`; dado vuoto/"custom" → ""; livello < 1 → trattato 1.
- `SuggestMaxHp`: liv 1 d12 modCOS+2 → 14; multilivello (media arrotondata per eccesso, es. d12 liv 3 modCOS+1 →
  12+7+7+3 = 29); modCOS negativo; minimo 1; dado non riconosciuto → 0 (sentinella).
- `ParseSaveProficiencies`: "Forza, Costituzione" → ["strength","constitution"]; maiuscole/spazi extra; voce ignota →
  scartata; stringa vuota/null → vuoto; tutte e sei.

## 9. Verifica locale

Su `https://localhost:7076` (login abilitato), come membro di una campagna con almeno una razza e una classe a catalogo:
"+"/"Crea Personaggio" → scegliere classe e razza e verificare che caratteristiche (bonus razza) e dado vita si
applichino e che "Usa suggerito" dia i PF attesi (la velocità della razza è solo info, non scritta nel PG);
"Carica array standard"; chip "Applica" sui tiri salvezza; impostare un incantatore e verificarne CD/slot; Riepilogo
coerente → Salva → si apre la scheda con i valori giusti; il tab Magic compare solo se incantatore. Provare anche:
niente razza/classe (salvataggio col solo nome), classe "Altro".

## 10. Rischi

- **Suggerimento PF/tiri salvezza da catalogo homebrew:** se il testo è non standard, dado vita "" e nessun chip; non si
  rompe nulla, l'utente compila a mano. Accettato (è il senso dell'automazione "intermedia").
- **Base scores non persistiti:** voluto; chi modifica dopo lavora sui finali (accordion). Documentato nello spec.
- **Estrazione `ProficiencyFields`:** se fatta, tocca l'accordion funzionante → cambio localizzato, comportamento
  invariato, coperto da verifica a vista. Se rischioso, si rinuncia (duplicazione minore accettata).
- **Coerenza salvataggio:** riuso integrale di `SaveFormAsync`/`CharacterNormalizer`, così il comportamento di create è
  identico a oggi.
- **Unità velocità razza/PG incoerenti (pre-esistente):** Razze in scala 0–120 (default 30, piedi), PG in metri
  (default 9). Il wizard non auto-applica la velocità (la mostra come info, come l'accordion), evitando di scrivere
  "30 metri". La normalizzazione delle unità è un debito separato, fuori scope di questa feature.
