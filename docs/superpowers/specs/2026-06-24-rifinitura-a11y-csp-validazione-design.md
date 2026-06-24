# Spec — Rifinitura UX/a11y + CSP + validazione form

> Stato: **design approvato** (2026-06-24). Tre interventi indipendenti dal backlog DA-FARE,
> raggruppati in un solo /loop. Brainstorming → questa spec → piano (`writing-plans`) → implementazione.
> Coprono: §5/§6 (spinner + a11y), §1 (CSP), §1 (validazione di dominio lato client).

## 1. Obiettivo e confini

Tre rifiniture a basso/medio rischio, ognuna completabile da sola, da fare **in sequenza 1 → 3 → 2**
(la CSP per ultima, è la più delicata):

1. **UX/a11y dei cataloghi** — sostituire i "Caricamento…" testuali rimasti con lo spinner a tema esistente e
   dare un nome accessibile ai pulsanti a sola icona.
2. **CSP** — aggiungere una Content-Security-Policy in `<meta>` (unica via su GitHub Pages).
3. **Validazione dati nei form** — controlli di dominio lato client con messaggi chiari, dove mancano.

**Fuori scope:** virtualizzazione/memoizzazione delle liste (valutata e **scartata**: cataloghi < ~50 voci →
nessun beneficio percepibile; rivalutare solo se cresceranno, es. con import massivo/AI); vincoli a livello DB
(NOT NULL/CHECK SQL — serve accesso alle migrazioni Supabase, resta annotato in §1 di DA-FARE);
header di sicurezza veri (`frame-ancestors`, `report-uri` — non ottenibili via `<meta>`).

**Criterio di successo:** dopo ogni step, build 0/0 + 111 test verdi (più gli eventuali nuovi); verifica in
locale su `https://localhost:7076`; per la CSP anche login Google + un CRUD funzionanti senza violazioni in console.

## 2. Decisioni (dal brainstorming)

- **Spinner:** riuso `Shared/LoadingSpinner.razor` (già usato in Combat/CharacterItemsTab), con testo per-pagina.
- **a11y FAB:** aggiungo `aria-label` ai 6 pulsanti "+" mantenendo il `title` (tooltip). Niente restyling.
- **CSP — severità:** _scelta iniziale_ hash SHA-256 sugli script inline. **AGGIORNAMENTO in implementazione
  (2026-06-24): approccio a hash ABBANDONATO** — .NET inietta un `<script type="importmap">` auto-generato il cui
  contenuto (fingerprint asset + integrity) cambia ad ogni build → un hash fisso si romperebbe ai rilasci. Adottata
  **CSP pragmatica**: `script-src 'self' 'unsafe-inline' 'wasm-unsafe-eval'` + direttive restrittive
  (connect-src solo Supabase, object-src 'none', base-uri 'self'). `'wasm-unsafe-eval'` obbligatorio per Blazor WASM.
- **Validazione:** lato **client** sui form che ne mancano (soprattutto Mostri); i Personaggi sono già coperti da
  `CharacterNormalizer` (clamp testato) e **non** si toccano.

## 3. Step 1 — UX/a11y (markup)

- Sostituire `<span>Caricamento...</span>` con `<LoadingSpinner Text="…" />` in: `Pages/Classes.razor`,
  `Pages/Monsters.razor`, `Pages/Notes.razor`, `Pages/Races.razor`, `Pages/Spells.razor`. Testo coerente con la
  pagina ("Caricamento incantesimi…", "…mostri…", "…classi…", "…razze…", "…note…").
- Aggiungere `aria-label` ai FAB "+" in: Spells, Races, Notes, Classes, Characters, Monsters (stesso testo del
  `title`, es. `aria-label="Nuovo incantesimo"`).
- **Nessun cambiamento visivo** atteso (lo spinner sostituisce solo lo stato di loading testuale).

## 4. Step 3 — Validazione form (logica + UI)

Regole di dominio, con messaggio nel banner `errorMessage` (stesso pattern dei form attuali), prima del salvataggio:

