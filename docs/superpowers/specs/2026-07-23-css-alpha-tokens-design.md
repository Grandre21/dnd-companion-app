# §6 — Token per i colori con opacità (rgba) — design

> Data: **2026-07-23**. Chiude il residuo di §6 (design token) in `docs/DA-FARE.md`:
> i literali `rgba()` con opacità non avevano un token diretto.

## Problema
I 376 colori **hex** erano già stati convertiti in design token (`:root`, `wwwroot/css/app.css`).
Restavano i literali `rgba()` con opacità, non tokenizzati, nel CSS del progetto (`app.css` + gli scoped
`.razor.css`). Un token hex (`--gold: #d4a574`) non è usabile in `rgba(var(--gold), α)`: `var(--gold)`
restituisce la stringa hex, non i canali numerici richiesti da `rgba()`.

> **Nota di misura.** Il conteggio grezzo `rgba()` sui `.css` (~3000) era gonfiato dalle copie in
> `bin/obj` e da **Bootstrap vendored** (`wwwroot/lib/`). Il perimetro reale è il **CSS sorgente del
> progetto**: **~363 occorrenze** su **19 triple distinte**. Le 3 triple in più che comparivano nel conteggio
> grezzo — blu/grigi Bootstrap `13,110,253` / `33,37,41` / `222,226,230` — esistono **solo** nel vendored e
> restano fuori scope (Bootstrap non si modifica).

## Decisione
**Canali RGB affiancati ai token hex** + `rgba(var(--X-rgb), α)`. Scelto rispetto a
`color-mix()`/relative-color per:
1. **Supporto browser universale** (anche WebView Android datati / Safari vecchi) — zero rischio di colori rotti.
2. **Trasformazione meccanica e visivamente invariante** — cruciale su centinaia di occorrenze.
3. Token hex esistenti **intatti**.

Sweep su tutte le triple del progetto, **mapping 1:1** — nessun consolidamento delle sfumature vicine.

## Token (in `:root` di `wwwroot/css/app.css`)

| Famiglia | Token | Tripla | Usi |
|---|---|---|---|
| Oro/bronzo | `--gold-rgb` | 212,165,116 | 216 |
| | `--gold-muted-rgb` | 154,140,106 | 7 |
| | `--bronze-rgb` | 139,110,58 | 2 |
| | `--gold-light-rgb` | 240,220,176 | 1 |
| | `--taupe-rgb` | 120,110,90 | 1 |
| | `--bronze-dark-rgb` | 100,80,40 | 1 |
| Neutri | `--black-rgb` | 0,0,0 | 76 |
| | `--white-rgb` | 255,255,255 | 3 |
| | `--shadow-warm-rgb` | 13,10,8 | 1 |
| Rossi | `--danger-rgb` | 196,86,56 | 25 |
| | `--error-rgb` | 139,35,35 | 19 |
| | `--danger-mid-rgb` | 150,50,38 | 1 |
| | `--danger-light-rgb` | 168,70,52 | 1 |
| | `--danger-deep-rgb` | 120,38,30 | 1 |
| | `--error-dark-rgb` | 74,19,19 | 1 |
| Verdi | `--success-rgb` | 139,181,75 | 3 |
| | `--success-dark-rgb` | 107,142,58 | 2 |
| | `--success-light-rgb` | 120,170,90 | 1 |
| | `--success-darker-rgb` | 74,100,36 | 1 |

Le 6 sfumature di rosso, 4 di verde e 6 oro/bronzo riflettono un accumulo organico: **non** vengono
consolidate (sarebbe un cambio di colore → decisione separata), solo tokenizzate 1:1.

## Trasformazione
- Script **una-passata, map-driven** (perl one-off, non committato): matcha
  `rgba(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*,` e, se la tripla è in mappa, la sostituisce con
  `rgba(var(--<token>-rgb),` (alpha e parentesi preservate). Tollerante agli spazi.
- **Perimetro:** solo il CSS sorgente del progetto (`app.css` + `.razor.css`). Esclusi `wwwroot/lib/`
  (Bootstrap vendored), `bin/`, `obj/`.
- I due token già `rgba` in `:root` (`--error-bg`, `--error-border`) sono anch'essi passati a
  `var(--error-rgb)`/`var(--danger-rgb)` — coerente. L'ordine in `:root` è irrilevante (le custom property
  risolvono al momento dell'uso).
- Le custom property di `:root` (app.css) sono **globali** e raggiungono gli scoped `.razor.css`
  (ereditarietà via DOM, non lo scope del selettore) → `var(--X-rgb)` risolve anche nei componenti isolati.

## Verifica (comportamento invariato)
- Ogni `--X-rgb` vale **esattamente** la tripla rimpiazzata → colore calcolato identico al bit.
- `grep` conferma **zero** `rgba(<numeri>)` residui nel CSS di progetto (tutti → `var()`).
- Nessun token **morto**: le 3 triple solo-Bootstrap non sono state aggiunte a `:root`.
- Build Release **0/0**, `dotnet test` verde, gate a due agenti pulito, controllo a vista su `/_showroom`
  (rimandato all'utente prima del push).

## Fuori scope (follow-up)
- **Consolidamento** delle sfumature quasi-duplicate in meno token (cambierebbe i colori).
- 1 `rgba()` in `Pages/Showroom.razor` (stringa-demo di una vetrina): lasciato letterale di proposito
  (mostra il valore grezzo come documentazione).
- `rgb()` opachi: **nessuno** presente nel CSS di progetto.
