# La vista di gioco — design (2026-08-08)

## Il problema, nelle parole di chi lo ha

> «Tutta la parte del personaggio — non la creazione, la gestione — è fatta bene ma ti chiedo un
> nuovo design più completo: attualmente è ancora meglio usare la scheda.»

Interrogato su cosa lo faccia tornare alla carta, l'utente ha indicato tre cose e ne ha **esclusa**
una quarta. Le tre: vede tutto in una schermata; sa cosa fanno le sue abilità; sa a colpo d'occhio
cosa può fare ora. La quarta, esclusa: «aggiornare è più veloce a matita».

Quell'esclusione è il dato che orienta tutto il progetto. **L'input dell'app va bene: è la lettura
che fallisce.** Non serve rendere più veloce nessuna scrittura; serve mettere sotto gli occhi, nel
momento del turno, informazione che oggi l'app possiede ma disperde.

## Il documento che ha riaperto il problema

`character-sheet.pdf` (root del repo, non versionato) è la scheda cartacea in uso: Grunnok Baldus,
Barbaro 5 Berserker, Orco, background Soldato. Il riquadro CLASS FEATURES **è compilato a mano, con
parole dell'utente**:

> Ira (Rage): 3/Riposo Lungo. +2 Danni. Resistenza (Contundente, Perforante, Tagliente). Vantaggio Forza.
> Attacco Temerario: Vantaggio ai tuoi attacchi FOR, ma i nemici hanno vantaggio contro di te.
> Conoscenza Primordiale: Usa FOR al posto di altre statistiche per (acrobazia, intimidire,
> percezione, stealth, sopravvivenza) in Ira.
> Frenesia (Frenzy): In Ira + Attacco Temerario, primo colpo infligge +xd6 danni.

Da qui due fatti che cambiano il perimetro del lavoro.

**Primo: le descrizioni ufficiali non servono.** Il punto era considerato bloccato sull'assenza del
PDF SRD 5.2.1 italiano (il PHB in `docs/` è fuori licenza, e inventare regole per una scheda usata al
tavolo è escluso — v. DIARIO, «La scheda apre il manuale che aveva già in casa»). Ma l'utente non
usa il testo ufficiale: usa il **proprio riassunto operativo**. Il blocco riguardava il *fornire*
descrizioni, non il *farne posto*. Con un posto dove metterle, la richiesta è interamente
realizzabile oggi.

**Secondo: l'utente annota da sé l'economia d'azione** — «*1 volta a turno, bonus action*». Quel
metadato, per chi gioca, è informazione di prima classe: è il criterio con cui si decide un turno.

## Cosa c'è oggi, e perché non basta

Quattro tab (`Pages/Characters.razor:102-120`) sotto una barra fissa con PF/CA/INI/PERC:

| Tab | Componente | Contenuto |
|---|---|---|
| Tiri | `CharacterCombatTab` (929 righe) + `CharacterStatsTab` | TS morte, velocità, ispirazione, armi, risorse a pallini, caratteristiche/abilità, dadi vita, riposi |
| Scheda | `CharacterBioTab` (399 righe) | aspetto, storia, tratti di specie, tabella «DAL MANUALE» coi **nomi** dei privilegi, sottoclasse, privilegi annotati a mano, talenti, background, addestramento, lingue |
| Oggetti | `CharacterItemsTab` (869 righe) | inventario, monete |
| Magia | `CharacterMagicTab` | slot, incantesimi (nascosto se il PG non lancia) |

Il difetto è una **separazione**, non una mancanza: «Ira» esiste due volte e scollegata — come
contatore 3/riposo lungo nel tab «Tiri», e come parola dentro il paragrafo `class_features` nel tab
«Scheda». Sulla carta è un riquadro solo, e per leggerlo non si volta pagina.

Quella separazione ha una data e una ragione. `Models/ClassResource.cs:9-12` la dichiara: *«Quattro
campi e non uno di più: nessun campo effetto, nessuna formula. Il contatore conta e basta; la
semantica del privilegio resta nella prosa che il personaggio già porta altrove»* (spec 2026-08-06).
La decisione ha tenuto la struttura dati minima e va **rispettata, non rovesciata**: il contatore
resta dov'è, e ciò che nasce qui gli si affianca puntandolo per nome.