- **Mostri (`Pages/Monsters.razor`):** nome obbligatorio (già); 6 punteggi caratteristica in **1–30**; CA in **0–40**.
  Messaggi tipo "I punteggi di caratteristica devono essere tra 1 e 30", "La CA deve essere tra 0 e 40".
- **Incantesimi (`Pages/Spells.razor`):** già valida nome + livello 0–9 → invariato (verifica di coerenza).
- **Classi/Razze:** nome obbligatorio + rispetto `maxlength` (allineamento; verificare cosa manca leggendo i form).
- **Personaggi:** invariati (coperti da `CharacterNormalizer`).
- **Logica testabile:** estrarre le regole numeriche riusabili in un helper puro
  `Services/FormValidation.cs` (`internal static`, esposto via `InternalsVisibleTo`) così i range sono coperti da
  unit test senza bUnit. Es. `InRange(int value, int min, int max)` e validatori specifici
  (`ValidateMonster(...) -> string?` che ritorna il primo errore o null). Il `.razor` chiama l'helper e mostra il messaggio.

## 5. Step 2 — CSP in `<meta>` (config + test browser)

In `wwwroot/index.html`, nel `<head>`, una `<meta http-equiv="Content-Security-Policy" content="…">` con direttive:

```
default-src 'self';
script-src 'self' 'wasm-unsafe-eval' 'sha256-…' 'sha256-…' 'sha256-…' ['sha256-<empty>' per l'importmap se serve];
style-src 'self' 'unsafe-inline';
img-src 'self' data:;
font-src 'self';
connect-src 'self' https://tbgjwtfmijrcmeracfzh.supabase.co;
object-src 'none';
base-uri 'self';
manifest-src 'self';
worker-src 'self';
```

- **Hash:** calcolare lo SHA-256 (base64) del **contenuto esatto** di ciascuno dei 3 `<script>` inline di
  `index.html` (SPA redirect, PWA update, repairApp) e, se necessario, dell'`<script type="importmap">` vuoto.
  Documentare in un commento HTML accanto alla meta **come ricalcolarli** se quegli script cambiano.
- **`worker-src 'self'`:** il service worker. **`manifest-src 'self'`:** il webmanifest.
- ⚠️ `frame-ancestors`/`report-uri` omessi: ignorati nel `<meta>` (solo header). Annotato in DA-FARE §1.
- **Caveat noto:** se Blazor/WASM richiedesse a runtime un permesso non previsto (es. un blob/worker), emergerà
  come violazione in console → si aggiunge la direttiva minima necessaria durante la verifica locale.

## 6. Verifica locale (per step)

- **Step 1:** ogni catalogo mostra lo spinner a tema durante il caricamento; l'albero di accessibilità (DevTools)
  espone il nome dei FAB ("Nuovo …").
- **Step 3:** nel form Mostri, valori fuori range (es. FOR 99, CA 99) → messaggio chiaro, niente salvataggio;
  valori validi → salva. Unit test dei range verdi.
- **Step 2:** caricare l'app su `https://localhost:7076` → **nessuna** violazione CSP in console; **login Google**
  completo; un **CRUD** (es. crea/elimina un incantesimo) ok; reload ok (service worker/manifest non bloccati).

## 7. Rischi

- **CSP rompe Blazor/OAuth/Supabase:** è il rischio principale; mitigato dalla verifica locale con console aperta
  (login + CRUD) **prima** di considerare lo step fatto. Per questo è l'ultimo step.
- **Hash da ricalcolare:** modifiche future ai 3 script inline richiedono nuovo hash, altrimenti schermata bianca;
  mitigato dal commento-guida in `index.html`. Gli script sono stabili.
- **Validazione client ≠ server:** resta un controllo UX, non sicurezza (l'autorità è RLS + futuri CHECK DB);
  esplicitato.

## 8. Ordine e gating

1 (UX/a11y) → 3 (validazione, con unit test) → 2 (CSP, con verifica login/CRUD). Build + test verdi dopo ognuno.
**Niente commit/push** finché tutti e tre non sono fatti e verificati in locale, e comunque solo su ok esplicito dell'utente.
