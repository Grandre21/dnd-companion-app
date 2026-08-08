# La vista di gioco — piano di implementazione

> **Per chi esegue:** questo progetto **non** usa `superpowers:subagent-driven-development` né
> `superpowers:executing-plans` — sono esplicitamente vietate da `~/.claude/CLAUDE.md`. Vale il
> protocollo del progetto: l'orchestratore (Opus) scrive un brief per fetta, un `implementer`
> (Sonnet) per brief, poi il gate a due agenti (`bug-hunter` + `conformity`). Le caselle `- [ ]`
> servono al tracciamento.

**Obiettivo:** rendere la gestione del personaggio usabile al tavolo senza tornare alla scheda di
carta — privilegi con le note del giocatore raggruppati per economia d'azione, stato attivo, e una
spesa in monete che rompe solo i tagli necessari.

**Architettura:** logica di dominio in helper puri `static` testabili; l'elenco dei privilegi si
**deriva** dal pacchetto SRD e sul database va solo l'annotazione dell'utente, in un unico jsonb;
lo stato «attivo adesso» vive in `localStorage`, mai su `characters`.

**Stack:** Blazor WebAssembly / .NET 10, xUnit, Supabase (PostgREST via `postgrest-csharp 3.5.1`).

**Spec:** [`docs/superpowers/specs/2026-08-08-vista-di-gioco-design.md`](../specs/2026-08-08-vista-di-gioco-design.md)

## Vincoli globali

Valgono per **ogni** task, senza ripeterli task per task.

- Build attesa: `dotnet build` → **0 warning / 0 errori**. Test: `dotnet test Tests/DndCompanion.Tests.csproj`.
- **Solo `main`.** Nessun branch, nessuna PR. Gli agenti **non committano**: committa l'orchestratore.
- **Nessuna dipendenza nuova.** Trimming `full` attivo; Realtime e `System.Reactive` rimossi di proposito.
- **`InvariantGlobalization` attivo**: `String.Normalize` è un no-op silenzioso. Per confrontare nomi
  si usa **solo** `CatalogKey.NormalizeName(string?)`.
- Logica di dominio **mai** nei `.razor`: helper puri `static` in `Services/`.
- UI: toast classe **`.app-toast`** (mai `.toast`), **`ConfirmDialog`** (mai `confirm()`),
  `DbErrorBanner` per gli errori di sistema, `<LoadingSpinner>` per le attese.
- a11y: ogni controllo interattivo non-`<button>` porta `role`, `tabindex`, `aria-*` e risponde a
  Enter/Space tramite l'helper `OnKey` già presente nei tab.
- CSS: **design token in `:root`**. Lo scope isolato del genitore **non raggiunge i componenti
  figli**, `@media` incluse: si replica nel figlio o si promuove in `app.css`.
- **Un campo nuovo su `Character` va aggiunto anche a `CharacterClone.CloneCharacter`**, per valore
  se è una collezione. `Tests/CharacterCloneTests.cs` confronta per riflessione e diventa rosso da solo.
- Un test nato per sorvegliare una correzione **va provato per mutazione**: si toglie la correzione,
  si verifica che diventi **rosso**, si ripristina. Dove il valore scelto è ciò che rende il test non
  vacuo, va scritto **accanto al valore**.

---

## Struttura dei file

| File | Responsabilità | Task |
|---|---|---|
| `Services/CoinConversion.cs` *(modifica)* | `EsitoSpesa`, `Spendi`, `Applica(Character, EsitoSpesa)` | 1 |
| `Tests/CoinConversionTests.cs` *(modifica)* | test della spesa | 1 |
| `Shared/CharacterTabs/CharacterItemsTab.razor` *(modifica)* | pannello «Usa» con anteprima | 2 |
| `Models/CharacterFeature.cs` *(nuovo)* | POCO della voce annotata, elemento del jsonb | 3 |
| `Models/Character.cs` *(modifica)* | proprietà `CharacterFeatures` + `[Column]` | 3 |
| `Services/CharacterClone.cs` *(modifica)* | copia per valore della lista | 3 |
| `Services/Repositories/CharacterRepository.cs` *(modifica)* | normalizzazione in lettura | 3 |
| `Services/CharacterFeatureRules.cs` *(nuovo)* | `Normalizza`, `TagAmmessi`, `AzioniSuggerite` | 4 |
| `Tests/CharacterFeatureRulesTests.cs` *(nuovo)* | test, incluso l'incrocio col pacchetto SRD | 4 |
| `Services/CharacterFeatureJoin.cs` *(nuovo)* | JOIN puro derivati + annotazioni + contatori | 5 |
| `Tests/CharacterFeatureJoinTests.cs` *(nuovo)* | test del join e dei gruppi | 5 |
| `Services/ActiveEffectsService.cs` *(nuovo)* | Singleton, stato attivo in `localStorage` | 6 |
| `Program.cs` *(modifica)* | registrazione DI | 6 |
| `Shared/CharacterTabs/CharacterFeaturesSection.razor` *(nuovo)* + `.css` | schede-privilegio, editing inline | 7 |
| `Shared/CharacterTabs/CharacterActiveStrip.razor` *(nuovo)* + `.css` | strip «ATTIVO» | 8 |
| `Shared/CharacterTabs/CharacterCombatTab.razor` *(modifica)* | innesto delle sezioni nuove | 8 |
| `Pages/Characters.razor` *(modifica)* | wiring, rinomina del tab, salvataggio | 8 |