## Le decisioni

### D1 — Il tab «Tiri» *diventa* la vista di gioco

Nessuna quinta tab, nessuna rifusione della navigazione. Il tab si rinomina «Gioco» e accoglie le
sezioni nuove.

Una quinta tab dedicata duplicherebbe armi e contatori su due superfici: è il difetto di giuntura
che questo repo ha già pagato più volte, e per un dato che si spende — i pallini dell'Ira — due
superfici primarie sono un invito alla divergenza. Rifondere i quattro tab in due sarebbe un
refactor di componenti da 900+ righe per un beneficio che si ottiene senza.

E soprattutto: «Tiri» **è già ordinato per momento del turno** (riordino del 2026-08-06, commento in
testa a `CharacterCombatTab.razor`). La vista di gioco esiste in embrione; le mancano le
schede-privilegio e lo stato attivo. Su telefono «vedo tutto in una schermata» non significa «tutto
sopra la piega»: significa **non cambiare tab durante il turno**, e una colonna unica con la barra
vitali sticky lo soddisfa.

### D2 — L'elenco dei privilegi si **deriva**, non si salva

Sul database va solo ciò che nessun'altra fonte può conoscere: le parole dell'utente.

| Cosa | Origine | Persistito |
|---|---|---|
| Nomi dei privilegi di classe | `ClassProgression.PrivilegiFinoAl` | no |
| Nomi dei privilegi di sottoclasse | `SubclassCatalog.PrivilegiFinoAl` | no |
| Talenti, con descrizione ufficiale | `CharacterManualJoin.TalentiRiconosciuti` | no |
| Nota, tag azione, collegamento al contatore, attivabilità | l'utente | **sì** |

L'aggancio fra annotazione e privilegio è il **nome normalizzato** (`CatalogKey.NormalizeName`; mai
`String.Normalize`, che sotto `InvariantGlobalization` è un no-op silenzioso — v. memoria «Stack
gotchas»).

Il guadagno non è teorico. **Al level-up le schede nuove compaiono da sole**, senza rigenerare
niente: la domanda «quali privilegi ho?» conserva **una sola risposta possibile**, quella del
pacchetto. Una copia salvata dei nomi sarebbe stata più semplice da scrivere e avrebbe creato una
seconda fonte di verità per lo stesso dato — la classe di difetto che questo repo ha già pagato due
volte (il documento che dichiarava applicata una migrazione che non lo era; i campi dimenticati in
`CloneCharacter`).

Le **voci scritte a mano** sono parte del modello, non un'eccezione: un'annotazione la cui chiave non
corrisponde a nessun privilegio derivato è semplicemente una voce propria. Servono per i casi sotto
(D7) e per le note operative che non sono privilegi — sulla scheda cartacea, per esempio, *«Se non
hai armatura pesante, vai a 12 m/s»*.

Le annotazioni **orfane** — chiave che non corrisponde più a nulla dopo un cambio di classe o un
aggiornamento del pacchetto — non si cancellano mai in silenzio: confluiscono nelle voci proprie.

### D3 — Cinque campi, e **nessun campo effetto**

```
Nome · Nota · Azione (azione|bonus|reazione|passivo|turno, opzionale)
     · Risorsa (nome del contatore collegato, opzionale) · Attivabile (bool)
```

Non c'è `Manuale`: una voce è propria se la sua chiave non è fra quelle derivate — dedurlo elimina un
campo che potrebbe andare fuori sincrono con la realtà.

L'assenza di campi effetto è **strutturale, non un rinvio**. Il rischio maggiore di questo progetto è
la deriva verso il motore di regole: da «mostra la nota dell'Ira» a «applica il vantaggio ai tiri di
Forza e il +2 al danno» il passo sembra breve ed è un burrone — semantica D&D senza fondo,
descrizioni ufficiali non disponibili, e la violazione frontale della decisione del 2026-08-06. La
scheda cartacea è la prova che la prosa dell'utente basta: non chiede che l'app calcoli l'effetto,
chiede di poterlo rileggere al volo. Niente bonus, niente formule, niente riferimenti a
caratteristiche — e il commento in testa al Model che lo dichiara, come già fa `ClassResource`.

