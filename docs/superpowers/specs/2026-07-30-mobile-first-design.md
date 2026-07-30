# Mobile-first totale — analisi e design

> Data: **2026-07-30** · Stato: **design approvato, implementato nella stessa sessione**
> Mandato: portare l'app «totalmente in un contesto di utilizzo per telefono».
> Metodo: lettura del CSS di progetto (8.3k righe) + **misure a runtime** su viewport 390×844 e
> 320×568 (Chrome DevTools, emulazione mobile+touch) su `/_showroom` e `/login`, le uniche pagine
> raggiungibili senza un accesso Google che solo l'utente può completare. Dove serviva misurare
> un componente di una pagina protetta (la riga del tracker, la barra di navigazione) ne è stato
> iniettato il markup reale con le classi di scope generate, così da leggere le regole vere.

---

## 0. La premessa che ribalta il mandato

L'ipotesi di partenza — «il CSS è desktop-first, va convertito» — **è falsa** ed è stata
scartata dopo la prima misura. Il CSS di progetto **è già scritto mobile-first**: le regole base
descrivono il telefono e ogni pagina ha **un solo** blocco di enhancement `@media (min-width: 641px)`
(10 pagine + `CharacterEditForm`; Login e Showroom stanno sotto `LoginLayout`). Non c'è una sola `max-width` usata come breakpoint di
layout nel codice di progetto (l'unica, `Login.razor.css:154`, è una rifinitura).

Il lavoro quindi **non è una riscrittura**. È chiudere i punti in cui il mobile-first è
*dichiarato ma non realizzato*: dove il codice per il telefono c'è ma non ha effetto, dove le
dita non arrivano, e dove il telefono paga un costo che non gli serve.

---

## 1. Finding — impatto alto

### M1 · `viewport-fit=cover` mancante mentre le `safe-area` sono già usate
`wwwroot/index.html:19` dichiara `width=device-width, initial-scale=1.0`. Senza
`viewport-fit=cover` **iOS risolve tutte le `env(safe-area-inset-*)` a 0**.
Il FAB (`app.css:206-207`) le usa già, con tanto di commento «safe-area iPhone (notch / home
indicator)»: **quel codice non ha mai avuto effetto**. In PWA standalone su iPhone il FAB cade
sotto la home indicator. Costo della correzione: una parola.

### M2 · Nessuna navigazione: ogni spostamento passa dalla Home
Già a backlog (§8-bis 🟡). Ogni pagina ha un `← Home` e la Home è l'unico smistamento: cambiare
sezione costa **2 tap + un ricaricamento dati**. Su desktop lo mitigano URL e cronologia; sul
telefono no. È l'attrito strutturale n.1 del mandato, e la voce di backlog più pertinente ad esso.

### M3 · Il layout è sbilanciato di 8px, solo su mobile
`Layout/MainLayout.razor.css:14-17` impone `padding-left: 2rem !important; padding-right: 1.5rem !important`.
Tutti e 10 i container di pagina lo annullano con `margin: -1.1rem -1.5rem 0 -1.5rem`:
−24px contro 32px a sinistra e 24px a destra, quindi il contenuto resta **8px più a destra**
del dovuto, con il bordo destro a filo schermo e il sinistro no. Il blocco `≥641px` corregge
(`-2rem`/`-1.5rem`), quindi **il difetto esiste solo sul telefono** — l'esatto contrario del
mobile-first.

### M4 · Zoom automatico di iOS al focus degli input
iOS Safari ingrandisce la pagina quando riceve il focus un campo con `font-size < 16px`, e
**non la rimpicciolisce all'uscita**. Quattro campi sotto soglia nell'app:
`Shared/SpellPicker.razor.css` (14px), `.notes-textarea` di `CharacterBioTab` (0.95rem = 15.2px),
i 5 campi `.money-field input` (0.9rem = 14.4px) e `.attunement-input` dell'inventario (13px),
più `.sr-input` della vetrina. Gli altri 90 `.input` sono già a `1rem`: il problema è
circoscritto ma vistoso.

### M5 · Tap target sotto soglia (misurati a runtime)
`.db-error-dismiss` misura **27×22** → sotto anche il minimo WCAG 2.2 AA (24×24): **è un difetto
di conformità, non una preferenza**. Gli altri, tutti sotto i 44px di riferimento pratico:
36×36 i pallini con estensione (`sc-dot`, `ds-dot`, `spell-slot-dot`, `inspiration-toggle`),
34×34 `hp-btn` e `qty-btn`, 30×30 `remove-btn`, 38×38 `prep-toggle` (cerchio 22px + `::after`), 20×20 `.skill-check`
(**72 occorrenze**) e `checkbox-row`, 15×15 `inv-eq-toggle`.
I due più toccati al tavolo sono proprio `hp-btn` (i ± dei PF in combattimento) e `.skill-check`.

### M6 · 2,4 MB di Bootstrap per due classi
`wwwroot/lib/bootstrap` contiene **22 file css/js** (bootstrap completo, grid, utilities, reboot,
tutte le varianti RTL, i bundle JS). Il service worker precacha **per estensione**
(`/\.css$/`, `/\.js$/`, `service-worker.published.js:20`): li prende **tutti**, non solo quello
linkato in pagina. Uso reale nel markup, contato per token esatti: **`btn` una volta** (in
`Pages/Login.razor`, dove però `.btn` è ridefinita per intero in `Login.razor.css`) e **`px-4`
una volta** (in `MainLayout`, dove è già inerte per via del `!important` sul padding). L'unico effetto strutturale di cui l'app vive
davvero è il `box-sizing: border-box` universale del reboot.
Sulla rete di un telefono è il costo più grosso e più inutile del bundle — per confronto,
l'intero `_framework` compresso pesa 3,45 MB.

### M7 · `100vh` in 14 punti (13 regole CSS + lo `<style>` critico di `index.html`)
Su mobile `100vh` conta anche la barra URL che si ritrae: genera scroll spurio e il "salto" del
layout allo scroll. `100dvh` è l'unità corretta.

### M8 · `<html lang="en">` su un'app interamente in italiano
`wwwroot/index.html:2`. Uno screen reader legge l'italiano con pronuncia inglese.

---

## 2. Finding — impatto medio

- **M9 · Manifest PWA povero.** Nessuna icona `purpose: "maskable"` (su Android l'icona finisce
  in un cerchietto bianco), nessuno `scope`, nessuna `shortcuts`. Per un'app "da telefono"
  l'installazione è la porta d'ingresso.
- **M10 · `touch-action: manipulation` solo su 5 elementi** → gli altri controlli conservano il
  ritardo di ~300 ms e il double-tap-zoom su alcuni browser mobili.
- **M11 · ~~`-webkit-tap-highlight-color` mai impostato`~~ — finding ritirato.** Lo imposta già il
  reboot di Bootstrap (`-webkit-tap-highlight-color: rgba(0,0,0,0)` sul `body`). Non è un difetto
  da correggere ma **una regola da preservare** rimuovendo Bootstrap (§4.2): senza, il flash grigio
  di sistema comparirebbe *a causa* di questo lavoro. Stesso discorso per
  `-webkit-text-size-adjust: 100%`.
- **M12 · `.turn-controls` (Combat) è `position: sticky; bottom: 0` senza safe-area** → in PWA
  finisce sotto la home indicator. Stesso difetto della `.fab`, e stessa causa (M1).
- **M13 · Nessuna `overscroll-behavior`** → il pull-to-refresh di Chrome Android ricarica l'app.
  In una PWA offline-first è un reset involontario nel mezzo di una sessione di gioco.
- **M14 · ~~Griglie strette~~ — finding ridimensionato dopo il conto.** `.money-editor` a 5 colonne
  dà 63px per campo su 390px: al netto del padding restano ~55px, cioè cinque cifre abbondanti
  anche portando il carattere a 16px. `.wiz-summary-grid` a 6 colonne (~52px) è di sola lettura.
  Spezzarle su più righe romperebbe l'ordine delle monete (MR MA ME MO MP) e delle sei
  caratteristiche in cambio di nulla: **non si tocca**.

---

## 3. Cosa NON è un problema (verificato, non assunto)

Elencato perché costa quanto un finding sbagliato:

- Il CSS **non** è desktop-first (§0) → nessuna riscrittura.
- **Nessun overflow orizzontale** a 390px (misurato: `scrollWidth == clientWidth == 390`).
- I 90 `.input` principali sono già a `1rem` → niente zoom iOS sul grosso dei form.
- I 53 `type="number"` danno già la tastiera numerica: `inputmode` sarebbe rifinitura, non correzione.
- I pallini con `::after` a 36px **passano** WCAG 2.2 AA (24×24): alzarli a 44 è comodità d'uso,
  non conformità. L'unico realmente non conforme è `.db-error-dismiss` (M5).

---

## 4. Decisioni

1. **Due strade per i tap target, a seconda dell'elemento.** I **pulsanti** (`hp-btn`, `qty-btn`,
   `remove-btn`, `hd-btn`, `header-btn`) si ingrandiscono davvero a 44px: cambiano di aspetto, ed
   è voluto — con un'eccezione, `wiz-step-dot`, che si ferma a 40px perché sei pastiglie da 44
   non stanno su una riga da 320px. I **pallini** (`sc-dot`, `ds-dot`, `spell-slot-dot`) tengono il pattern
   che il progetto usa già in `StatCard`: uno pseudo-elemento `::after` trasparente e assoluto che
   allarga l'area di tocco **senza toccare il layout** (commento in `StatCard.razor.css`). Gli
   `<input type="checkbox">` non accettano né l'una né l'altra strada — un input è un elemento
   rimpiazzato, `::after` non viene reso, e a 44px il quadratino nativo si deforma: restano a
   **24px**, il minimo WCAG 2.2 AA, e la strada per i 44 (avvolgerli in una `<label>` che occupi
   la cella, 72 punti di markup) va in DA-FARE.
1-bis. **Il pattern `::after` da solo non basta: va misurata la sovrapposizione.** Misurando a
   runtime è emerso che i tap target dei pallini **si accavallavano** — 7 coppie su `sc-dot`
   (`::after` 36px contro centri distanti 26px), e per costruzione anche `ds-dot` (36 contro 22)
   e `spell-slot-dot` (32 contro 18). Un'area allargata che sconfina su quella del vicino non
   allarga il bersaglio: lo sposta. Quindi: `::after` mai più grande del passo fra i centri, e
   dove serve è il **passo** ad aumentare (`gap` di `ds-dots` e `spell-slot-dots`). Su `sc-dot`
   l'area è **asimmetrica** (44×28): larga quanto il dito dove c'è spazio, alta quanto la riga
   dove non ce n'è.
2. **Bootstrap si rimuove, con un reset locale al suo posto.** Non basta togliere il link: il
   reboot fornisce `box-sizing: border-box` universale, il reset dei margini dei titoli e
   `font: inherit` sui controlli. Si scrive in `app.css` un reset minimo che replica **solo** le
   regole in gioco, e si eliminano i 22 file. La verifica a vista sulle pagine autenticate resta
   in capo all'utente (senza sessione non sono raggiungibili — v. §6).
3. **La barra di navigazione porta 5 voci**, non 9. Le destinazioni sono 9 (8 card + Combattimento):
   una barra a 9 voci su 390px darebbe celle da 43px. Si scelgono le 4 sezioni di uso continuo al
   tavolo — **Personaggi, Iniziativa, Incantesimi, Appunti** — più **Home**, che resta l'accesso
   a tutto il resto (Mostri, Classi, Razze, Background, Dati) e al cambio campagna.
   Le etichette sono quelle che l'app già usa altrove: «Iniziativa» viene dal titolo della pagina
   Combat («Tracker Iniziativa») ed è anche l'unica abbastanza corta per la cella — «Combattimento»
   troncherebbe. La barra **compare solo con una campagna attiva**: senza, le sezioni non hanno
   dati e la Home è l'unica destinazione sensata.
4. **Il `← Home` di pagina resta.** La barra lo rende ridondante, ma rimuoverlo cambia il flusso
   oltre il mandato di questo lavoro: annotato in DA-FARE come rifinitura successiva.
5. **`100dvh` senza fallback `100vh`.** `dvh` è supportata da Safari iOS 15.4+, Chrome 108+,
   Firefox 101+ (2022). Il fallback duplicherebbe 14 regole per browser che non reggono comunque
   Blazor WASM in condizioni decenti.

---

## 5. Fuori mandato (verdetto sui pending aperti)

Chiesto esplicitamente: valutare se abbia senso chiudere prima ciò che è in sospeso.

| Pending | Verdetto |
|---|---|
| **Barra di navigazione + cache cataloghi** (§8-bis 🟡) | **Si chiude qui** la barra: è mobile-first puro, non un lavoro adiacente. La *cache* dei cataloghi resta aperta — è performance dati, ha una sua invalidazione da progettare, e la barra non la richiede. |
| **`--author-badge-text` in 3 pagine** (§6) | **Chiuso qui**: `Classes`, `Races` e `Spells` usano il token, stesso valore esatto, zero cambiamento visivo. Lo stesso literal resta in `.master-placeholder` (Home) e `.inv-weight` (inventario), che badge non sono. |
| **RLS "campaign hopping"** (§1 🟡) | **Non qui.** È il gate di pubblicazione e la finestra è effettivamente adesso, ma tocca 7 tabelle di cui 6 in produzione, diverge da policy già rilasciate e il documento stesso chiede conferma esplicita. Mescolarla a un lavoro di UI renderebbe entrambi i diff illeggibili. È la cosa da fare **subito dopo**. |
| **Verifica manuale end-to-end della Fase 2** | **Non chiudibile da qui**: richiede due account reali su Supabase. Resta aperta. |
| **Virtualizzazione liste** (§5 🟡) | **Non qui.** Pertinente al telefono, ma `<Virtualize>` su card espandibili ad altezza variabile è il caso ostico già dichiarato: merita il suo design, non un ritaglio. |
| **Fase 3 modello 2024** | Fuori tema, e con una decisione aperta sul formato (`PackageSpeed.Value` intero) che non c'entra col mobile. |

---

## 6. Limite di verifica dichiarato

`AuthRedirect` porta al login qualunque rotta senza sessione, e il login è OAuth Google: va
fatto dall'utente, non è automatizzabile da qui. (In locale funziona — gli URL di localhost sono
negli allowlist dei Redirect URLs dal 2026-06-21 — ma serve comunque una persona che completi
l'accesso.) Le pagine verificabili a vista in questa sessione sono quindi **solo** `/_showroom`
e `/login`. Lo showroom è stato costruito come banco
di lavoro proprio per questo (backlog §B) e copre palette, tipografia, bottoni, form, card,
banner, `StatCard`, `SpellListItem`, FAB, empty state — abbastanza per validare il reset di §4.2.
**Resta all'utente** la verifica a vista, da telefono e loggato, delle pagine dati.