**Perché componenti figli e non tutto dentro `CharacterCombatTab.razor`:** quel file è già a 929
righe. Le due sezioni nuove hanno confini netti (una rende un elenco, l'altra uno stato) e stanno
meglio separate — ma ricordarsi del vincolo CSS: lo scope del genitore non le raggiunge.

---

## Fetta A — Le monete

Indipendente da tutto il resto. **Non richiede la migrazione**: si può rilasciare da sola.

### Task 1: `CoinConversion.Spendi`

**File:**
- Modifica: `Services/CoinConversion.cs` (in coda, dopo `Applica` a riga 125-132)
- Test: `Tests/CoinConversionTests.cs` (in coda)

**Interfacce:**
- Consuma: `CoinConversion.TotaleInRame`, `ValoreRame`/`ValoreArgento`/`ValoreElectrum`/`ValoreOro`/`ValorePlatino` (già esistenti, righe 30-48).
- Produce, per il Task 2:
  ```csharp
  public sealed record EsitoSpesa
  {
      public required bool Riuscita { get; init; }
      public required int PlatinumPieces { get; init; }
      public required int GoldPieces { get; init; }
      public required int ElectrumPieces { get; init; }
      public required int SilverPieces { get; init; }
      public required int CopperPieces { get; init; }
      public required long MancanoInRame { get; init; }
      public required long RestoInRame { get; init; }
      public required string? TaglioRotto { get; init; }   // "mp" | "mo" | "me" | "ma" | null
  }

  public static EsitoSpesa Spendi(int platino, int oro, int electrum, int argento, int rame,
                                  int spesaPlatino, int spesaOro, int spesaElectrum,
                                  int spesaArgento, int spesaRame);
  public static EsitoSpesa Spendi(Character c, int spesaPlatino, int spesaOro,
                                  int spesaElectrum, int spesaArgento, int spesaRame);
  public static void Applica(Character c, EsitoSpesa esito);
  ```

**La regola, in una frase:** *paghi con i tagli più piccoli che hai finché non copri la spesa, e il
resto ti torna nei tagli comuni.* È come si paga davvero al tavolo, e produce la «rottura minima»
richiesta dalla spec (D6) senza nessun ciclo di conversioni.

Il resto si rende **solo in mr/ma/mo**: mai electrum né platino, esattamente come
`Compatta` (righe 66-70), e per la stessa ragione di gioco.

- [ ] **Passo 1: scrivere i test che falliscono**

In coda a `Tests/CoinConversionTests.cs`:

```csharp
    // -----------------------------------------------------------------------------------
    // Spendi — v. spec 2026-08-08, D6
    // -----------------------------------------------------------------------------------

    /// <summary>L'esempio dell'utente: 1 mo, spendo 2 mr. Non ci sono spiccioli, quindi l'oro va
    /// consegnato intero e il resto torna compattato.</summary>
    [Fact]
    public void Spendi_RompeIlTaglioGrandeQuandoIPiccoliNonBastano()
    {
        var esito = CoinConversion.Spendi(0, 1, 0, 0, 0, 0, 0, 0, 0, 2);

        Assert.True(esito.Riuscita);
        Assert.Equal(0, esito.GoldPieces);
        Assert.Equal(9, esito.SilverPieces);
        Assert.Equal(8, esito.CopperPieces);
        Assert.Equal("mo", esito.TaglioRotto);
        Assert.Equal(98, esito.RestoInRame);
    }

    /// <summary>Il cuore di D6: il borsello si riorganizza SOLO dove è stato toccato. I 15 ma non
    /// sono un numero a caso — sono più di 10, quindi una ricompattazione generale li
    /// trasformerebbe in 1 mo + 5 ma. Se questo test passa con 5 ma non prova più nulla.</summary>
    [Fact]
    public void Spendi_NonRiorganizzaITagliCheNonHaToccato()
    {
        var esito = CoinConversion.Spendi(0, 0, 0, 15, 3, 0, 0, 0, 0, 1);

        Assert.True(esito.Riuscita);
        Assert.Equal(15, esito.SilverPieces);   // NON 1 mo + 5 ma
        Assert.Equal(0, esito.GoldPieces);
        Assert.Equal(2, esito.CopperPieces);
        Assert.Null(esito.TaglioRotto);
        Assert.Equal(0, esito.RestoInRame);
    }

    [Fact]
    public void Spendi_FondiInsufficienti_NonCambiaNullaEDiceQuantoManca()
    {
        var esito = CoinConversion.Spendi(0, 0, 0, 0, 5, 0, 0, 0, 0, 12);

        Assert.False(esito.Riuscita);
        Assert.Equal(5, esito.CopperPieces);    // invariato
        Assert.Equal(7, esito.MancanoInRame);
    }

    /// <summary>Il resto non crea mai electrum né platino: stessa scelta di Compatta.</summary>
    [Fact]
    public void Spendi_IlRestoNonCreaElectrumNePlatino()
    {
        var esito = CoinConversion.Spendi(1, 0, 0, 0, 0, 0, 0, 0, 0, 1);

        Assert.True(esito.Riuscita);
        Assert.Equal(0, esito.PlatinumPieces);
        Assert.Equal(0, esito.ElectrumPieces);
        Assert.Equal(9, esito.GoldPieces);
        Assert.Equal(9, esito.SilverPieces);
        Assert.Equal(9, esito.CopperPieces);
        Assert.Equal("mp", esito.TaglioRotto);
    }

    /// <summary>Invariante: il valore che esce dal borsello è esattamente la spesa.</summary>
    [Theory]
    [InlineData(0, 1, 0, 0, 0, 2)]
    [InlineData(0, 0, 3, 0, 0, 60)]
    [InlineData(2, 5, 1, 15, 3, 137)]
    [InlineData(0, 0, 0, 15, 3, 1)]
    public void Spendi_ToglieEsattamenteLaSpesa(int mp, int mo, int me, int ma, int mr, int spesaInRame)
    {
        var prima = CoinConversion.TotaleInRame(mp, mo, me, ma, mr);
        var esito = CoinConversion.Spendi(mp, mo, me, ma, mr, 0, 0, 0, 0, spesaInRame);

        Assert.True(esito.Riuscita);
        var dopo = CoinConversion.TotaleInRame(
            esito.PlatinumPieces, esito.GoldPieces, esito.ElectrumPieces,
            esito.SilverPieces, esito.CopperPieces);
        Assert.Equal(prima - spesaInRame, dopo);
    }

    [Fact]
    public void Spendi_SpesaNulla_LasciaTuttoComEra()
    {
        var esito = CoinConversion.Spendi(1, 2, 3, 4, 5, 0, 0, 0, 0, 0);

        Assert.True(esito.Riuscita);
        Assert.Equal(1, esito.PlatinumPieces);
        Assert.Equal(2, esito.GoldPieces);
        Assert.Equal(3, esito.ElectrumPieces);
        Assert.Equal(4, esito.SilverPieces);
        Assert.Equal(5, esito.CopperPieces);
    }

    /// <summary>Applica non deve MAI scrivere un esito fallito: la scheda resterebbe con un
    /// borsello che il server non ha, e senza nessun errore visibile.</summary>
    [Fact]
    public void Applica_EsitoFallito_NonToccaIlPersonaggio()
    {
        var pg = new Character { CopperPieces = 5, SilverPieces = 2 };
        var esito = CoinConversion.Spendi(pg, 0, 0, 0, 0, 999);

        CoinConversion.Applica(pg, esito);

        Assert.Equal(5, pg.CopperPieces);
        Assert.Equal(2, pg.SilverPieces);
    }
```

- [ ] **Passo 2: eseguire i test e verificare che falliscano**

Run: `dotnet test Tests/DndCompanion.Tests.csproj --filter "FullyQualifiedName~CoinConversionTests"`
Atteso: **errore di compilazione** — `EsitoSpesa` e `Spendi` non esistono.

- [ ] **Passo 3: implementare**

In coda a `Services/CoinConversion.cs`, prima della graffa di chiusura della classe, e il record
`EsitoSpesa` accanto a `EsitoCompattazione` in testa al file:

```csharp
/// <summary>
/// L'esito di una spesa: il nuovo assetto delle cinque monete, oppure il rifiuto con l'ammontare
/// mancante. Come <see cref="EsitoCompattazione"/>, calcola senza toccare il personaggio: la
/// scrittura passa da <see cref="CoinConversion.Applica(Character, EsitoSpesa)"/>, che su un esito
/// fallito non scrive nulla.
/// </summary>
public sealed record EsitoSpesa
{
    public required bool Riuscita { get; init; }
    public required int PlatinumPieces { get; init; }
    public required int GoldPieces { get; init; }
    public required int ElectrumPieces { get; init; }
    public required int SilverPieces { get; init; }
    public required int CopperPieces { get; init; }

    /// <summary>Rame che manca per coprire la spesa; 0 quando <see cref="Riuscita"/> è true.</summary>
    public required long MancanoInRame { get; init; }

    /// <summary>Il resto ricevuto, in rame: &gt; 0 solo se è stato consegnato un taglio più grande
    /// del dovuto. La UI se ne serve per dire cosa è successo al borsello.</summary>
    public required long RestoInRame { get; init; }

    /// <summary>Sigla del taglio più grande consegnato quando c'è stato resto ("mp"/"mo"/"me"/"ma"),
    /// altrimenti null. Serve al messaggio «rotta 1 mo», non al calcolo.</summary>
    public required string? TaglioRotto { get; init; }
}
```

```csharp
    /// <summary>
    /// Spende dal gruzzolo, <b>rompendo solo ciò che serve</b> (v. spec 2026-08-08, D6).
    ///
    /// La regola è quella del tavolo: si consegnano i tagli più piccoli posseduti finché non
    /// coprono la spesa, e il resto torna indietro nei tagli comuni. Da qui la proprietà che si
    /// voleva: i tagli che non sono serviti a pagare restano <b>esattamente come erano</b> — con 15
    /// ma e 3 mr, spendere 1 mr lascia 15 ma, non 1 mo e 5 ma.
    ///
    /// Il resto si rende solo in mr/ma/mo: platino ed electrum non si creano mai, stessa scelta —
    /// e stessa ragione di gioco — di <see cref="Compatta(int,int,int,int,int)"/>.
    ///
    /// Fondi insufficienti: nessuna mutazione, <see cref="EsitoSpesa.MancanoInRame"/> valorizzato.
    /// Valute negative (il DB non ha vincoli CHECK) contano come 0, come in
    /// <see cref="TotaleInRame(int,int,int,int,int)"/>.
    /// </summary>
    public static EsitoSpesa Spendi(int platino, int oro, int electrum, int argento, int rame,
                                    int spesaPlatino, int spesaOro, int spesaElectrum,
                                    int spesaArgento, int spesaRame)
    {
        var borsello = new[]
        {
            (Sigla: "mr", Valore: ValoreRame,     Quantita: (long)Math.Max(0, rame)),
            (Sigla: "ma", Valore: ValoreArgento,  Quantita: (long)Math.Max(0, argento)),
            (Sigla: "me", Valore: ValoreElectrum, Quantita: (long)Math.Max(0, electrum)),
            (Sigla: "mo", Valore: ValoreOro,      Quantita: (long)Math.Max(0, oro)),
            (Sigla: "mp", Valore: ValorePlatino,  Quantita: (long)Math.Max(0, platino)),
        };

        var totale = TotaleInRame(platino, oro, electrum, argento, rame);
        var spesa = TotaleInRame(spesaPlatino, spesaOro, spesaElectrum, spesaArgento, spesaRame);

        if (spesa > totale)
        {
            return new EsitoSpesa
            {
                Riuscita = false,
                PlatinumPieces = Math.Max(0, platino),
                GoldPieces = Math.Max(0, oro),
                ElectrumPieces = Math.Max(0, electrum),
                SilverPieces = Math.Max(0, argento),
                CopperPieces = Math.Max(0, rame),
                MancanoInRame = spesa - totale,
                RestoInRame = 0,
                TaglioRotto = null,
            };
        }

        // Si consegna dal taglio più piccolo verso l'alto, una moneta per volta, finché il
        // consegnato non copre la spesa. Il ciclo termina per costruzione: totale >= spesa.
        long consegnato = 0;
        string? taglioRotto = null;
        for (var i = 0; i < borsello.Length && consegnato < spesa; i++)
        {
            var (sigla, valore, quantita) = borsello[i];
            var servono = Math.Min(quantita, (spesa - consegnato + valore - 1) / valore);
            if (servono <= 0) continue;

            borsello[i].Quantita = quantita - servono;
            consegnato += servono * valore;
            taglioRotto = sigla;
        }

        var resto = consegnato - spesa;

        // Il resto rientra nei soli tagli comuni, dal più grande al più piccolo.
        var rientroOro = resto / ValoreOro;
        resto %= ValoreOro;
        var rientroArgento = resto / ValoreArgento;
        var rientroRame = resto % ValoreArgento;

        return new EsitoSpesa
        {
            Riuscita = true,
            PlatinumPieces = (int)borsello[4].Quantita,
            GoldPieces = (int)(borsello[3].Quantita + rientroOro),
            ElectrumPieces = (int)borsello[2].Quantita,
            SilverPieces = (int)(borsello[1].Quantita + rientroArgento),
            CopperPieces = (int)(borsello[0].Quantita + rientroRame),
            MancanoInRame = 0,
            RestoInRame = consegnato - spesa,
            TaglioRotto = consegnato > spesa ? taglioRotto : null,
        };
    }

    public static EsitoSpesa Spendi(Character c, int spesaPlatino, int spesaOro,
                                    int spesaElectrum, int spesaArgento, int spesaRame) =>
        Spendi(c.PlatinumPieces, c.GoldPieces, c.ElectrumPieces, c.SilverPieces, c.CopperPieces,
               spesaPlatino, spesaOro, spesaElectrum, spesaArgento, spesaRame);

    /// <summary>Scrive l'esito sul personaggio. <b>Un esito fallito non scrive nulla</b>: altrimenti
    /// la scheda mostrerebbe un borsello che il server non ha, senza nessun errore visibile.</summary>
    public static void Applica(Character c, EsitoSpesa esito)
    {
        if (!esito.Riuscita) return;
        c.PlatinumPieces = esito.PlatinumPieces;
        c.GoldPieces = esito.GoldPieces;
        c.ElectrumPieces = esito.ElectrumPieces;
        c.SilverPieces = esito.SilverPieces;
        c.CopperPieces = esito.CopperPieces;
    }
```

- [ ] **Passo 4: eseguire i test e verificare che passino**

Run: `dotnet test Tests/DndCompanion.Tests.csproj --filter "FullyQualifiedName~CoinConversionTests"`
Atteso: **tutti verdi**.

- [ ] **Passo 5: collaudo per mutazione di `Spendi_NonRiorganizzaITagliCheNonHaToccato`**

Sostituire temporaneamente il corpo di `Spendi` (ramo riuscito) con una ricompattazione generale:
`return Compatta(...)` applicato a `totale - spesa`. Rieseguire: quel test **deve diventare rosso**
(15 ma → 1 mo + 5 ma). Ripristinare e riverificare il verde. È la sola prova che il test difenda
davvero D6 e non un'ovvietà.

- [ ] **Passo 6: build pulita**

Run: `dotnet build`  → atteso **0 warning / 0 errori**.

---

### Task 2: il pulsante «Usa» sulle monete

**File:**
- Modifica: `Shared/CharacterTabs/CharacterItemsTab.razor` (sezione monete: markup 362-416, code-behind 462, 797-846)
- Modifica: `Shared/CharacterTabs/CharacterItemsTab.razor.css`

**Interfacce:**
- Consuma: `CoinConversion.Spendi`, `CoinConversion.Applica(Character, EsitoSpesa)`, `EsitoSpesa` (Task 1).
- Produce: niente per i task successivi.

**Pattern da rispecchiare — non inventarne di nuovi.** Il file applica già il **pattern a bozza**
a `newItemDraft`, `editItemDraft` e (dal 2026-08-08) all'editor delle monete: si compila una copia,
il salvataggio la travasa. La spesa segue lo stesso schema, e per la stessa ragione — `Pages/Characters.razor`
salva con `UpdateCharacterAsync(selected)`, cioè **la riga intera da 113 colonne**: una mutazione mai
confermata partirebbe col primo salvataggio di qualunque altro campo.

- [ ] **Passo 1: stato della bozza di spesa**

Accanto a `isEditingMoney` (riga 462):

```csharp
    // Spesa (pulsante «Usa»): bozza separata da quella dell'editor, perché sono due modi diversi
    // di toccare lo stesso dato e aprirne uno non deve sporcare l'altro.
    private bool isSpendingMoney;
    private int spendPlatinum, spendGold, spendElectrum, spendSilver, spendCopper;
```

E dentro il `if (Character.Id != _lastId)` di `OnParametersSet` (accanto all'azzeramento delle cinque
bozze dell'editor, riga ~526) aggiungere l'azzeramento di queste cinque e `isSpendingMoney = false`:
cambiare personaggio con un pannello aperto mostrerebbe la spesa di un altro.

- [ ] **Passo 2: l'anteprima calcolata**

```csharp
    /// <summary>L'esito della spesa in bozza, ricalcolato a ogni render: è una funzione pura di
    /// cinque interi e del borsello, quindi non serve memorizzarlo.</summary>
    private EsitoSpesa AnteprimaSpesa() =>
        CoinConversion.Spendi(Character, spendPlatinum, spendGold, spendElectrum, spendSilver, spendCopper);

    private bool SpesaVuota =>
        spendPlatinum <= 0 && spendGold <= 0 && spendElectrum <= 0 && spendSilver <= 0 && spendCopper <= 0;

    private void ApriSpesa()
    {
        if (!CanEdit) return;
        spendPlatinum = spendGold = spendElectrum = spendSilver = spendCopper = 0;
        isSpendingMoney = true;
    }

    private void AnnullaSpesa() => isSpendingMoney = false;

    private async Task ConfermaSpesaAsync()
    {
        var esito = AnteprimaSpesa();
        if (!esito.Riuscita)
        {
            // Errore di VALIDAZIONE, non di sistema: toast, non banner (v. CLAUDE.md).
            Toasts.Show($"Ti mancano {CoinConversion.FormattaTotaleInOro(esito.MancanoInRame)} mo.", ToastLevel.Error);
            return;
        }

        CoinConversion.Applica(Character, esito);
        isSpendingMoney = false;
        await OnChanged.InvokeAsync();
    }
```

> Verificare la firma reale di `ToastService.Show` nel file prima di scriverla: se non accetta un
> livello, usare l'overload esistente. **Non** aggiungere overload al servizio.

- [ ] **Passo 3: il markup**

Accanto al riepilogo (dopo il blocco `money-compact`, riga ~416), un pulsante `Usa` visibile solo se
`CanEdit`, e il pannello:

```razor
@if (isSpendingMoney)
{
    var anteprima = AnteprimaSpesa();
    <div class="money-spend">
        <p class="money-spend-title">Quanto spendi?</p>
        <div class="money-spend-fields">
            <div class="money-field">
                <label for="spend-mr">mr</label>
                <input id="spend-mr" type="number" min="0" @bind="spendCopper" />
            </div>
            @* ...stessi cinque campi dell'editor, nell'ordine mr · ma · me · mo · mp... *@
        </div>

        @if (SpesaVuota)
        {
            <p class="money-spend-hint">Indica quante monete usi.</p>
        }
        else if (!anteprima.Riuscita)
        {
            <p class="money-spend-error">
                Non bastano: mancano @CoinConversion.FormattaTotaleInOro(anteprima.MancanoInRame) mo.
            </p>
        }
        else
        {
            @if (anteprima.TaglioRotto is not null)
            {
                <p class="money-spend-hint">Non hai spiccioli: si rompe 1 @anteprima.TaglioRotto.</p>
            }
            <p class="money-spend-result">
                Ti restano: @FormatMoneyDa(anteprima)
            </p>
        }

        <div class="money-editor-actions">
            <button type="button" class="money-cancel-btn" @onclick="AnnullaSpesa">Annulla</button>
            <button type="button" class="money-save-btn" @onclick="ConfermaSpesaAsync"
                    disabled="@(SpesaVuota || !anteprima.Riuscita)">Conferma</button>
        </div>
    </div>
}
```

`FormatMoneyDa(EsitoSpesa)` è il gemello di `FormatMoney()` (riga 797) che formatta cinque interi
qualsiasi invece di leggere `Character`: **estrarre il corpo di `FormatMoney()` in un metodo privato
che prende i cinque valori**, e far chiamare quello a entrambi. Nessuna duplicazione.

- [ ] **Passo 4: a11y e CSS**

Il pulsante «Usa» è un vero `<button>`: non servono `role`/`tabindex`. I cinque `<input>` hanno
`<label for>`. Le classi nuove (`money-spend*`) vanno in `CharacterItemsTab.razor.css` usando i
**design token esistenti** — nessun colore letterale.

- [ ] **Passo 5: build e test**

Run: `dotnet build` → 0/0. Run: `dotnet test Tests/DndCompanion.Tests.csproj` → tutti verdi.

- [ ] **Passo 6: gate**

Diff circoscritto alla UI → **1 giro** di `bug-hunter` + `conformity` (tabella in `CLAUDE.md`).
A `conformity` passare come omologhi: il pattern a bozza dell'editor monete e di `newItemDraft` nello
**stesso file**. A `bug-hunter`: `Pages/Characters.razor:872-908` (`OnChanged` non riporta l'esito) e
il fatto che `UpdateCharacterAsync` scriva la riga intera.

---

## Fetta B — I privilegi

Richiede la migrazione. **Da qui in poi nulla va in produzione finché la colonna non esiste sul
database hosted.**

### Task 3: il modello e la colonna

**File:**
- Crea: `Models/CharacterFeature.cs`
- Modifica: `Models/Character.cs` (accanto a `class_resources`, righe 127-128)
- Modifica: `Services/CharacterClone.cs` (accanto alla copia di `ClassResources`, righe 61-70)
- Modifica: `Services/Repositories/CharacterRepository.cs` (accanto alla normalizzazione di `ClassResources`, righe 29-34)
- Crea: `supabase/migrations/20260808000000_character_features.sql`

**Interfacce:**
- Produce, per i Task 4, 5, 7, 8:
  ```csharp
  public class CharacterFeature
  {
      public string Nome { get; set; } = string.Empty;
      public string Nota { get; set; } = string.Empty;
      public string? Azione { get; set; }      // azione|bonus|reazione|passivo|turno, null = da classificare
      public string? Risorsa { get; set; }     // Nome di una ClassResource, null = nessun contatore
      public bool Attivabile { get; set; }
  }
  // su Character:
  [Column("character_features")] public List<CharacterFeature> CharacterFeatures { get; set; } = new();
  ```

- [ ] **Passo 1: il POCO**

`Models/CharacterFeature.cs`, con il commento che dichiara il vincolo — **è la contromisura
strutturale al rischio n.1 della spec, e senza il commento la prossima persona lo smonterà senza
sapere che era voluto**:

```csharp
namespace DndCompanion.Models;

/// <summary>
/// L'annotazione dell'utente su un privilegio: le sue parole, il momento del turno in cui si usa,
/// e l'eventuale contatore collegato. Elemento del jsonb <c>character_features</c> di
/// <see cref="Character"/> (POCO, non una tabella a sé): stesso pattern di
/// <see cref="ClassResource"/> dentro <c>class_resources</c>.
///
/// <b>Il nome del privilegio non è un dato di questa voce: è la sua chiave.</b> L'elenco dei
/// privilegi si deriva dal pacchetto SRD (v. spec 2026-08-08, D2) e non si salva mai — così al
/// level-up le schede nuove compaiono da sole e «quali privilegi ho» conserva una sola risposta
/// possibile. Una voce il cui <see cref="Nome"/> non corrisponde a nessun privilegio derivato è
/// semplicemente una voce propria, scritta a mano: non è un errore e non si cancella.
///
/// <b>Cinque campi e nessun campo effetto</b>, come <see cref="ClassResource"/> ne ha quattro e
/// nessuna formula. Niente bonus, niente formule, niente riferimenti a caratteristiche: da «mostra
/// la nota dell'Ira» a «applica il +2 al danno» il passo sembra breve ed è un burrone — semantica
/// D&amp;D senza fondo, e descrizioni ufficiali che questo repo non ha licenza di ridistribuire.
/// La scheda cartacea dell'utente è la prova che la sua prosa basta.
/// </summary>
public class CharacterFeature
{
    /// <summary>Il nome del privilegio annotato. Si confronta normalizzato
    /// (<c>CatalogKey.NormalizeName</c>), mai per uguaglianza cruda.</summary>
    public string Nome { get; set; } = string.Empty;

    /// <summary>Cosa fa, con le parole dell'utente. È il prodotto della funzione, non un ripiego.</summary>
    public string Nota { get; set; } = string.Empty;

    /// <summary>Momento del turno: <c>azione</c>, <c>bonus</c>, <c>reazione</c>, <c>passivo</c>,
    /// <c>turno</c>. <b>Null significa «da classificare»</b> e si rende come tale: in combattimento
    /// un tag indovinato è peggio di un tag mancante.</summary>
    public string? Azione { get; set; }

    /// <summary>Nome della <see cref="ClassResource"/> collegata, se il privilegio ha usi da
    /// contare. Null quando non ne ha.</summary>
    public string? Risorsa { get; set; }

    /// <summary>Se true la scheda mostra l'interruttore «attivo». Lo stato acceso NON sta qui:
    /// vive in localStorage (v. spec, D4), perché scriverlo su characters significherebbe un
    /// Update di riga intera a ogni accensione.</summary>
    public bool Attivabile { get; set; }
}
```

- [ ] **Passo 2: la proprietà su `Character`**

Accanto a `ClassResources` (riga 127), con `[Column("character_features")]`.

- [ ] **Passo 3: `CloneCharacter`**

Copia **per valore**, come già fa per `ClassResources` alle righe 61-70 — `= c.CharacterFeatures`
condividerebbe la lista fra la bozza e l'originale, e l'annullamento non annullerebbe niente:

```csharp
        CharacterFeatures = c.CharacterFeatures.Select(f => new CharacterFeature
        {
            Nome = f.Nome,
            Nota = f.Nota,
            Azione = f.Azione,
            Risorsa = f.Risorsa,
            Attivabile = f.Attivabile,
        }).ToList(),
```

- [ ] **Passo 4: eseguire `CharacterCloneTests` PRIMA di scrivere il passo 3**

Run: `dotnet test Tests/DndCompanion.Tests.csproj --filter "FullyQualifiedName~CharacterCloneTests"`
Atteso **dopo il passo 2 e prima del passo 3**: **rosso** — il test confronta ogni proprietà per
riflessione e vede il campo nuovo non copiato. È la rete che il progetto ha messo dopo aver
dimenticato due volte lo stesso passaggio: **vederla scattare è parte del lavoro**. Poi scrivere il
passo 3 e riverificare il verde.

- [ ] **Passo 5: normalizzazione in lettura**

In `CharacterRepository`, accanto alla riga 34, aggiungere
`pg.CharacterFeatures = CharacterFeatureRules.Normalizza(pg.CharacterFeatures);`.
**Dipende dal Task 4**: se questo task viene eseguito per primo, lasciare il punto e collegarlo
appena `CharacterFeatureRules` esiste — ma non dimenticarlo, o un jsonb malformato aprirebbe la
strada a una scheda che non si apre.

- [ ] **Passo 6: il file di migrazione**

`supabase/migrations/20260808000000_character_features.sql`. **Serve a `supabase db reset` per lo
stack di test locale, non è un'istruzione per l'utente** (regola del 2026-08-08):

```sql
-- Annotazioni dell'utente sui privilegi (v. docs/superpowers/specs/2026-08-08-vista-di-gioco-design.md).
-- I NOMI dei privilegi non stanno qui: si derivano dal pacchetto SRD a ogni render. Qui va solo ciò
-- che nessuna altra fonte può conoscere — le parole del giocatore.
-- Un jsonb e non cinque colonne: postgrest-csharp serializza OGNI colonna mappata a ogni Update,
-- quindi ogni colonna nuova è un'esposizione al difetto che il 2026-08-08 ha bloccato per due giorni
-- tutte le scritture su characters.
ALTER TABLE "public"."characters"
    ADD COLUMN IF NOT EXISTS "character_features" jsonb DEFAULT '[]'::jsonb NOT NULL;

COMMENT ON COLUMN "public"."characters"."character_features" IS
    'Annotazioni sui privilegi: nome (chiave), nota, tag di economia d''azione, risorsa collegata, attivabile.';
```

- [ ] **Passo 7: verificare eseguendo, non leggendo**

Una migrazione **non è verificata** finché non gira contro un Postgres vero (regola del 2026-08-06).
Se Docker è spento, **accenderlo**: i test saltati non sono test verdi.

```bash
supabase start && supabase db reset
dotnet test Tests.Integration/
```

`supabase start` da solo **non riapplica** le migrazioni se il volume esiste già: serve `db reset`.

- [ ] **Passo 8: build**

Run: `dotnet build` → 0/0.

---

### Task 4: `CharacterFeatureRules`

**File:**
- Crea: `Services/CharacterFeatureRules.cs`
- Crea: `Tests/CharacterFeatureRulesTests.cs`

**Interfacce:**
- Consuma: `CharacterFeature` (Task 3), `CatalogKey.NormalizeName`.
- Produce, per i Task 3 (passo 5), 5, 7, 8:
  ```csharp
  public static class CharacterFeatureRules
  {
      public static IReadOnlyList<string> TagAmmessi { get; }        // azione, bonus, reazione, passivo, turno
      public static List<CharacterFeature> Normalizza(IEnumerable<CharacterFeature?>? voci);
      public static string? AzioneSuggerita(string? nomeClasse, string? nomePrivilegio);

      /// <summary>La tabella curata, esposta al solo test che la incrocia col pacchetto SRD.
      /// Chiave esterna: classe normalizzata. Chiave interna: privilegio normalizzato.</summary>
      internal static IReadOnlyDictionary<string, Dictionary<string, string>> TabellaPerTest { get; }
  }
  ```
  `TabellaPerTest` è `internal`: il progetto ha già `InternalsVisibleTo` per i test (v.
  `CharacterSpellJoin`, che è `internal` per la stessa ragione). **Verificare che l'attributo ci sia
  nel `.csproj` prima di scrivere**; se manca, esporre `public` invece di aggiungerlo.

**Modello da rispecchiare:** `Services/ClassResourceRules.cs` per intero — la mappa curata
`PerClasse` (righe 39-54), la tolleranza al malformato di `Normalizza`, e il test che incrocia la
mappa col pacchetto SRD (`Tests/ClassResourceRulesTests.cs:278-310`). **Leggerlo prima di scrivere.**

- [ ] **Passo 1: i test che falliscono**

`Tests/CharacterFeatureRulesTests.cs`. Copiare `PercorsoPacchetto()` e `CaricaPacchetto()` da
`ClassResourceRulesTests.cs:14-38` — sono già duplicati fra i test del progetto, non è il momento di
estrarli.

```csharp
    [Fact]
    public void Normalizza_ScartaLeVociSenzaNome()
    {
        var voci = new List<CharacterFeature?>
        {
            new() { Nome = "  ", Nota = "x" },
            null,
            new() { Nome = "Ira", Nota = "3/riposo lungo" },
        };

        var esito = CharacterFeatureRules.Normalizza(voci);

        Assert.Single(esito);
        Assert.Equal("Ira", esito[0].Nome);
    }

    /// <summary>Un tag ignoto torna a null («da classificare»), non a un valore indovinato:
    /// in combattimento un tag sbagliato è peggio di un tag mancante.</summary>
    [Fact]
    public void Normalizza_TagIgnoto_DiventaNull()
    {
        var esito = CharacterFeatureRules.Normalizza(new List<CharacterFeature?>
        {
            new() { Nome = "Ira", Azione = "azione bonus lunga" },
        });

        Assert.Null(esito[0].Azione);
    }

    [Fact]
    public void Normalizza_TagAmmessoMaConMaiuscole_SiRiportaAlValoreCanonico()
    {
        var esito = CharacterFeatureRules.Normalizza(new List<CharacterFeature?>
        {
            new() { Nome = "Ira", Azione = "BONUS" },
        });

        Assert.Equal("bonus", esito[0].Azione);
    }

    /// <summary>«Ira» e «IRA» non sopravvivono entrambe: stessa regola di ClassResourceRules.</summary>
    [Fact]
    public void Normalizza_ScartaIDuplicatiPerNomeNormalizzato()
    {
        var esito = CharacterFeatureRules.Normalizza(new List<CharacterFeature?>
        {
            new() { Nome = "Ira", Nota = "prima" },
            new() { Nome = "IRA", Nota = "seconda" },
        });

        Assert.Single(esito);
        Assert.Equal("prima", esito[0].Nota);
    }

    [Fact]
    public void Normalizza_NullOListaVuota_NonSolleva()
    {
        Assert.Empty(CharacterFeatureRules.Normalizza(null));
        Assert.Empty(CharacterFeatureRules.Normalizza(new List<CharacterFeature?>()));
    }

    /// <summary>La rete che tiene questa tabella onesta: ogni nome suggerito deve esistere DAVVERO
    /// fra i features del pacchetto SRD della sua classe. Se un nome viene ribattezzato nel
    /// pacchetto, questo test diventa rosso da solo — senza che nessuno debba ricordarsene.
    /// Stessa costruzione di ClassResourceRulesTests sulla mappa delle risorse.</summary>
    [Fact]
    public void AzioneSuggerita_OgniNomeInTabellaEsisteNelPacchetto()
    {
        var pacchetto = CaricaPacchetto();

        foreach (var (nomeClasse, privilegi) in CharacterFeatureRules.TabellaPerTest)
        {
            var classe = pacchetto.Classes.FirstOrDefault(
                c => CatalogKey.NormalizeName(c.Name) == nomeClasse);
            Assert.True(classe is not null, $"Classe non nel pacchetto: {nomeClasse}");

            var featuresDelPacchetto = classe!.Levels
                .SelectMany(l => l.Features)
                .Select(CatalogKey.NormalizeName)
                .ToHashSet(StringComparer.Ordinal);

            foreach (var nomePrivilegio in privilegi.Keys)
            {
                Assert.True(featuresDelPacchetto.Contains(nomePrivilegio),
                    $"«{nomePrivilegio}» non esiste fra i features di {classe.Name} nel pacchetto SRD.");
            }
        }
    }
```

- [ ] **Passo 2: eseguire e verificare il fallimento**

Run: `dotnet test Tests/DndCompanion.Tests.csproj --filter "FullyQualifiedName~CharacterFeatureRulesTests"`
Atteso: **errore di compilazione**, la classe non esiste.

- [ ] **Passo 3: implementare**

La mappa curata copre le classi di cui si conoscono con certezza i nomi SRD. **Cominciare dal
Barbaro** (è la classe in uso e la sola verificabile contro la scheda cartacea), e aggiungere le
altre solo se i nomi si riscontrano nel pacchetto — il test del passo 1 lo impone comunque:

```csharp
    private static readonly Dictionary<string, Dictionary<string, string>> PerClasse =
        new(StringComparer.Ordinal)
        {
            [CatalogKey.NormalizeName("Barbaro")] = new(StringComparer.Ordinal)
            {
                [CatalogKey.NormalizeName("Ira")] = "bonus",
                [CatalogKey.NormalizeName("Difesa senza armatura")] = "passivo",
                [CatalogKey.NormalizeName("Attacco temerario")] = "azione",
                [CatalogKey.NormalizeName("Senso del pericolo")] = "passivo",
                [CatalogKey.NormalizeName("Attacco extra")] = "azione",
            },
            // Altre classi: solo nomi riscontrati nel pacchetto — il test li verifica.
        };
```

`Normalizza` ricalca `ClassResourceRules.Normalizza`: scarta le voci null e senza nome, riporta
`Azione` a un valore ammesso (confronto case-insensitive, **default null** e non «azione»), tronca
`Nota` a un tetto (2000 caratteri: una nota è un riassunto, non un capitolo), scarta i duplicati per
nome normalizzato tenendo la prima occorrenza.

- [ ] **Passo 4: verificare il verde**

Run: `dotnet test Tests/DndCompanion.Tests.csproj --filter "FullyQualifiedName~CharacterFeatureRulesTests"`

- [ ] **Passo 5: collaudo per mutazione del test incrociato**

Aggiungere alla mappa un nome inventato (`[CatalogKey.NormalizeName("Ira Fortissima")] = "bonus"`).
Rieseguire: **deve diventare rosso**. Toglierlo e riverificare il verde. Senza questa prova il test
potrebbe non stare guardando il pacchetto.

- [ ] **Passo 6: collegare il passo 5 del Task 3**

Aggiungere la normalizzazione in `CharacterRepository` se non è già stata collegata. **Un pezzo
scritto e non innestato resta morto**: è già successo due volte in questo repo.

---

### Task 5: `CharacterFeatureJoin`

**File:**
- Crea: `Services/CharacterFeatureJoin.cs`
- Crea: `Tests/CharacterFeatureJoinTests.cs`

**Interfacce:**
- Consuma: `CharacterFeature` (Task 3), `CharacterFeatureRules.AzioneSuggerita` (Task 4),
  `ClassProgression.PrivilegiFinoAl(string? testo, int livello) → IReadOnlyList<ClassLevelRow>`
  con `ClassLevelRow(int Livello, IReadOnlyList<string> Privilegi, IReadOnlyList<int> Slot)`,
  `SubclassCatalog.PrivilegiFinoAl(PackageSubclass?, int) → IReadOnlyList<ClassLevelRow>`,
  `CharacterManualJoin.TalentiRiconosciuti(string?, IReadOnlyList<PackageFeat>) → IReadOnlyList<PackageFeat>`,
  `Models.ClassResource`, `CatalogKey.NormalizeName`.
- Produce, per i Task 7 e 8:
  ```csharp
  public sealed record VistaPrivilegio(
      string Nome,
      string Nota,
      string? Azione,
      string Origine,               // "classe" | "sottoclasse" | "talento" | "propria"
      ClassResource? Contatore,
      bool Attivabile,
      int? SbloccatoAlLivello);     // null per talenti e voci proprie

  public sealed record GruppoPrivilegi(string Tag, string Etichetta, IReadOnlyList<VistaPrivilegio> Voci);

  public static class CharacterFeatureJoin
  {
      public static IReadOnlyList<VistaPrivilegio> Costruisci(
          string? classProgressionText, int livello, string? nomeClasse,
          PackageSubclass? sottoclasse, string? testoTalenti, IReadOnlyList<PackageFeat> catalogoTalenti,
          IEnumerable<CharacterFeature>? annotazioni, IEnumerable<ClassResource>? contatori);

      public static IReadOnlyList<GruppoPrivilegi> Raggruppa(IReadOnlyList<VistaPrivilegio> voci);
  }
  ```

**Modello da rispecchiare:** `Services/CharacterSpellJoin.cs` (join puro, nessuno stato, nessuna I/O)
e `Services/CharacterManualJoin.cs`.

**Regole del join, non negoziabili:**
1. L'aggancio annotazione ↔ privilegio è **sempre** `CatalogKey.NormalizeName`.
2. Un'annotazione la cui chiave non corrisponde a nessun derivato → `Origine = "propria"`, **mai scartata**.
3. Il tag effettivo è: quello dell'annotazione se valorizzato, **altrimenti** `AzioneSuggerita`,
   **altrimenti** null. L'utente vince sempre sulla tabella.
4. `Contatore` è la `ClassResource` il cui nome normalizzato coincide con `CharacterFeature.Risorsa`;
   se `Risorsa` è null si tenta il **nome del privilegio stesso** (l'Ira si aggancia da sola), e se
   non trova nulla resta null.
5. `Raggruppa` produce i gruppi nell'ordine `azione`, `bonus`, `reazione`, `turno`, *(senza tag)*, e
   **`passivo` per ultimo** — il chiamante lo rende in una sezione a parte. Gruppi vuoti omessi.
   Etichette: «Azione», «Azione bonus», «Reazione», «Una volta per turno», «Da classificare», «Passivi».
6. Dentro ogni gruppo l'ordine è quello di sblocco (livello crescente), poi alfabetico.

- [ ] **Passo 1: i test che falliscono**

Dati costruiti a mano: è un helper puro, nessun accesso al pacchetto. La progressione si serializza
col formato che `ClassProgression.Leggi` sa rileggere — **usare `ClassProgression.Serializza` per
costruirla**, non scrivere la stringa a mano, o il test collauderebbe un formato inventato.

```csharp
    private static string ProgressioneBarbaro() => ClassProgression.Serializza(new[]
    {
        new PackageClassLevel { Level = 1, Features = new() { "Ira", "Difesa senza armatura" } },
        new PackageClassLevel { Level = 2, Features = new() { "Attacco temerario" } },
        new PackageClassLevel { Level = 5, Features = new() { "Attacco extra" } },
    });

    private static IReadOnlyList<VistaPrivilegio> Costruisci(
        IEnumerable<CharacterFeature>? annotazioni = null,
        IEnumerable<ClassResource>? contatori = null,
        int livello = 5) =>
        CharacterFeatureJoin.Costruisci(
            ProgressioneBarbaro(), livello, "Barbaro",
            sottoclasse: null, testoTalenti: null, catalogoTalenti: Array.Empty<PackageFeat>(),
            annotazioni, contatori);

    [Fact]
    public void Costruisci_DerivaINomiDallaProgressioneFinoAlLivello()
    {
        var voci = Costruisci(livello: 2);

        Assert.Equal(new[] { "Ira", "Difesa senza armatura", "Attacco temerario" }.OrderBy(x => x),
                     voci.Select(v => v.Nome).OrderBy(x => x));
        Assert.DoesNotContain(voci, v => v.Nome == "Attacco extra");   // è del livello 5
    }

    [Fact]
    public void Costruisci_SenzaAnnotazioni_LaNotaEVuotaMaLaVoceCE()
    {
        var voce = Assert.Single(Costruisci(livello: 1).Where(v => v.Nome == "Ira"));

        Assert.Equal(string.Empty, voce.Nota);
        Assert.Equal("classe", voce.Origine);
        Assert.Equal(1, voce.SbloccatoAlLivello);
    }

    /// <summary>L'aggancio è per nome NORMALIZZATO: «IRA» annota «Ira». Il caso diverso non è un
    /// dettaglio estetico — è ciò che rende il test non vacuo.</summary>
    [Fact]
    public void Costruisci_AgganciaLAnnotazionePerNomeNormalizzato()
    {
        var voci = Costruisci(new[] { new CharacterFeature { Nome = "IRA", Nota = "3/riposo lungo" } });

        Assert.Equal("3/riposo lungo", voci.Single(v => v.Nome == "Ira").Nota);
    }

    /// <summary>Un'annotazione che non corrisponde a nessun privilegio derivato NON si scarta: è
    /// una voce propria. È il meccanismo con cui l'utente aggiunge i tratti di specie, che il
    /// pacchetto SRD non sa separare (spec D7).</summary>
    [Fact]
    public void Costruisci_AnnotazioneOrfana_DiventaVocePropriaENonSiPerde()
    {
        var voci = Costruisci(new[]
        {
            new CharacterFeature { Nome = "Scarica di adrenalina", Nota = "bonus action, +PF" },
        });

        var propria = Assert.Single(voci.Where(v => v.Nome == "Scarica di adrenalina"));
        Assert.Equal("propria", propria.Origine);
        Assert.Null(propria.SbloccatoAlLivello);
    }

    /// <summary>Il tag dell'utente vince sulla tabella curata. Il valore scelto è ciò che rende il
    /// test non vacuo: «Ira» in tabella è "bonus", quindi l'annotazione DEVE dire altro — con
    /// "bonus" il test passerebbe anche invertendo la precedenza.</summary>
    [Fact]
    public void Costruisci_IlTagDellUtenteVinceSullaTabellaCurata()
    {
        var voci = Costruisci(new[]
        {
            new CharacterFeature { Nome = "Ira", Azione = "azione" },   // la tabella dice "bonus"
        });

        Assert.Equal("azione", voci.Single(v => v.Nome == "Ira").Azione);
    }

    [Fact]
    public void Costruisci_SenzaTagDellUtente_UsaLaTabellaCurata()
    {
        Assert.Equal("bonus", Costruisci().Single(v => v.Nome == "Ira").Azione);
    }

    /// <summary>Con Risorsa null il contatore si aggancia per nome del privilegio: l'Ira trova da
    /// sola i propri pallini, senza che l'utente debba collegarli a mano.</summary>
    [Fact]
    public void Costruisci_ContatoreAgganciatoPerNomeQuandoRisorsaENull()
    {
        var voci = Costruisci(
            annotazioni: new[] { new CharacterFeature { Nome = "Ira", Risorsa = null } },
            contatori: new[] { new ClassResource { Nome = "Ira", Max = 3, Spesi = 1, Ricarica = "lungo" } });

        var ira = voci.Single(v => v.Nome == "Ira");
        Assert.NotNull(ira.Contatore);
        Assert.Equal(3, ira.Contatore!.Max);
    }

    [Fact]
    public void Raggruppa_MettePassiviPerUltimoEOmetteIGruppiVuoti()
    {
        var gruppi = CharacterFeatureJoin.Raggruppa(Costruisci());

        Assert.DoesNotContain(gruppi, g => g.Voci.Count == 0);
        Assert.Equal("passivo", gruppi[^1].Tag);      // «Difesa senza armatura» è passivo in tabella
    }
```

- [ ] **Passo 2: eseguire e verificare il fallimento**

Run: `dotnet test Tests/DndCompanion.Tests.csproj --filter "FullyQualifiedName~CharacterFeatureJoinTests"`
Atteso: **errore di compilazione**, i tipi non esistono.

- [ ] **Passo 3: implementare**

`Costruisci` in tre passaggi, senza scorciatoie:

1. Costruisce l'elenco **derivato** — `ClassProgression.PrivilegiFinoAl` (origine `classe`,
   `SbloccatoAlLivello` = `ClassLevelRow.Livello`), poi `SubclassCatalog.PrivilegiFinoAl`
   (`sottoclasse`), poi `CharacterManualJoin.TalentiRiconosciuti` (`talento`,
   `SbloccatoAlLivello` null, e la **nota preimpostata alla descrizione ufficiale del talento** —
   quella il pacchetto ce l'ha).
2. Indicizza le annotazioni per `CatalogKey.NormalizeName(Nome)` e le fonde sui derivati.
3. Le annotazioni rimaste senza corrispondenza diventano voci `propria`, **in coda**.

`Raggruppa` ordina i gruppi `azione`, `bonus`, `reazione`, `turno`, senza-tag, `passivo`, e dentro
ciascuno ordina per `SbloccatoAlLivello` (i null in fondo) e poi per nome.

- [ ] **Passo 4: verificare il verde**
- [ ] **Passo 5: collaudo per mutazione della regola 3**

Invertire la precedenza (tabella prima dell'utente). Il test «il tag dell'utente vince» **deve
diventare rosso**. Ripristinare. Se resta verde, il test usa un caso in cui i due tag coincidono ed
è vacuo — è esattamente l'errore della Costituzione 14 del 2026-08-06.

---

### Task 6: `ActiveEffectsService`

**File:**
- Crea: `Services/ActiveEffectsService.cs`
- Modifica: `Program.cs` (accanto a `AddSingleton<CampaignStateService>()`, riga 14)

**Interfacce:**
- Consuma: `IJSRuntime`.
- Produce, per i Task 7 e 8:
  ```csharp
  public sealed class ActiveEffectsService
  {
      public event Action? Changed;
      public Task EnsureLoadedAsync();
      public bool IsActive(string characterId, string nomePrivilegio);
      public IReadOnlyCollection<string> ActiveFor(string characterId);
      public Task ToggleAsync(string characterId, string nomePrivilegio);
      public Task ClearAsync(string characterId);
  }
  ```

**Modello da rispecchiare:** `Services/CampaignStateService.cs:55-100` — inizializzazione idempotente
con `_initialization ??= LoadStateAsync()` e lo **scarto del Task fallito dal chiamante** (righe
55-70, il commento spiega perché lì e non dentro). Copiare quella struttura, non improvvisarne una.

**Perché non su `characters`:** l'Ira si accende e si spegne più volte a sessione; ogni interruttore
sarebbe un `Update` di riga intera da 113 colonne, last-write-wins, con rifiuto RLS silenzioso.
**Perché non in sola memoria:** su un telefono al tavolo il sistema operativo chiude la scheda a ogni
distrazione. Chiave `localStorage`: `dnd_active_effects`, valore JSON `{ "<characterId>": ["Ira"] }`.
I nomi si confrontano **normalizzati**.

- [ ] **Passo 1: implementare il servizio** (nessun test unitario: dipende da `IJSRuntime`, che non è
      istanziabile in un test — è lo stesso motivo per cui `SessionFreshness` è stato estratto da
      `SupabaseService`. Se emergesse logica pura degna di test, estrarla in un helper).
- [ ] **Passo 2: registrare in `Program.cs`** come `AddSingleton`.
- [ ] **Passo 3: build** → `dotnet build`, 0/0.

---

### Task 7: le schede-privilegio

**File:**
- Crea: `Shared/CharacterTabs/CharacterFeaturesSection.razor` + `.razor.css`

**Interfacce:**
- Consuma: `GruppoPrivilegi`, `VistaPrivilegio` (Task 5), `ActiveEffectsService` (Task 6),
  `CharacterFeatureRules.TagAmmessi` (Task 4).
- Produce, per il Task 8:
  ```razor
  [Parameter, EditorRequired] public IReadOnlyList<GruppoPrivilegi> Gruppi { get; set; }
  [Parameter, EditorRequired] public string CharacterId { get; set; }
  [Parameter] public int LivelloAttuale { get; set; }
  [Parameter] public bool CanEdit { get; set; }
  [Parameter] public bool IsSaving { get; set; }
  [Parameter] public EventCallback<CharacterFeature> OnSalvaAnnotazione { get; set; }
  [Parameter] public EventCallback<VistaPrivilegio> OnEliminaVocePropria { get; set; }
  [Parameter] public EventCallback<(string Nome, int Usi)> OnSpendiContatore { get; set; }
  ```
  `OnSalvaAnnotazione` copre **sia** la modifica di una voce esistente **sia** l'aggiunta di una voce
  propria: sono la stessa operazione — si scrive un `CharacterFeature` nel jsonb, e se il suo nome
  non corrisponde a nessun derivato il join lo renderà come voce propria. Un callback separato per
  l'aggiunta creerebbe due strade per lo stesso effetto.

Rende i gruppi **escluso `passivo`**, che il Task 8 mette nella sua sezione. Ogni scheda: nome, nota
(o l'invito a scriverla se vuota), i pallini del contatore quando c'è, l'interruttore «attivo» quando
`Attivabile`, e il pulsante di modifica inline — **stesso schema di `resource-edit-btn` in
`CharacterCombatTab.razor:186-190` e del pannello a righe 208-240**: leggerlo e rispecchiarlo.

- [ ] **Passo 1: markup dei gruppi e delle schede**
- [ ] **Passo 2: pannello di modifica inline** (nota, tag, risorsa collegata, attivabile) su bozza,
      con Salva/Annulla — **mai `@bind` diretto sul dato**, per la stessa ragione delle monete.
- [ ] **Passo 3: pulsante «+ Aggiungi voce»**, visibile solo se `CanEdit`, che apre lo **stesso**
      pannello del passo 2 col nome modificabile invece che fisso. È il meccanismo con cui l'utente
      inserisce ciò che il pacchetto SRD non sa separare — i tratti di specie (spec D7) e le note
      operative che non sono privilegi. **Senza questo passo la spec D7 non ha implementazione** e
      quei tratti resterebbero irraggiungibili.
      Rispecchiare `OpenAddResourceForm` / `resource-form` in `CharacterCombatTab.razor:126-178`.
- [ ] **Passo 4: contrassegno «nuovo»** sulle schede con
      `SbloccatoAlLivello == LivelloAttuale`. Nessuno stato da persistere e niente da far sparire:
      «recente» significa «sbloccato al livello a cui sono adesso», e smette di esserlo da sé al
      level-up successivo. La spec esclude una **sezione** dedicata, non il contrassegno.
- [ ] **Passo 5: eliminazione di una voce propria** dietro `ConfirmDialog`, mai `confirm()`.
      Le voci **derivate** non si eliminano: si può solo svuotarne la nota. Cancellare «Ira» dal
      jsonb non la farebbe sparire dalla scheda — tornerebbe al render successivo senza nota, e il
      pulsante sembrerebbe rotto.
- [ ] **Passo 6: a11y** — i pallini e l'interruttore non sono `<button>`: servono `role="button"`,
      `tabindex="0"`, `aria-pressed`, `aria-label` e `OnKey`. Copiare da `CharacterCombatTab.razor:180-205`.
- [ ] **Passo 7: CSS** con i soli design token. **Le regole del genitore non arrivano qui**: quel che
      serve va scritto in questo `.razor.css` o promosso in `app.css`.
- [ ] **Passo 8: build** → 0/0.

---

### Task 8: innesto, strip «ATTIVO», passivi e wiring

**File:**
- Crea: `Shared/CharacterTabs/CharacterActiveStrip.razor` + `.razor.css`
- Modifica: `Shared/CharacterTabs/CharacterCombatTab.razor`
- Modifica: `Pages/Characters.razor` (etichetta del tab riga 102-104; rendering righe 193-199; wiring)

**Interfacce:** consuma tutto quanto sopra. Non produce nulla.

**Ordine verticale da rispettare** (spec, sezione «L'ordine verticale»): barra vitali · TS morte ·
**strip ATTIVO** · velocità/ispirazione · armi · **privilegi per economia d'azione** · **passivi** ·
caratteristiche e abilità · divisore «a fine combattimento» · dadi vita e riposi.

- [ ] **Passo 1: `CharacterActiveStrip.razor`** — chip degli attivi con la **nota dell'utente**, e
      un modo per spegnerli. Invisibile se non c'è niente di attivo né di attivabile.
- [ ] **Passo 2: sezione PASSIVI** dentro `CharacterCombatTab` — riga singola per voce, nessun
      contatore, nessun interruttore.
- [ ] **Passo 3: restringere la sezione RISORSE esistente** ai contatori **senza** scheda collegata.
      Un dato, una sola superficie primaria: se «Ira» ha la sua scheda, i suoi pallini stanno **solo** lì.
- [ ] **Passo 4: wiring in `Pages/Characters.razor`** — costruire la vista con
      `CharacterFeatureJoin.Costruisci` usando i cataloghi **già in cache** (`packageFeats`,
      `SottoclasseSelezionata`, `ClassProgressionText`): **nessuna chiamata di rete nuova**.
      Il salvataggio di un'annotazione passa da `SaveCharacterAsync`, come gli altri tab.
- [ ] **Passo 5: rinominare l'etichetta del tab** da «Tiri» a «Gioco» (riga 102-104). Solo
      l'etichetta: `activeTab == "rolls"` resta invariato, o si rompono i riferimenti alle righe
      461 e 1099-1100.
- [ ] **Passo 6: build e test** → `dotnet build` 0/0; `dotnet test Tests/DndCompanion.Tests.csproj` verde.

---

## Gate e consegna

**Il gate a due agenti si applica a ogni task**, secondo la tabella di `CLAUDE.md`:
UI e refactor circoscritti → 1 giro; **Task 3 (dati, serializzazione, modello) → fino a 3 giri**.

**Il giro sulle giunture non è un doppione.** Se i task vengono affidati a implementer diversi, il
gate individuale **non può** vedere i difetti che stanno dove una fetta incontra l'altra — è
verificato due volte in questo repo, l'ultima con un BLOCCANTE che cancellava dati dopo che ogni
fetta era passata pulita. Le giunture di questo piano, da elencare **esplicitamente** nel prompt del
gate finale:

| Chi scrive | Chi legge | Cosa può rompersi |
|---|---|---|
| Task 3 (`CharacterFeature`) | `CloneCharacter`, `CharacterRepository` | campo dimenticato → il form di modifica cancella le annotazioni al primo salvataggio |
| Task 4 (`Normalizza`) | Task 3 passo 5 | mai collegato → jsonb malformato arriva alla UI |
| Task 5 (`Contatore`) | Task 8 passo 3 | doppia superficie per gli stessi pallini, o contatore che sparisce da entrambe |
| Task 6 (`ActiveEffectsService`) | Task 7, 8 | chiavi non normalizzate: «Ira» acceso, «ira» spento |
| Task 1 (`Applica`) | Task 2 | esito fallito applicato → borsello mostrato diverso da quello salvato |

**Prima del push, e nell'ordine:**

1. `supabase/verifica-schema.sh` → deve stampare `Schema allineato ai Model.`
   **Fallisce finché la colonna non è stata applicata a mano sul database hosted.**
2. La query va consegnata **in chat all'utente**, non lasciata in un file (regola del 2026-08-08).
3. `dotnet publish DndCompanion.csproj -c Release -o publish` — il nome del progetto **serve**: senza,
   il CLI prende la solution e piazza copie non trimmate accanto al `wwwroot`.
4. Servire `publish/wwwroot` **con accesso fatto e almeno una pagina di dati aperta**: la sola
   schermata di login non esercita nessuna deserializzazione Gotrue/Postgrest.
5. Elencare all'utente le verifiche manuali che il push richiede.

Il push resta **solo su richiesta esplicita dell'utente**: su `main` pubblica.