### D4 — «Cosa è attivo adesso» vive in `localStorage`, mai sul database

Un Singleton con lo stato in `localStorage` via `IJSRuntime`, sul modello esatto di
`CampaignStateService.cs:74-95`.

**Non su `characters`**: l'Ira si accende e si spegne più volte per sessione, e ogni interruttore
sarebbe un `Update` di riga intera da 113 colonne, last-write-wins, su rete mobile, con rifiuto RLS
silenzioso (PostgREST aggiorna zero righe e risponde `[]`, senza eccezione).

**Non in sola memoria**: su un telefono al tavolo il sistema operativo chiude la scheda mentre si
guarda altro, e lo stato andrebbe perso a ogni distrazione. `localStorage` non costa rete, non costa
migrazione, e sopravvive alla riapertura. Il Singleton è necessario a prescindere, perché
`@if (activeTab == ...)` distrugge il componente al cambio tab.

Quando una voce è attiva, la strip in alto mostra **la nota dell'utente**, promossa in evidenza.
Nessun motore di regole: solo prosa messa dove serve.

### D5 — Tag di economia d'azione: tabella curata come seme, valore dell'utente come verità

Il pattern esiste ed è collaudato: `ClassResourceRules.PerClasse` (righe 39-54) è una mappa curata a
mano con chiavi normalizzate, e `Tests/ClassResourceRulesTests.cs` la **incrocia col pacchetto SRD**
— se un nome nella tabella non esiste più fra i `features` del pacchetto, il test diventa rosso da
solo. `CharacterFeatureRules.AzioniSuggerite` ne è il gemello.

La deduzione automatica è impossibile: `levels[].features` sono stringhe nude. Il solo-manuale
butterebbe via il fatto che i nomi SRD sono noti e normalizzabili.

**Voce non in tabella → tag nullo → gruppo «Da classificare».** Mai un tag indovinato: in
combattimento un tag sbagliato è peggio di un tag mancante.

Cinque valori: `azione`, `bonus`, `reazione`, `passivo`, `turno`. L'ultimo esiste perché la scheda
cartacea è piena di *rider* del tipo «una volta per turno, se colpisci» (Aggressore selvaggio,
Scarica di adrenalina) che non sono nessuna delle altre quattro.

Il tag decide **in quale sezione** la voce viene resa: `azione`, `bonus`, `reazione`, `turno` e le
voci senza tag stanno nella sezione 6 dell'ordine verticale; `passivo` sta nella sezione 7, che ha
una resa diversa — riga singola, nessun contatore, nessun interruttore.

### D6 — La spesa in monete rompe **solo ciò che serve**

`CoinConversion.Spendi` paga dal taglio più piccolo posseduto verso l'alto, e rompe un taglio
maggiore soltanto quando i minori non bastano.

La regola alternativa — sottrai dal totale e ricompatta il borsello — è più semplice e sbagliata:
con 15 ma e 3 mr, spendendo 1 mr lascerebbe 1 mo e 5 ma, riorganizzando tagli che l'utente non ha
toccato. **Il borsello si riorganizza solo dove è stato toccato.**

L'aritmetica resta in rame su interi, come già fa il resto di `CoinConversion`: mr 1, ma 10, me 50,
mo 100, mp 1000. Fondi insufficienti → **nessuna scrittura**, e il messaggio dice quanto manca.

Esempio dell'utente: 1 mo, spesa 2 mr → nessun rame né argento disponibile, si rompe 1 mo in 10 ma,
poi 1 ma in 10 mr, si pagano 2 → **restano 9 ma e 8 mr**, con la spiegazione «rotta 1 mo».

### D7 — I tratti di specie restano prosa: è un limite del pacchetto, non una scelta

`Models/Packages/CatalogPackage.cs:42-50` — `PackageSpecies.Traits` è **una stringa unica**, non un
elenco. *Scarica di adrenalina*, *Scurovisione*, *Resistenza implacabile* non sono separabili
automaticamente.

