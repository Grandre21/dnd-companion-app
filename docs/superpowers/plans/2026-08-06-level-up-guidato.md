# Level-up guidato — piano di implementazione

> **Per chi esegue:** una fetta per agente. Le fette 0 e 3 vanno da sole; 1 e 2 in parallelo.
> Spec: [`docs/superpowers/specs/2026-08-06-level-up-guidato-design.md`](../specs/2026-08-06-level-up-guidato-design.md).

**Obiettivo:** un dialogo «Sali di livello» sulla scheda del personaggio che propone i derivati
calcolati (punti ferita, dadi vita, slot incantesimo, bonus di competenza) e le sole scelte legali,
sostituendo la compilazione a mano del form di modifica.

**Architettura:** un helper puro `LevelUpPlanner` produce un `LevelUpPlan` — un diff *proposto*, mai
applicato da solo — che si **rigenera a ogni risposta** dell'utente, perché l'incremento di
Costituzione ha effetto retroattivo sui punti ferita. `Applica` è separata e scrive solo su una
whitelist di colonne. La UI è un visualizzatore di piano: non calcola e non salva.

**Stack:** Blazor WebAssembly .NET 10, xUnit, Supabase/Postgrest. Nessuna dipendenza nuova.

## Vincoli globali

Valgono per **ogni** task, senza ripeterli.

1. **Si lavora su `main`**, si committa direttamente. **Mai `git push`**: pubblica il sito.
2. **Zero migrazioni, zero colonne nuove.** Si scrive solo in colonne che esistono già.
3. **Logica di dominio in helper puri `static`**, mai nei `.razor`. Test xUnit in `Tests/`.
4. **Stile dei test:** nomi in italiano `snake_case`, `[Fact]`/`[Theory]`, come
   `Tests/ClassProgressionTests.cs`.
5. **Nomi:** classi in inglese, membri in italiano — la convenzione di `ClassProgression`.
6. **`Character.SpellcastingAbility` si salva in INGLESE MINUSCOLO**: `"intelligence"`, `"wisdom"`,
   `"charisma"`. `CharacterCalculations.ParseSpellcastingAbility` scarta tutto il resto **in
   silenzio**, e la CD incantesimi resterebbe vuota senza che nulla fallisca. Mai i nomi italiani del
   catalogo.