Restano due strade, entrambe già coperte: l'utente le aggiunge come **voci proprie** (D2), oppure le
lascia nel testo libero del tab «Scheda» come oggi. Non è lavoro aggiuntivo: le voci proprie servono
comunque.

Classe e sottoclasse invece **sono** elenchi (`PackageClassLevel.Features` è `List<string>`), e i
talenti portano già la descrizione ufficiale.

## L'ordine verticale, su telefono

Una colonna, ordinata per frequenza d'uso nel turno. In **grassetto** ciò che nasce nuovo.

1. Barra vitali sticky — PF, CA, INI, PERC *(esiste)*
2. TS contro morte, condizionale *(esiste)* — se stai morendo non conta altro
3. **Strip ATTIVO** — chip degli stati accesi con la prosa dei loro effetti. In alto perché cambia
   come si legge tutto ciò che sta sotto: con l'Ira accesa, armi e tiri salvezza si leggono
   diversamente. Invisibile se niente è attivo né attivabile
4. Velocità e ispirazione *(esiste)*
5. Armi *(esiste)* — il riquadro WEAPONS & DAMAGE della cartacea: l'azione della maggior parte dei turni
6. **Privilegi raggruppati per economia d'azione** — Azione · Bonus · Reazione · Una volta per turno ·
   Da classificare. Ogni scheda porta nome, nota e — se collegata — **i pallini del contatore sulla
   scheda stessa**: l'Ira si spende e si attiva da lì
7. **Passivi** — riga singola per voce (Senso del pericolo, Difesa senza armatura, Scurovisione):
   promemoria, non azioni, quindi sotto
8. Caratteristiche e abilità *(esiste)* — servono per le prove, non a ogni turno
9. Divisore «a fine combattimento»: dadi vita, riposi *(esiste)*

Le aggiunte si **innestano** in quell'ordine, non lo ridisegnano: il criterio è lo stesso del
riordino già fatto il 2026-08-06.

La sezione RISORSE attuale non sparisce, ma si restringe ai contatori **senza** scheda-privilegio
collegata: un dato, una sola superficie primaria.

## Componenti e contratti

Logica di dominio in helper puri `static` testabili, mai nei `.razor` — la regola del progetto.

| Unità | Responsabilità | Dipende da |
|---|---|---|
| `Models/CharacterFeature.cs` | POCO della voce annotata (5 campi), elemento del jsonb `character_features` | — |
| `Services/CharacterFeatureRules.cs` | `Normalizza` (rete anti-jsonb malformato, gemella di `ClassResourceRules.Normalizza`), `TagAmmessi`, `AzioniSuggerite` (tabella curata) | `CatalogKey` |
| `Services/CharacterFeatureJoin.cs` | JOIN puro in memoria: privilegi derivati + annotazioni + `ClassResources` → gruppi ordinati per tag. Modello: `CharacterSpellJoin`, `CharacterManualJoin` | `ClassProgression`, `SubclassCatalog`, `CharacterManualJoin` |
| `Services/ActiveEffectsService.cs` | Singleton, stato attivo per personaggio, persistito in `localStorage`. Modello: `CampaignStateService` | `IJSRuntime` |
| `Services/CoinConversion.cs` *(esiste)* | `Spendi` — nuovo metodo accanto a `Compatta` | — |
| `Shared/CharacterTabs/…` | Rendering delle sezioni nuove dentro il tab «Gioco»; editing inline della nota sulla scheda, come già fa `resource-edit-btn` per le risorse | i sopra |

`CharacterCombatTab.razor` è già a 929 righe: le sezioni nuove vanno in componenti figli, e va tenuto
presente che **l'isolamento CSS scoped del genitore non raggiunge i figli**, `@media` incluse — si
replica nel figlio o si promuove in `app.css` (memoria «Blazor CSS isolation»).

## Errori

- Spesa insufficiente, nota vuota, tag non ammesso → errore di **validazione** → toast `.app-toast`
  (mai `.toast`).
- Salvataggio rifiutato o fallito → errore di **sistema** → `DbErrorBanner`.
- Cancellazione di una voce annotata → `ConfirmDialog`, mai `confirm()`.
- `character_features` malformato non deve **mai** impedire l'apertura della scheda: `Normalizza`
  scarta le voci senza nome, tronca i valori fuori campo e riporta i tag ignoti a nullo.