7. **Il nome `SceltaSottoclasse` è già occupato** (`SubclassCatalog.SceltaSottoclasse`, significa
   un'altra cosa). Le decisioni di questo piano si chiamano `Decisione*`.
8. **Non riparsare mai il formato testuale dei livelli**: si passa solo per `ClassProgression`
   (`Leggi`, `FinoAl`, `SlotFinoAl`, `PrivilegiFinoAl`, `RiguardaSottoclasse`, `Risolvi`). Uno
   `Split('\n')` dentro il planner è un difetto, non una scorciatoia.
9. Verifica prima di dire «fatto»: `dotnet build` (0 warning / 0 errori) e
   `dotnet test Tests/DndCompanion.Tests.csproj`.

## Struttura dei file

| File | Responsabilità | Fetta |
|---|---|---|
| `Services/LevelUpContracts.cs` | i record scambiati fra motore e UI. Nessuna logica. | 0 |
| `Services/LevelUpRules.cs` | mappa classe → caratteristica da incantatore; riconoscitori dei privilegi che aprono una scelta. Dati e stringhe. | 0 |
| `Tests/LevelUpRulesTests.cs` | incrocia la mappa e i riconoscitori col JSON reale. | 0 |
| `Services/LevelUpPlanner.cs` | `Pianifica` e `Applica`. Il cuore. | 1 |
| `Tests/LevelUpPlannerTests.cs` | calcoli, whitelist dei campi, end-to-end sui dati reali. | 1 |
| `Shared/CharacterTabs/LevelUpDialog.razor` (+ `.css`) | il dialogo. Non calcola, non salva. | 2 |
| `Pages/Characters.razor`, `Shared/CharacterTabs/CharacterVitalsBar.razor` | bottone, apertura, salvataggio, refresh. | 3 |

---

# FETTA 0 — contratti e regole

> **Da sola, prima di tutto.** È il 20% del lavoro che determina il 100% delle giunture: le fette 1 e
> 2 partono in parallelo *contro questi tipi* e non devono più negoziarli.

### Task 0.1: I contratti

**File:** crea `Services/LevelUpContracts.cs`

**Produce:** i tipi `Proposta<T>`, `OpzioneDecisione`, `Decisione`, `DecisioneFraOpzioni`,
`DecisionePunteggi`, `DecisioneLibera`, `Risposta`, `LevelUpPlan`.

- [ ] **Passo 1: scrivi il file per intero**

```csharp
namespace DndCompanion.Services;

/// <summary>Un valore che il dialogo mostra come diff: sempre attuale e proposto affiancati.
/// La UI decide se e come segnalare la differenza; qui non si giudica.</summary>
public sealed record Proposta<T>(T Attuale, T Proposto);

/// <summary>Una voce selezionabile. <paramref name="Descrizione"/> è il testo del catalogo, che il
/// dialogo mostra in accordion — può essere lungo (le sottoclassi sfiorano i 3.000 caratteri).</summary>
public sealed record OpzioneDecisione(string Nome, string Descrizione);

/// <summary>Qualcosa che il giocatore deve decidere per poter salire. La <paramref name="Chiave"/>
/// ha forma <c>L{livello}:{tipo}</c> — <c>L4:talento</c>, <c>L4:talento/punteggi</c>,
/// <c>L3:sottoclasse</c> — ed è la stessa che compare come prefisso nelle righe appese ai campi
/// testuali del personaggio.</summary>
public abstract record Decisione(string Chiave, string Titolo);

/// <summary>Scelta fra voci note al catalogo: sottoclasse, talento, stile di combattimento, dono
/// epico. <paramref name="Quante"/> è quante se ne devono scegliere (quasi sempre 1).</summary>
public sealed record DecisioneFraOpzioni(
    string Chiave, string Titolo, IReadOnlyList<OpzioneDecisione> Opzioni, int Quante)
    : Decisione(Chiave, Titolo);

/// <summary>La ripartizione dell'incremento di caratteristica: +2 a una, oppure +1 a due. Compare
/// solo come figlia di una <see cref="DecisioneFraOpzioni"/> in cui è stato scelto il talento
/// dell'incremento.</summary>
public sealed record DecisionePunteggi(string Chiave, string Titolo) : Decisione(Chiave, Titolo);

/// <summary>Scelta di cui il catalogo non conosce le opzioni (invocazioni occulte, metamagia,
/// maestrie). Si annota in prosa. <paramref name="Avviso"/> è il testo che spiega perché non c'è un
/// elenco. È sempre facoltativa: non blocca la conferma.</summary>
public sealed record DecisioneLibera(string Chiave, string Titolo, string Avviso)
    : Decisione(Chiave, Titolo);

/// <summary>La risposta a una <see cref="Decisione"/>. Solo il campo che compete alla forma della
/// decisione è valorizzato; gli altri restano vuoti.</summary>
public sealed record Risposta
{
    /// <summary>I nomi scelti, per <see cref="DecisioneFraOpzioni"/>.</summary>
    public IReadOnlyList<string> Scelte { get; init; } = Array.Empty<string>();

    /// <summary>Incrementi per <see cref="DecisionePunteggi"/>: chiavi inglesi minuscole
    /// (<c>strength</c>, <c>dexterity</c>, <c>constitution</c>, <c>intelligence</c>, <c>wisdom</c>,
    /// <c>charisma</c>) e valori che sommano a 2.</summary>
    public IReadOnlyDictionary<string, int> Punteggi { get; init; } =
        new Dictionary<string, int>(StringComparer.Ordinal);

    /// <summary>Il testo, per <see cref="DecisioneLibera"/>.</summary>
    public string Testo { get; init; } = string.Empty;
}

/// <summary>Cosa comporta salire di un livello, viste le risposte date finora. È un diff
/// **proposto**: nessun campo del personaggio è stato toccato.
///
/// Il piano <b>non è stabile</b>: va ricalcolato a ogni risposta, perché l'incremento di
/// Costituzione cambia i punti ferita di tutti i livelli già posseduti.</summary>
public sealed record LevelUpPlan(
    string Classe,
    int LivelloDa,
    int LivelloA,
    /// <summary>Il dado vita della classe ("d12"), per il selettore del tiro.</summary>
    string DadoVita,
    Proposta<int> PuntiFeritaMax,
    Proposta<int> PuntiFeritaCorrenti,
    Proposta<string> DadiVita,
    /// <summary>Nove valori, dal 1° al 9° cerchio.</summary>
    Proposta<IReadOnlyList<int>> SlotMax,
    Proposta<string> CaratteristicaIncantatore,
    Proposta<int> BonusCompetenza,
    IReadOnlyList<string> PrivilegiOttenuti,
    IReadOnlyList<Decisione> Decisioni,
    /// <summary>Incoerenze rilevate: si mostrano, non si correggono.</summary>
    IReadOnlyList<string> Avvisi,
    /// <summary>Il cerchio di incantesimi che si apre per la prima volta, null se nessuno.</summary>
    int? CerchioSbloccato)
{
    /// <summary>Vero se ogni decisione che blocca la conferma ha una risposta valida. Le
    /// <see cref="DecisioneLibera"/> non bloccano mai: annotare è un servizio, non un obbligo.</summary>
    public bool Completo(IReadOnlyDictionary<string, Risposta>? risposte)
    {
        foreach (var d in Decisioni)
        {
            if (d is DecisioneLibera) continue;

            if (risposte is null || !risposte.TryGetValue(d.Chiave, out var r)) return false;

            switch (d)
            {
                case DecisioneFraOpzioni f when r.Scelte.Count != f.Quante: return false;
                case DecisionePunteggi when r.Punteggi.Values.Sum() != 2: return false;
            }
        }
        return true;
    }
}
```

- [ ] **Passo 2: compila**

Esegui: `dotnet build DndCompanion.csproj`
Atteso: 0 errori, 0 warning. Non ci sono test: è un file di soli tipi.

- [ ] **Passo 3: commit**

```bash
git add Services/LevelUpContracts.cs
git commit -m "feat(level-up): i contratti fra motore e dialogo"
```

---

### Task 0.2: La mappa degli incantatori e i riconoscitori

**File:** crea `Services/LevelUpRules.cs`, `Tests/LevelUpRulesTests.cs`

**Consuma:** `CatalogKey.NormalizeName`, `Models.Packages.PackageFeat`.
**Produce:** `LevelUpRules.CaratteristicaIncantatore(string?)`, `LevelUpRules.TipoDi(string?)`,
l'enum `TipoDiScelta`, `LevelUpRules.ÈTalentoDiIncremento(PackageFeat?)`,
`LevelUpRules.CategoriaPerScelta(TipoDiScelta)`.

- [ ] **Passo 1: scrivi il test che fallisce**

In `Tests/LevelUpRulesTests.cs`. Il primo test è quello che incrocia col JSON reale: è il contratto
fra codice e dati, e senza di lui la mappa si scollega dal pacchetto in silenzio. Copia
`PercorsoPacchetto()` e `CaricaPacchetto()` da `Tests/SrdPackageContentTests.cs` (righe 34-60).

```csharp
using DndCompanion.Models.Packages;
using DndCompanion.Services;
using Xunit;

namespace DndCompanion.Tests;

public class LevelUpRulesTests
{
    // --- copia qui PercorsoPacchetto() e CaricaPacchetto() da SrdPackageContentTests ---

    [Fact]
    public void Ogni_classe_che_ha_slot_ha_una_caratteristica_da_incantatore()
    {
        var pacchetto = CaricaPacchetto();

        foreach (var classe in pacchetto.Classes)
        {
            var haSlot = classe.Levels.Any(l => l.SpellSlots.Any(s => s > 0));
            var caratteristica = LevelUpRules.CaratteristicaIncantatore(classe.Name);

            if (haSlot)
                Assert.True(caratteristica is not null,
                    $"«{classe.Name}» ha slot nel pacchetto ma nessuna caratteristica in mappa: " +
                    "la scheda mostrerebbe gli slot senza la CD degli incantesimi.");
            else
                Assert.True(caratteristica is null,
                    $"«{classe.Name}» non ha slot ma è in mappa come incantatore.");
        }
    }

    [Theory]
    [InlineData("Mago", "intelligence")]
    [InlineData("Ranger", "wisdom")]      // primaryAbility dice Destrezza: non è derivabile
    [InlineData("Paladino", "charisma")]  // primaryAbility dice «Forza e Carisma»
    [InlineData("Barbaro", null)]
    public void La_caratteristica_da_incantatore_e_in_inglese_minuscolo(string classe, string? atteso)
        => Assert.Equal(atteso, LevelUpRules.CaratteristicaIncantatore(classe));

    [Fact]
    public void Le_categorie_di_talento_che_servono_esistono_nel_pacchetto()
    {
        var pacchetto = CaricaPacchetto();
        var categorie = pacchetto.Feats.Select(f => f.Category).ToHashSet(StringComparer.Ordinal);

        Assert.Contains("Generale", categorie);
        Assert.Contains("Stile di combattimento", categorie);
        Assert.Contains("Epico", categorie);
    }

    [Fact]
    public void Il_talento_dell_incremento_si_riconosce_nel_pacchetto()
    {
        var pacchetto = CaricaPacchetto();
        var generali = pacchetto.Feats.Where(f => f.Category == "Generale").ToList();

        Assert.Single(generali, LevelUpRules.ÈTalentoDiIncremento);
    }

    [Theory]
    [InlineData("Sottoclasse del Barbaro", TipoDiScelta.Sottoclasse)]
    [InlineData("Tradizione arcana", TipoDiScelta.Sottoclasse)]
    [InlineData("Incremento punteggio caratteristica", TipoDiScelta.TalentoGenerale)]
    [InlineData("Stile di combattimento", TipoDiScelta.StileDiCombattimento)]
    [InlineData("Dono epico", TipoDiScelta.DonoEpico)]
    [InlineData("Invocazioni occulte", TipoDiScelta.Libera)]
    [InlineData("Metamagia", TipoDiScelta.Libera)]
    [InlineData("Ira", TipoDiScelta.Nessuna)]
    [InlineData(null, TipoDiScelta.Nessuna)]
    public void I_privilegi_che_aprono_una_scelta_si_riconoscono(string? privilegio, TipoDiScelta atteso)
        => Assert.Equal(atteso, LevelUpRules.TipoDi(privilegio));

    [Fact]
    public void Ogni_privilegio_del_pacchetto_che_dice_scegli_e_riconosciuto()
    {
        // Guardia contro il pacchetto che cambia sotto i piedi al codice: se una classe introduce
        // un privilegio di scelta con un nome nuovo, il dialogo lo mostrerebbe come passivo e la
        // scelta sparirebbe in silenzio. Le parole spia sono quelle dello SRD 2024.
        var pacchetto = CaricaPacchetto();
        string[] spie = { "sottoclasse", "incremento", "stile di combattimento", "dono epico",
                          "invocazioni", "metamagia", "maestria", "tradizione", "giuramento" };

        var mancanti = pacchetto.Classes
            .SelectMany(c => c.Levels)
            .SelectMany(l => l.Features)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(f => spie.Any(s => f.Contains(s, StringComparison.OrdinalIgnoreCase)))
            .Where(f => LevelUpRules.TipoDi(f) == TipoDiScelta.Nessuna)
            .ToList();

        Assert.True(mancanti.Count == 0,
            "Privilegi che sembrano una scelta ma non sono riconosciuti:\n  " +
            string.Join("\n  ", mancanti));
    }
}
```

- [ ] **Passo 2: esegui i test e verifica che falliscano**

Esegui: `dotnet test Tests/DndCompanion.Tests.csproj --filter LevelUpRulesTests`
Atteso: FAIL in compilazione — `LevelUpRules` non esiste.

- [ ] **Passo 3: scrivi l'implementazione**

```csharp
using DndCompanion.Models.Packages;

namespace DndCompanion.Services;

/// <summary>Che genere di scelta apre un privilegio di classe.</summary>
public enum TipoDiScelta
{
    Nessuna,
    Sottoclasse,
    TalentoGenerale,
    StileDiCombattimento,
    DonoEpico,
    /// <summary>Il catalogo non conosce le opzioni: si annota in prosa.</summary>
    Libera
}

/// <summary>I fatti sulle regole che il pacchetto dati non dichiara, e i riconoscitori dei
/// privilegi che aprono una scelta. Dati e stringhe: nessun calcolo.
///
/// Perché una mappa nel codice e non un campo nel pacchetto: aggiungere un campo al formato di
/// scambio significa toccare serializzazione e modelli — la fascia a tre giri di revisione, più il
/// publish trimmato obbligatorio — per dodici valori che lo SRD non cambierà. Quando il formato si
/// toccherà per altri motivi, questa mappa diventa il suo valore predefinito.</summary>
public static class LevelUpRules
{
    /// <summary>Classe → caratteristica da incantatore, **in inglese minuscolo** come vuole
    /// <see cref="CharacterCalculations"/>: gli altri valori vengono scartati in silenzio e la CD
    /// degli incantesimi resta vuota.
    ///
    /// Non è derivabile da <c>primaryAbility</c>: il Ranger dichiara «Destrezza» ma lancia con
    /// Saggezza, e il Paladino dichiara «Forza e Carisma».</summary>
    private static readonly Dictionary<string, string> PerClasse = new(StringComparer.Ordinal)
    {
        [CatalogKey.NormalizeName("Bardo")] = "charisma",
        [CatalogKey.NormalizeName("Chierico")] = "wisdom",
        [CatalogKey.NormalizeName("Druido")] = "wisdom",
        [CatalogKey.NormalizeName("Mago")] = "intelligence",
        [CatalogKey.NormalizeName("Paladino")] = "charisma",
        [CatalogKey.NormalizeName("Ranger")] = "wisdom",
        [CatalogKey.NormalizeName("Stregone")] = "charisma",
        [CatalogKey.NormalizeName("Warlock")] = "charisma",
    };

    /// <summary>Null per le classi che non incantano e per i nomi che il manuale non conosce — una
    /// classe del tavolo non deve ereditare la caratteristica di un'omonima del manuale.</summary>
    public static string? CaratteristicaIncantatore(string? nomeClasse)
    {
        if (string.IsNullOrWhiteSpace(nomeClasse)) return null;
        return PerClasse.TryGetValue(CatalogKey.NormalizeName(nomeClasse), out var v) ? v : null;
    }

    /// <summary>Le parole con cui lo SRD nomina le scelte senza elenco a catalogo.</summary>
    private static readonly string[] SenzaElenco =
        { "invocazioni", "metamagia", "maestria" };

    /// <summary>Che scelta apre questo privilegio. L'ordine dei controlli conta: «Privilegio di
    /// tradizione arcana» va letto come sottoclasse, e <see cref="ClassProgression.RiguardaSottoclasse"/>
    /// conosce già le quattro grafie che lo SRD usa.</summary>
    public static TipoDiScelta TipoDi(string? privilegio)
    {
        if (string.IsNullOrWhiteSpace(privilegio)) return TipoDiScelta.Nessuna;

        if (ClassProgression.RiguardaSottoclasse(privilegio)) return TipoDiScelta.Sottoclasse;

        if (privilegio.Contains("incremento", StringComparison.OrdinalIgnoreCase))
            return TipoDiScelta.TalentoGenerale;
        if (privilegio.Contains("stile di combattimento", StringComparison.OrdinalIgnoreCase))
            return TipoDiScelta.StileDiCombattimento;
        if (privilegio.Contains("dono epico", StringComparison.OrdinalIgnoreCase))
            return TipoDiScelta.DonoEpico;

        return SenzaElenco.Any(s => privilegio.Contains(s, StringComparison.OrdinalIgnoreCase))
            ? TipoDiScelta.Libera
            : TipoDiScelta.Nessuna;
    }

    /// <summary>La categoria di talenti da cui pescare le opzioni, null se la scelta non è un
    /// talento.</summary>
    public static string? CategoriaPerScelta(TipoDiScelta tipo) => tipo switch
    {
        TipoDiScelta.TalentoGenerale => "Generale",
        TipoDiScelta.StileDiCombattimento => "Stile di combattimento",
        TipoDiScelta.DonoEpico => "Epico",
        _ => null
    };

    /// <summary>Vero se è il talento che concede l'incremento di caratteristica — quello che apre la
    /// sotto-scelta dei punteggi. Si riconosce dalla parola, non dal nome intero: il privilegio si
    /// chiama «Incremento punteggio caratteristica» e il talento «Incremento del Punteggio di
    /// Caratteristica», e confrontarli per intero non funzionerebbe.</summary>
    public static bool ÈTalentoDiIncremento(PackageFeat? talento)
        => talento is not null
           && talento.Name.Contains("incremento", StringComparison.OrdinalIgnoreCase);
}
```

- [ ] **Passo 4: esegui i test e verifica che passino**

Esegui: `dotnet test Tests/DndCompanion.Tests.csproj --filter LevelUpRulesTests`
Atteso: PASS, tutti.

Se `Ogni_privilegio_del_pacchetto_che_dice_scegli_e_riconosciuto` fallisce, **non allargare le parole
spia del test**: è il test a dire la verità. Aggiungi il caso a `TipoDi` e riporta cosa hai trovato.

- [ ] **Passo 5: commit**

```bash
git add Services/LevelUpRules.cs Tests/LevelUpRulesTests.cs
git commit -m "feat(level-up): mappa degli incantatori e riconoscitori delle scelte"
```

---

# FETTA 1 — il motore

> **In parallelo con la fetta 2.** Consuma i tipi della fetta 0 e non li cambia. Se ti serve un tipo
> che non c'è, **fermati e chiedi**: cambiarlo qui romperebbe la fetta 2, che è già in scrittura.

### Task 1.1: `Pianifica`

**File:** crea `Services/LevelUpPlanner.cs`, `Tests/LevelUpPlannerTests.cs`

**Consuma:** i contratti (fetta 0), `LevelUpRules`, `ClassProgression`, `SubclassCatalog`,
`CharacterCalculations.GetModifier`/`GetProficiencyBonus`, `CharacterWizardLogic.BuildHitDice`.
**Produce:** `LevelUpPlanner.Pianifica(...)`.

**Firma esatta** (le fette 2 e 3 la usano così):

```csharp
public static LevelUpPlan? Pianifica(
    Character pg,
    string? testoProgressione,
    IReadOnlyList<PackageSubclass>? sottoclassi,
    IReadOnlyList<PackageFeat>? talenti,
    IReadOnlyDictionary<string, Risposta>? risposte,
    int? tiroPuntiFerita = null)
```

**Le formule, per esteso.** Salita da `pg.Level` a `pg.Level + 1`.

```
modCosPrima = GetModifier(pg.Constitution)
modCosDopo  = GetModifier(pg.Constitution + incrementoCostituzioneDalleRisposte)

mediaDado       = (facceDado / 2) + 1
guadagnoLivello = max(1, (tiroPuntiFerita ?? mediaDado) + modCosDopo)
retroattivo     = (modCosDopo - modCosPrima) * pg.Level      // i livelli già posseduti

puntiFeritaMax       = pg.MaxHitPoints + guadagnoLivello + retroattivo
puntiFeritaCorrenti  = pg.HitPoints + (puntiFeritaMax - pg.MaxHitPoints)
```

Il retroattivo è la regola, non un extra: +2 Costituzione all'8° livello vale +1 punto ferita per
**ognuno** dei livelli già posseduti. `tiroPuntiFerita` va troncato a `1..facce` prima dell'uso.

- [ ] **Passo 1: scrivi i test che falliscono**

```csharp
using DndCompanion.Models;
using DndCompanion.Models.Packages;
using DndCompanion.Services;
using Xunit;

namespace DndCompanion.Tests;

public class LevelUpPlannerTests
{
    private const string TabellaBarbaro =
        "L1 — Ira, Difesa senza armatura\n" +
        "L2 — Senso del pericolo\n" +
        "L3 — Sottoclasse del Barbaro\n" +
        "L4 — Incremento punteggio caratteristica\n" +
        "L5 — Attacco extra, Movimento veloce";

    private const string TabellaMago =
        "L1 — Lanciare incantesimi · Slot 2\n" +
        "L2 — Tradizione arcana · Slot 3\n" +
        "L3 — Slot 4/2";

    private static Character Pg(int livello = 4, int costituzione = 16, string classe = "Barbaro")
        => new()
        {
            Id = "pg-1", Name = "Grog", Class = classe, Level = livello,
            Constitution = costituzione, MaxHitPoints = 38, HitPoints = 38,
            HitDiceMax = $"{livello}d12"
        };

    [Fact]
    public void Una_classe_senza_tabella_non_produce_un_piano()
        => Assert.Null(LevelUpPlanner.Pianifica(Pg(), testoProgressione: null, null, null, null));

    [Fact]
    public void I_punti_ferita_si_sommano_ai_correnti_non_si_ricalcolano()
    {
        // 38 max, ferito a 30. Guadagno medio d12 = 7, +3 di Costituzione = +10.
        var pg = Pg();
        pg.HitPoints = 30;

        var piano = LevelUpPlanner.Pianifica(pg, TabellaBarbaro, null, null, null)!;

        Assert.Equal(48, piano.PuntiFeritaMax.Proposto);
        Assert.Equal(40, piano.PuntiFeritaCorrenti.Proposto);   // ferito resta ferito, di 8
        Assert.Equal(38, piano.PuntiFeritaMax.Attuale);
    }

    [Fact]
    public void Il_tiro_del_dado_sostituisce_la_media_e_resta_nel_range()
    {
        var piano = LevelUpPlanner.Pianifica(Pg(), TabellaBarbaro, null, null, null, tiroPuntiFerita: 12)!;
        Assert.Equal(53, piano.PuntiFeritaMax.Proposto);        // 38 + 12 + 3

        var fuori = LevelUpPlanner.Pianifica(Pg(), TabellaBarbaro, null, null, null, tiroPuntiFerita: 99)!;
        Assert.Equal(53, fuori.PuntiFeritaMax.Proposto);        // troncato a 12, non 99
    }

    [Fact]
    public void L_incremento_di_costituzione_vale_anche_per_i_livelli_gia_posseduti()
    {
        // Da 4° a 5°, Costituzione 15 → 17: il modificatore passa da +2 a +3.
        // Guadagno del livello: 7 (media d12) + 3 = 10. Retroattivo: +1 × 4 livelli già avuti = +4.
        // Tabella dedicata: TabellaBarbaro ha già un L5, e due righe con lo stesso livello
        // renderebbero ambiguo quale privilegio porta il quinto.
        const string tabella = "L1 — Ira\nL5 — Incremento punteggio caratteristica";

        var pg = Pg(livello: 4, costituzione: 15);
        var talenti = new List<PackageFeat>
        {
            new() { Id = "t/asi", Name = "Incremento del Punteggio di Caratteristica",
                    Category = "Generale", Description = "..." }
        };
        var risposte = new Dictionary<string, Risposta>
        {
            ["L5:talento"] = new() { Scelte = new[] { "Incremento del Punteggio di Caratteristica" } },
            ["L5:talento/punteggi"] = new()
            {
                Punteggi = new Dictionary<string, int> { ["constitution"] = 2 }
            }
        };

        var piano = LevelUpPlanner.Pianifica(pg, tabella, null, talenti, risposte)!;

        Assert.Equal(52, piano.PuntiFeritaMax.Proposto);        // 38 + 10 + 4
    }

    [Fact]
    public void Gli_slot_sono_assoluti_e_lunghi_nove()
    {
        var mago = Pg(livello: 2, classe: "Mago");
        var piano = LevelUpPlanner.Pianifica(mago, TabellaMago, null, null, null)!;

        Assert.Equal(9, piano.SlotMax.Proposto.Count);
        Assert.Equal(4, piano.SlotMax.Proposto[0]);
        Assert.Equal(2, piano.SlotMax.Proposto[1]);
        Assert.Equal(0, piano.SlotMax.Proposto[8]);
    }

    [Fact]
    public void Il_cerchio_che_si_apre_per_la_prima_volta_viene_segnalato()
    {
        var mago = Pg(livello: 2, classe: "Mago");
        mago.SpellSlots1Max = 3;

        var piano = LevelUpPlanner.Pianifica(mago, TabellaMago, null, null, null)!;

        Assert.Equal(2, piano.CerchioSbloccato);                // il 2° cerchio da 0 a 2
    }

    [Fact]
    public void La_caratteristica_da_incantatore_si_propone_solo_se_manca()
    {
        var mago = Pg(livello: 2, classe: "Mago");
        var piano = LevelUpPlanner.Pianifica(mago, TabellaMago, null, null, null)!;
        Assert.Equal("intelligence", piano.CaratteristicaIncantatore.Proposto);

        mago.SpellcastingAbility = "wisdom";                    // scelta del tavolo: non si tocca
        var secondo = LevelUpPlanner.Pianifica(mago, TabellaMago, null, null, null)!;
        Assert.Equal("wisdom", secondo.CaratteristicaIncantatore.Proposto);
    }

    [Fact]
    public void Il_livello_che_porta_la_sottoclasse_apre_la_scelta()
    {
        var pg = Pg(livello: 2);
        var sottoclassi = new List<PackageSubclass>
        {
            new() { Id = "x/berserker", Name = "Cammino del berserker", Description = "Furia." }
        };

        var piano = LevelUpPlanner.Pianifica(pg, TabellaBarbaro, sottoclassi, null, null)!;

        var scelta = Assert.IsType<DecisioneFraOpzioni>(Assert.Single(piano.Decisioni));
        Assert.Equal("L3:sottoclasse", scelta.Chiave);
        Assert.Equal("Cammino del berserker", Assert.Single(scelta.Opzioni).Nome);
    }

    [Fact]
    public void Una_sottoclasse_gia_scelta_non_viene_richiesta_di_nuovo()
    {
        var pg = Pg(livello: 2);
        pg.Subclass = "Cammino di casa nostra";                 // scritta a mano dal tavolo

        var piano = LevelUpPlanner.Pianifica(pg, TabellaBarbaro, null, null, null)!;

        Assert.DoesNotContain(piano.Decisioni, d => d.Chiave == "L3:sottoclasse");
    }

    [Fact]
    public void Il_talento_dell_incremento_apre_la_scelta_dei_punteggi()
    {
        var pg = Pg(livello: 3);
        var talenti = new List<PackageFeat>
        {
            new() { Id = "t/asi", Name = "Incremento del Punteggio di Caratteristica",
                    Category = "Generale", Description = "..." },
            new() { Id = "t/lottatore", Name = "Lottatore", Category = "Generale", Description = "..." }
        };

        var senzaRisposta = LevelUpPlanner.Pianifica(pg, TabellaBarbaro, null, talenti, null)!;
        Assert.Single(senzaRisposta.Decisioni);                 // solo la scelta del talento

        var risposte = new Dictionary<string, Risposta>
        {
            ["L4:talento"] = new() { Scelte = new[] { "Incremento del Punteggio di Caratteristica" } }
        };
        var conRisposta = LevelUpPlanner.Pianifica(pg, TabellaBarbaro, null, talenti, risposte)!;

        Assert.Contains(conRisposta.Decisioni, d => d is DecisionePunteggi && d.Chiave == "L4:talento/punteggi");
    }

    [Fact]
    public void I_dadi_vita_multiclasse_non_si_toccano_e_si_avvisa()
    {
        var pg = Pg();
        pg.HitDiceMax = "3d12+1d8";

        var piano = LevelUpPlanner.Pianifica(pg, TabellaBarbaro, null, null, null)!;

        Assert.Equal("3d12+1d8", piano.DadiVita.Proposto);      // invariato
        Assert.NotEmpty(piano.Avvisi);
    }
}
```

- [ ] **Passo 2: esegui i test e verifica che falliscano**

Esegui: `dotnet test Tests/DndCompanion.Tests.csproj --filter LevelUpPlannerTests`
Atteso: FAIL in compilazione — `LevelUpPlanner` non esiste.

- [ ] **Passo 3: implementa `Pianifica`**

Struttura da rispettare, in `Services/LevelUpPlanner.cs`:

1. `livelloA = pg.Level + 1`; se `pg.Level >= 20` torna `null`.
2. `ClassProgression.Leggi(testoProgressione)`; se vuoto torna `null`.
3. Riga del livello nuovo: `Leggi(...).FirstOrDefault(r => r.Livello == livelloA)`. Assente =
   nessun privilegio, **non** un errore (una classe del tavolo può avere una tabella parziale).
4. Dado vita: **dalla tabella non si ricava** — leggi le facce da `pg.HitDiceMax` con
   `CharacterCalculations.GetHitDiceTotal`? No: quello conta i dadi. Ricava le facce dal primo
   blocco `NdM` di `pg.HitDiceMax`; se non è parsabile, usa `d8` e aggiungi un avviso.
5. Punti ferita con le formule sopra.
6. Dadi vita: `CharacterWizardLogic.BuildHitDice(dado, livelloA)`, **tranne** se `pg.HitDiceMax`
   contiene un `+` (multiclasse): allora lascia il valore e aggiungi l'avviso «Dadi vita da più
   classi: aggiornali a mano».
7. Slot: `ClassProgression.SlotFinoAl(testoProgressione, livelloA)`, riempiti a 9 con zeri.
8. `CerchioSbloccato`: il primo indice `i` con `nuovi[i] > 0 && attuali[i] == 0`, più 1. Gli attuali
   si leggono dai nove `pg.SpellSlotsNMax`.
9. Caratteristica da incantatore: se `pg.SpellcastingAbility` è valorizzata **si tiene**; altrimenti
   `LevelUpRules.CaratteristicaIncantatore(pg.Class)`.
10. Decisioni: per ogni privilegio del livello nuovo, `LevelUpRules.TipoDi(privilegio)` →
    - `Sottoclasse`: **salta** se `pg.Subclass` è già valorizzata. Altrimenti `DecisioneFraOpzioni`
      con chiave `L{livelloA}:sottoclasse` e le `sottoclassi` come opzioni (`Nome`, `Description`).
      Se la lista è vuota, emetti invece una `DecisioneLibera` con l'avviso.
    - `TalentoGenerale` / `StileDiCombattimento` / `DonoEpico`: `DecisioneFraOpzioni` chiave
      `L{livelloA}:talento`, opzioni = `talenti` filtrati per
      `LevelUpRules.CategoriaPerScelta(tipo)`.
    - `Libera`: `DecisioneLibera` chiave `L{livelloA}:libera/{privilegio normalizzato}`, avviso
      «Il manuale non porta l'elenco: annota qui la tua scelta».
    - `Nessuna`: niente.
11. Figlia dei punteggi: se fra le risposte c'è `L{livelloA}:talento` con una scelta il cui talento
    soddisfa `LevelUpRules.ÈTalentoDiIncremento`, aggiungi la `DecisionePunteggi` con chiave
    `L{livelloA}:talento/punteggi`.

- [ ] **Passo 4: esegui i test e verifica che passino**

Esegui: `dotnet test Tests/DndCompanion.Tests.csproj --filter LevelUpPlannerTests`
Atteso: PASS, tutti.

- [ ] **Passo 5: commit**

```bash
git add Services/LevelUpPlanner.cs Tests/LevelUpPlannerTests.cs
git commit -m "feat(level-up): il piano si rigenera a ogni risposta"
```

---

### Task 1.2: `Applica` e la whitelist dei campi

> Questo task è l'unico che scrive su schede di produzione. Il test di whitelist non è un extra: è
> ciò che rende sicura la delega.

**File:** modifica `Services/LevelUpPlanner.cs`, `Tests/LevelUpPlannerTests.cs`

**Produce:** `LevelUpPlanner.Applica(Character pg, LevelUpPlan piano, IReadOnlyDictionary<string, Risposta>? risposte)`,
che **muta e restituisce** lo stesso `Character` ricevuto (non una copia: la scheda tiene un
riferimento vivo, e una copia farebbe divergere i tab aperti).

- [ ] **Passo 1: scrivi i test che falliscono**

```csharp
    [Fact]
    public void Applica_tocca_solo_i_campi_dichiarati()
    {
        // La whitelist: qualunque altra colonna cambi è un difetto che arriverebbe in produzione.
        var pg = Pg();
        pg.SpellSlots1Used = 2;
        pg.GoldPieces = 120;
        pg.ArmorClass = 16;
        pg.Strength = 18;

        var piano = LevelUpPlanner.Pianifica(pg, TabellaBarbaro, null, null, null)!;
        LevelUpPlanner.Applica(pg, piano, null);

        Assert.Equal(2, pg.SpellSlots1Used);       // gli slot spesi sono dati vivi
        Assert.Equal(120, pg.GoldPieces);
        Assert.Equal(16, pg.ArmorClass);
        Assert.Equal(18, pg.Strength);
        Assert.Equal(5, pg.Level);                 // questo sì
    }

    [Fact]
    public void Applica_con_risposte_incomplete_non_muta_nulla()
    {
        var pg = Pg(livello: 2);
        var sottoclassi = new List<PackageSubclass>
        {
            new() { Id = "x/b", Name = "Cammino del berserker", Description = "Furia." }
        };
        var piano = LevelUpPlanner.Pianifica(pg, TabellaBarbaro, sottoclassi, null, null)!;

        Assert.False(piano.Completo(null));
        LevelUpPlanner.Applica(pg, piano, null);

        Assert.Equal(2, pg.Level);                 // niente è cambiato
        Assert.Equal(38, pg.MaxHitPoints);
    }

    [Fact]
    public void Le_righe_appese_portano_il_prefisso_del_livello()
    {
        var pg = Pg(livello: 4);
        pg.ClassFeatures = "L1: Ira";
        var piano = LevelUpPlanner.Pianifica(pg, TabellaBarbaro, null, null, null)!;

        LevelUpPlanner.Applica(pg, piano, null);

        Assert.Contains("L1: Ira", pg.ClassFeatures);          // il testo preesistente resta
        Assert.Contains("L5: Attacco extra", pg.ClassFeatures);
        Assert.Contains("L5: Movimento veloce", pg.ClassFeatures);
    }

    [Fact]
    public void Applica_e_un_punto_fisso_di_Normalize()
    {
        // Se Normalize cambiasse qualcosa dopo Applica, il diff mostrato all'utente sarebbe stato
        // smentito subito dopo la conferma, in silenzio.
        var pg = Pg();
        var piano = LevelUpPlanner.Pianifica(pg, TabellaBarbaro, null, null, null)!;
        LevelUpPlanner.Applica(pg, piano, null);

        var pfDopoApplica = pg.MaxHitPoints;
        var dadiDopoApplica = pg.HitDiceMax;
        CharacterNormalizer.Normalize(pg);

        Assert.Equal(pfDopoApplica, pg.MaxHitPoints);
        Assert.Equal(dadiDopoApplica, pg.HitDiceMax);
    }

    [Fact]
    public void Il_giro_completo_su_un_mago_reale_dal_pacchetto()
    {
        // End-to-end: attraversa contratti, regole, planner e i dati veri. Nessuna delle altre
        // fette lo scriverebbe, perché nessuna li vede tutti insieme.
        var pacchetto = CaricaPacchetto();
        var mago = pacchetto.Classes.Single(c => c.Name == "Mago");
        var tabella = ClassProgression.Serializza(mago.Levels);

        var pg = new Character
        {
            Id = "pg-2", Name = "Elminster", Class = "Mago", Level = 2,
            Constitution = 14, MaxHitPoints = 13, HitPoints = 13, HitDiceMax = "2d6"
        };

        var piano = LevelUpPlanner.Pianifica(pg, tabella, mago.Subclasses, pacchetto.Feats, null)!;

        // Il Mago prende la sottoclasse al 3° («Tradizione arcana» nel pacchetto): finché non è
        // scelta, il piano non è confermabile.
        Assert.False(piano.Completo(null));
        Assert.Contains(piano.Decisioni, d => d.Chiave == "L3:sottoclasse");

        var risposte = new Dictionary<string, Risposta>
        {
            ["L3:sottoclasse"] = new() { Scelte = new[] { "Invocatore" } }
        };
        var conScelta = LevelUpPlanner.Pianifica(pg, tabella, mago.Subclasses, pacchetto.Feats, risposte)!;
        Assert.True(conScelta.Completo(risposte));

        LevelUpPlanner.Applica(pg, conScelta, risposte);

        Assert.Equal(3, pg.Level);
        Assert.Equal("3d6", pg.HitDiceMax);
        Assert.Equal(19, pg.MaxHitPoints);          // 13 + media d6 (4) + 2 di Costituzione
        Assert.Equal("Invocatore", pg.Subclass);
        Assert.Equal("intelligence", pg.SpellcastingAbility);
        Assert.Equal(4, pg.SpellSlots1Max);
        Assert.Equal(2, pg.SpellSlots2Max);
        Assert.Equal(0, pg.SpellSlots3Max);
    }
```

Aggiungi a questa classe `PercorsoPacchetto()`/`CaricaPacchetto()`, come nel task 0.2.

- [ ] **Passo 2: esegui e verifica che falliscano**

Esegui: `dotnet test Tests/DndCompanion.Tests.csproj --filter LevelUpPlannerTests`
Atteso: FAIL — `Applica` non esiste.

- [ ] **Passo 3: implementa `Applica`**

Regole, in ordine:

1. **Se `piano.Completo(risposte)` è falso, esci senza toccare nulla** e restituisci `pg`.
2. Scrivi **solo**: `Level`, `MaxHitPoints`, `HitPoints`, `HitDiceMax` (salvo il caso multiclasse),
   i nove `SpellSlotsNMax`, `Subclass`, `SpellcastingAbility` (**solo se era vuota**),
   `ClassFeatures`, `Feats`, e i punteggi indicati dalla `DecisionePunteggi`.
3. **Mai** i nove `SpellSlotsNUsed`.
4. Righe appese, ciascuna su una riga nuova, saltando i duplicati esatti:
   - a `ClassFeatures`: `L{livelloA}: {privilegio}` per ogni privilegio ottenuto che **non** apre una
     scelta (`TipoDi(...) == Nessuna`), più `L{livelloA}: {risposta}` per ogni `DecisioneLibera` con
     testo non vuoto.
   - a `Feats`: `L{livelloA}: {nome del talento}` per le scelte di talento.
   - la sottoclasse va in `pg.Subclass`, non fra le righe.
5. Chiudi con `CharacterNormalizer.Normalize(pg)`.

- [ ] **Passo 4: esegui e verifica che passino**

Esegui: `dotnet test Tests/DndCompanion.Tests.csproj`
Atteso: PASS su tutta la suite, non solo sui nuovi.

- [ ] **Passo 5: commit**

```bash
git add Services/LevelUpPlanner.cs Tests/LevelUpPlannerTests.cs
git commit -m "feat(level-up): Applica scrive solo sulla whitelist"
```

---

# FETTA 2 — il dialogo

> **In parallelo con la fetta 1.** Scrivi contro i tipi della fetta 0: `LevelUpPlanner` potrebbe non
> esistere ancora, e va bene — il componente **non lo chiama**. Riceve il piano già fatto.

### Task 2.1: `LevelUpDialog`

**File:** crea `Shared/CharacterTabs/LevelUpDialog.razor` e `LevelUpDialog.razor.css`

**Consuma:** `LevelUpPlan`, `Decisione*`, `Risposta`, `Proposta<T>` (fetta 0).

**Parametri esatti** (la fetta 3 li usa così):

```csharp
[Parameter, EditorRequired] public LevelUpPlan? Piano { get; set; }
[Parameter] public IReadOnlyDictionary<string, Risposta> Risposte { get; set; }
    = new Dictionary<string, Risposta>();
/// <summary>Una risposta è cambiata: il genitore ricalcola il piano e lo ripassa.</summary>
[Parameter] public EventCallback<(string Chiave, Risposta Risposta)> OnRisposta { get; set; }
/// <summary>Il tiro del dado inserito a mano, null = metodo medio.</summary>
[Parameter] public EventCallback<int?> OnTiro { get; set; }
[Parameter] public EventCallback OnConferma { get; set; }
[Parameter] public EventCallback OnAnnulla { get; set; }
[Parameter] public bool InCorso { get; set; }
```

**Vincoli non negoziabili:**

- **Nessun `@inject` di repository o servizi dati.** Il dialogo non salva e non calcola: mostra
  `Piano` e segnala le risposte. Un `@inject ICharacterRepository` qui è un difetto strutturale.
- **Nessuna chiamata a `LevelUpPlanner`.** Il ricalcolo è del genitore.
- Il bottone di conferma è **disabilitato** se `Piano is null`, se `InCorso`, o se
  `!Piano.Completo(Risposte)`.
- `InCorso` disabilita il bottone: protegge dal doppio tap, come `SaveAsync` nel wizard.

**Struttura visiva** — il guadagno prima della burocrazia, e le scelte come checklist:

```
Diventa livello {LivelloA}          ← titolo, non «Level up»
{Classe} {LivelloDa} → {LivelloA}

OTTIENI                             ← per primo: è la ricompensa
 • {PrivilegiOttenuti}

PF max      {Attuale} → {Proposto}  ← i derivati come delta
Dado vita   {Attuale} → {Proposto}
Competenza  +{Attuale} → +{Proposto}
Slot        (solo se cambiano)

(•) Media +{n}   ( ) Ho tirato → [input 1..dado]

DA SCEGLIERE                        ← righe con stato, non un wizard
 {Titolo}   ▸ da fare | ✓ {scelto}

{Avvisi}                            ← informativi, mai bloccanti

[Annulla]            [Diventa livello {LivelloA}]
```

- [ ] **Passo 1: scrivi il markup**

Il tap su una riga di `DA SCEGLIERE` espande il pannello **in linea** (accordion, una alla volta):
niente navigazione, niente secondo dialogo sopra il primo. Per `DecisioneFraOpzioni`, ogni opzione è
una riga con il nome e la descrizione troncata, espandibile. Per `DecisionePunteggi`, sei righe con
`+`/`−` e il vincolo «somma esattamente 2, massimo +2 su una sola». Per `DecisioneLibera`, una
`<textarea>` preceduta dal testo di `Avviso`.

Ogni interazione chiama `OnRisposta.InvokeAsync((chiave, nuovaRisposta))`; **non** modificare
`Risposte` in locale: la proprietà è del genitore, che ricalcola e ripassa il piano.

- [ ] **Passo 2: scrivi il CSS**

Usa i **design token** di `:root`, mai valori letterali di colore. Foglio a tutto schermo su mobile
con scroll interno e conferma sticky in fondo. Ricorda che lo scope isolato del genitore non
raggiunge questo componente: le classi che ti servono vanno definite qui.

Non riusare `ConfirmDialog` come contenitore: è un sì/no e non regge il contenuto.

- [ ] **Passo 3: compila**

Esegui: `dotnet build DndCompanion.csproj`
Atteso: 0 errori, 0 warning.

- [ ] **Passo 4: commit**

```bash
git add Shared/CharacterTabs/LevelUpDialog.razor Shared/CharacterTabs/LevelUpDialog.razor.css
git commit -m "feat(level-up): il dialogo mostra il piano e raccoglie le scelte"
```

---

# FETTA 3 — l'innesto

> **Da sola, per ultima.** Tocca file caldi e condivisi: un agente in parallelo qui produce
> conflitti garantiti.

### Task 3.1: bottone, apertura, salvataggio

**File:** modifica `Pages/Characters.razor` e `Shared/CharacterTabs/CharacterVitalsBar.razor`

**Consuma:** `LevelUpPlanner.Pianifica`/`Applica` (fetta 1), `LevelUpDialog` (fetta 2),
`ICatalogService` per classi, sottoclassi e talenti.

- [ ] **Passo 1: il bottone**

In `CharacterVitalsBar`, accanto al livello: «Sali di livello». Visibile solo a chi può già editare
la scheda — **riusa il controllo esistente**, non introdurne uno nuovo — e nascosto se
`Level >= 20`.

- [ ] **Passo 2: l'apertura e il ricalcolo**

In `Characters.razor`, lo stato: il piano corrente, le risposte, il tiro. Alla richiesta di apertura:

1. Prendi le classi di campagna e il pacchetto da `ICatalogService`.
2. `ClassProgression.Risolvi(righeDiCampagna, vociDiPacchetto, pg.Class)` per il testo della
   progressione.
3. Le sottoclassi da `SubclassCatalog.Disponibili(righeDiCampagna, classiDiManuale, pg.Class)`,
   i talenti da `Catalog.Feats`.
4. `LevelUpPlanner.Pianifica(...)`. **Se torna `null`, non aprire il dialogo**: mostra un toast
   `.app-toast` «Questa classe non ha una tabella dei livelli: modifica la scheda a mano».

A ogni `OnRisposta` e `OnTiro`: aggiorna lo stato e **richiama `Pianifica`** con le risposte nuove.
Il piano non si aggiorna da solo.

- [ ] **Passo 3: la conferma**

1. `LevelUpPlanner.Applica(pg, piano, risposte)` sull'**istanza corrente** del personaggio, non su
   una copia: i tab aperti tengono un riferimento vivo e una copia li farebbe divergere.
2. **Un solo** `UpdateCharacterAsync`. Se fallisce: `DbErrorBanner`, dialogo **aperto**, risposte
   intatte, ritentabile. Mai salvataggi parziali.
3. Se riesce: chiudi, `StateHasChanged`, toast con «Ora sei livello N» e, se
   `piano.CerchioSbloccato is not null`, la riga «Hai sbloccato il {n}° cerchio» con link al tab
   Magia.
4. Il toast porta **«Sali ancora»**, che riapre il dialogo sul livello successivo: è così che si
   recuperano più livelli, un passo alla volta.

- [ ] **Passo 4: compila e prova**

Esegui: `dotnet build DndCompanion.csproj` e `dotnet test Tests/DndCompanion.Tests.csproj`
Atteso: 0 warning, tutti i test verdi.

- [ ] **Passo 5: commit**

```bash
git add Pages/Characters.razor Shared/CharacterTabs/CharacterVitalsBar.razor
git commit -m "feat(level-up): il dialogo si apre dalla scheda e salva in un colpo"
```

---

## Dopo l'ultima fetta

1. **Gate a due agenti puntato sulle giunture** elencate nella spec, non sul diff intero. Nel prompt:
   quali fette hanno scritto cosa, e le invarianti testuali da verificare — nessun `Split('\n')` nel
   planner, nessun `@inject` di repository nel dialogo.
2. **Aggiorna `docs/DA-FARE.md`**: togli i due punti chiusi di §3, aggiungi le verifiche manuali —
   salita reale di un PG del manuale con una scelta, di un PG con classe del tavolo (il dialogo non
   deve aprirsi), di un incantatore che sblocca un cerchio.
3. **Aggiorna `docs/DIARIO.md`** con il *perché*: il retroattivo di Costituzione, il livello alla
   volta, l'app che non tira il dado.
4. **Non spingere.** Il push resta su richiesta esplicita dell'utente.