## Test

Tutti su helper puri, xUnit.

- `CoinConversion.Spendi`: rottura minima (15 ma + 3 mr − 1 mr lascia **15 ma**, non 1 mo);
  l'esempio dell'utente (1 mo − 2 mr = 9 ma + 8 mr); insufficienza → nessuna mutazione;
  invarianza `totale_prima − spesa == totale_dopo`.
- `CharacterFeatureRules.Normalizza`: jsonb malformato, tag ignoto → nullo, duplicati per nome
  normalizzato.
- `CharacterFeatureRules.AzioniSuggerite`: **incrociata col pacchetto SRD**, come
  `ClassResourceRulesTests` — un nome della tabella curata che non esiste fra i `features` del
  pacchetto rende il test rosso da solo.
- `CharacterFeatureJoin`: aggancio per nome normalizzato, annotazione orfana → voce propria,
  ordinamento dei gruppi, collegamento al contatore.

Vale la regola del 2026-08-06: **un test nato per sorvegliare una correzione va provato per
mutazione** — si toglie la correzione, si verifica che diventi rosso, si ripristina. Dove il valore
scelto è ciò che rende il test non vacuo (i 15 ma che non devono compattarsi), va scritto accanto al
valore.

## Il database

Una colonna, una volta sola. Da consegnare **in chat** e applicare a mano **prima** del push, per la
regola «Come si cambia il database»:

```sql
ALTER TABLE "public"."characters"
    ADD COLUMN IF NOT EXISTS "character_features" jsonb DEFAULT '[]'::jsonb NOT NULL;
```

Stessa forma di `class_resources` (`20260806130000_scheda_carta.sql:45`). Un jsonb invece di più
colonne non è preferenza di stile: `postgrest-csharp` serializza **ogni** proprietà `[Column]` a ogni
`Update`, quindi una colonna presente nel Model e assente sul server rifiuta l'intera riga — il
2026-08-08 sette colonne mancanti bloccarono per due giorni tutte le scritture su `characters`, punti
ferita compresi. Un jsonb concentra in **una** esposizione a quel rischio tutti i campi futuri della
funzione.

Retrocompatibilità col client in cache: il client precedente non mappa `character_features`, quindi i
suoi `Update` non la toccano. Chi non ha ancora premuto «Aggiorna» continua a funzionare.

Verifica obbligatoria prima del push: `supabase/verifica-schema.sh`.

## Cosa è stato deliberatamente escluso

- **Sezione «sbloccato di recente»**: al massimo un contrassegno sulla scheda dopo un level-up, che
  sparisce alla prima apertura. Una sezione dedicata ruberebbe spazio verticale tutto l'anno per
  un'informazione che vale due giorni.
- **Parsing automatico del testo libero esistente.** I nomi si derivano, la prosa no: `class_features`
  resta intatto e leggibile nel tab «Scheda» finché l'utente non travasa a mano ciò che vuole.
  Indovinare dove finisce la nota di un privilegio e comincia quella del successivo, dentro un
  paragrafo scritto liberamente, produrrebbe frammenti.
- **Qualunque campo effetto** (v. D3).
- **Rifusione della navigazione** (v. D1).
- **Guadagno di monete** simmetrico alla spesa: l'editor esistente lo copre già.

## Costo per l'utente, detto chiaro

Circa **12 voci** da annotare una volta per Grunnok. I nomi li mette l'app; le note sono quelle già
scritte sulla scheda cartacea, da ricopiare. Se le schede restassero vuote dopo due sessioni, sarebbe
il segnale che il travaso manuale non funziona e che serve un'assistenza alla migrazione — è il
criterio di fallimento dichiarato di questo design, insieme a: se al primo playtest l'utente
continua a cambiare tab durante il turno, la colonna unica non bastava e serviva la vista dedicata.

## Ordine di realizzazione

Le monete (D6) sono indipendenti da tutto il resto e non richiedono la migrazione: si possono
chiudere e rilasciare per prime.
