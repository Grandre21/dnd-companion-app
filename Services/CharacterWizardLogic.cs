using DndCompanion.Models;
using DndCompanion.Models.Packages;

namespace DndCompanion.Services;

/// <summary>
/// Helper di sole funzioni pure per il wizard di creazione PG: applicazione bonus razza,
/// costruzione dado vita, suggerimento PF e tiri salvezza. Nessuno stato, nessuna I/O.
/// Stesso pattern di <see cref="CharacterCalculations"/>.
/// </summary>
public static class CharacterWizardLogic
{
    /// <summary>Bonus razziali nell'ordine canonico FOR,DES,COS,INT,SAG,CAR. race null → tutti 0.
    /// Unica fonte dell'ordinamento: usata da <see cref="FinalAbilityScores"/> e dalla UI del wizard.</summary>
    public static int[] RaceBonuses(Race? race)
        => race is null
            ? new[] { 0, 0, 0, 0, 0, 0 }
            : new[] { race.StrBonus, race.DexBonus, race.ConBonus, race.IntBonus, race.WisBonus, race.ChaBonus };

    /// <summary>Finali = base + bonus razza (ordine FOR,DES,COS,INT,SAG,CAR), clamp 1..30.
    /// race null → base clampati; baseScores più corto → mancanti = 10.</summary>
    public static int[] FinalAbilityScores(int[] baseScores, Race? race)
    {
        var bonuses = RaceBonuses(race);

        var result = new int[6];
        for (var i = 0; i < 6; i++)
        {
            var b = baseScores is not null && i < baseScores.Length ? baseScores[i] : 10;
            result[i] = Math.Clamp(b + bonuses[i], 1, 30);
        }
        return result;
    }

    /// <summary>"d12" + livello 3 → "3d12". Dado vuoto/non riconosciuto → "". livello &lt; 1 trattato 1.</summary>
    public static string BuildHitDice(string? classHitDie, int level)
    {
        var die = ParseDieSize(classHitDie);
        if (die is null) return string.Empty;
        var lvl = level < 1 ? 1 : level;
        return $"{lvl}d{die.Value}";
    }

    /// <summary>PF suggeriti (metodo medio 5e): liv1 = dado pieno + modCOS; ogni livello oltre += media
    /// del dado (arrotondata per eccesso) + modCOS. Minimo 1. Dado non riconosciuto → 0 (sentinella).</summary>
    public static int SuggestMaxHp(string? classHitDie, int conModifier, int level)
    {
        var die = ParseDieSize(classHitDie);
        if (die is null) return 0;
        var lvl = level < 1 ? 1 : level;
        var avgPerLevel = (die.Value / 2) + 1; // media di un dN arrotondata per eccesso
        var hp = die.Value + conModifier;
        for (var i = 2; i <= lvl; i++)
            hp += avgPerLevel + conModifier;
        return Math.Max(1, hp);
    }

    /// <summary>Testo libero dei tiri salvezza (es. "Forza, Costituzione") → chiavi caratteristica inglesi.
    /// Tollerante a maiuscole/spazi; voci ignote scartate; nessun duplicato; vuoto/null → lista vuota.</summary>
    public static IReadOnlyList<string> ParseSaveProficiencies(string? savingThrowsText)
    {
        if (string.IsNullOrWhiteSpace(savingThrowsText)) return Array.Empty<string>();

        var result = new List<string>();
        foreach (var raw in savingThrowsText.Split(','))
        {
            var key = raw.Trim().ToLowerInvariant() switch
            {
                "forza" => "strength",
                "destrezza" => "dexterity",
                "costituzione" => "constitution",
                "intelligenza" => "intelligence",
                "saggezza" => "wisdom",
                "carisma" => "charisma",
                _ => null
            };
            if (key is not null && !result.Contains(key)) result.Add(key);
        }
        return result;
    }

    /// <summary>Applica i tiri salvezza di classe al personaggio indicato, da testo libero ("Forza,
    /// Costituzione") via <see cref="ParseSaveProficiencies"/>. Azzera sempre le sei competenze
    /// prima di applicare: un target rimasto con le competenze di una classe abbandonata (dopo un
    /// cambio di classe) non deve conservarle senza alcun segnale.
    ///
    /// Promossa dal wizard di creazione (SERIO 4 del gate del 2026-08-06): prima viveva come metodo
    /// privato del componente, applicato solo a <c>draft</c>; ora prende un target esplicito perché
    /// serve anche sull'anteprima (<see cref="AssemblaBaseline"/>), ricalcolata a ogni render, dove
    /// mutare il draft direttamente sarebbe uno scrivere-mentre-si-legge scorretto.</summary>
    public static void ApplicaTiriSalvezze(Character target, string? savingThrowsText)
    {
        target.ProfSaveStrength = target.ProfSaveDexterity = target.ProfSaveConstitution = false;
        target.ProfSaveIntelligence = target.ProfSaveWisdom = target.ProfSaveCharisma = false;

        foreach (var key in ParseSaveProficiencies(savingThrowsText))
        {
            switch (key)
            {
                case "strength": target.ProfSaveStrength = true; break;
                case "dexterity": target.ProfSaveDexterity = true; break;
                case "constitution": target.ProfSaveConstitution = true; break;
                case "intelligence": target.ProfSaveIntelligence = true; break;
                case "wisdom": target.ProfSaveWisdom = true; break;
                case "charisma": target.ProfSaveCharisma = true; break;
            }
        }
    }

    /// <summary>Cerca una voce di catalogo per nome, ignorando maiuscole e accenti — la STESSA
    /// normalizzazione con cui <see cref="ClassProgression.Risolvi"/> trova la tabella dei livelli
    /// (<see cref="CatalogKey.NormalizeName"/>). SERIO 1 del gate del 2026-08-06: prima del wizard
    /// del passo Progressione, il dado vita si cercava con un'uguaglianza esatta sul nome mentre la
    /// tabella dei livelli si risolveva normalizzando — una classe scritta a mano con un casing
    /// diverso da quello del manuale (es. "mago" minuscolo) trovava la tabella ma non il dado vita,
    /// e lo 0 sentinella che ne seguiva bloccava "Crea personaggio" senza via d'uscita. Generico su
    /// <typeparamref name="T"/> e sul selettore del nome per riusarlo invece di ripetere la stessa
    /// LINQ a ogni ricerca di catalogo.
    ///
    /// MINORE 2 del secondo giro del gate del 2026-08-06: la normalizzazione fa combaciare anche due
    /// righe omonime che differiscono solo per maiuscole/accenti ("Città natia" e "Citta natia"), e
    /// i repository leggono senza <c>Order</c> — l'ordine con cui arrivano da PostgREST NON è
    /// garantito, quindi senza uno spareggio la voce scelta poteva cambiare da un caricamento
    /// all'altro. Per il background non è estetica: cambia <see cref="RisolviCompetenzeConcesse"/>
    /// e <see cref="BackgroundAbilityKeys"/>, cioè dati scritti sul personaggio salvato. Lo
    /// spareggio, richiesto tramite <paramref name="idOf"/>: prima il match ESATTO (ordinale, non
    /// normalizzato) sul nome cercato, poi — a parità — il minore ordinale per Id, stesso stile di
    /// <see cref="CatalogMerge.Representative{T}"/> (criterio diverso perché qui i candidati vengono
    /// già da UNA sola lista, senza la dimensione "provenienza" di quella funzione — ma la stessa
    /// idea: ordinare, poi spareggiare su un Id stabile).</summary>
    public static T? TrovaPerNomeNormalizzato<T>(
        IEnumerable<T>? voci, Func<T, string?> nomeDi, string? nome, Func<T, string> idOf)
        where T : class
    {
        var chiave = CatalogKey.NormalizeName(nome);
        var candidati = (voci ?? Enumerable.Empty<T>())
            .Where(v => CatalogKey.NormalizeName(nomeDi(v)) == chiave)
            .ToList();

        if (candidati.Count == 0) return null;

        return candidati
            .OrderBy(v => string.Equals(nomeDi(v), nome, StringComparison.Ordinal) ? 0 : 1)
            .ThenBy(idOf, StringComparer.Ordinal)
            .First();
    }

    /// <summary>Dimensione del dado dopo la prima 'd'/'D' (es. "d12"/"1d6" → 12/6). null se assente o non parsabile.</summary>
    private static int? ParseDieSize(string? hitDie)
    {
        if (string.IsNullOrWhiteSpace(hitDie)) return null;
        var lower = hitDie.ToLowerInvariant();
        var idx = lower.IndexOf('d');
        if (idx < 0 || idx + 1 >= lower.Length) return null;
        var digits = new string(lower.Skip(idx + 1).TakeWhile(char.IsDigit).ToArray());
        return int.TryParse(digits, out var n) && n > 0 ? n : (int?)null;
    }

    // =====================================================================
    // Wizard di creazione PG guidato (/characters/nuovo): metodo di
    // generazione punteggi, bonus di background (§4.7) e validazione per
    // passo. Stesso stile "sola funzione pura" del resto del file.
    // =====================================================================

    /// <summary>Ordine canonico delle sei caratteristiche, come chiavi inglesi — la stessa fonte
    /// usata da <see cref="RaceBonuses"/>/<see cref="FinalAbilityScores"/> e dalle chiavi restituite
    /// da <see cref="ParseSaveProficiencies"/>. Pubblico: la UI del wizard lo riusa per indicizzare
    /// le stesse sei posizioni invece di ridichiarare l'ordine.</summary>
    public static readonly string[] AbilityKeyOrder =
        { "strength", "dexterity", "constitution", "intelligence", "wisdom", "charisma" };

    /// <summary>I tre metodi di generazione dei punteggi base offerti dal wizard.</summary>
    public enum AbilityScoreMethod { StandardArray, PointBuy, Roll }

    /// <summary>Array standard 5e, in ordine decrescente: il giocatore assegna questi sei valori
    /// alle sei caratteristiche (uno ciascuna), non li digita liberamente.</summary>
    public static readonly int[] StandardArrayScores = { 15, 14, 13, 12, 10, 8 };

    /// <summary>Vero se <paramref name="baseScores"/> è una permutazione esatta di
    /// <see cref="StandardArrayScores"/> (stessi sei valori, uno per caratteristica, nessuno
    /// ripetuto oltre le occorrenze previste). Usato per validare il metodo "array standard".</summary>
    public static bool IsValidStandardArrayAssignment(int[]? baseScores)
        => baseScores is not null && baseScores.Length == 6
           && baseScores.OrderBy(x => x).SequenceEqual(StandardArrayScores.OrderBy(x => x));

    // Costi ufficiali 5e dell'acquisto punti: punteggi 8-15, budget 27 (nessun punteggio ammesso
    // fuori da questo intervallo con questo metodo).
    private static readonly IReadOnlyDictionary<int, int> PointBuyCosts = new Dictionary<int, int>
    {
        [8] = 0, [9] = 1, [10] = 2, [11] = 3, [12] = 4, [13] = 5, [14] = 7, [15] = 9
    };

    /// <summary>Budget totale dell'acquisto punti (regola 5e: 27).</summary>
    public const int PointBuyBudget = 27;

    /// <summary>Punteggio minimo e massimo ammessi dall'acquisto punti. Derivati da
    /// <see cref="PointBuyCosts"/> e non scritti a mano: i due limiti e la tabella dei costi non
    /// possono divergere, ed è proprio la divergenza il difetto che si nasconde bene (un limite
    /// digitato altrove resta indietro quando la tabella cambia).</summary>
    public static readonly int PointBuyMin = PointBuyCosts.Keys.Min();

    /// <inheritdoc cref="PointBuyMin"/>
    public static readonly int PointBuyMax = PointBuyCosts.Keys.Max();

    /// <summary>Riporta un punteggio dentro i limiti dell'acquisto punti.</summary>
    public static int ClampPointBuy(int score) => Math.Clamp(score, PointBuyMin, PointBuyMax);

    /// <summary>I sei punteggi di partenza quando si sceglie un metodo: l'array standard già
    /// assegnato nell'ordine canonico, il minimo acquistabile per l'acquisto punti, 10 (il valore
    /// neutro) per il tiro. Sta qui e non nel markup perché è la regola che decide da dove parte
    /// il giocatore, e cambia insieme ai costi dell'acquisto punti.</summary>
    public static int[] InitialScoresFor(AbilityScoreMethod method) => method switch
    {
        AbilityScoreMethod.StandardArray => (int[])StandardArrayScores.Clone(),
        AbilityScoreMethod.PointBuy => Enumerable.Repeat(PointBuyMin, 6).ToArray(),
        _ => Enumerable.Repeat(10, 6).ToArray(),
    };

    /// <summary>Converte una velocità nell'unità del PG, che è sempre in <b>metri</b>
    /// (<see cref="Character.Speed"/>).
    ///
    /// Serve perché le due unità convivono per davvero: il pacchetto SRD dichiara i **piedi**
    /// (<c>"unit": "ft"</c>) — 30 per quasi tutte le specie, 35 per il Golia — mentre le righe di
    /// campagna possono essere in metri. Copiare il numero grezzo scriverebbe «30 metri», cioè una
    /// velocità più che tripla, e da lì si propagherebbe alla scheda e alla vista Party.
    ///
    /// Conversione con il fattore usato dai manuali italiani (30 ft = 9 m, cioè ×0,3), arrotondato
    /// all'intero più vicino perché la colonna è <c>integer</c>: i 35 ft del Golia valgono 10,5 m
    /// sul manuale ufficiale e qui diventano 11. È una perdita dichiarata, non un errore: il campo
    /// non sa rappresentare i mezzi metri.</summary>
    /// <param name="value">Valore nell'unità di partenza.</param>
    /// <param name="unit">Unità dichiarata dalla voce. Il riconoscimento NON è fatto qui: passa da
    /// <see cref="PackageRowMerge.UnitaValida"/>, che è la fonte unica già usata dall'import e dalle
    /// pagine di catalogo. Interpretare l'unità una seconda volta a mano è proprio ciò che le
    /// farebbe divergere: un'unità ignota verrebbe importata in campagna come metrica e convertita
    /// qui come se fosse in piedi, cioè lo stesso dato letto in due modi opposti dalla stessa app.</param>
    public static int SpeedInMeters(int value, string? unit)
        => PackageRowMerge.UnitaValida(unit) == "m"
            ? value
            : (int)Math.Round(value * 0.3, MidpointRounding.AwayFromZero);

    /// <summary>Costo in punti-acquisto di un singolo punteggio. null se il punteggio non è
    /// ammesso da questo metodo (fuori 8-15).</summary>
    public static int? PointBuyCost(int score) => PointBuyCosts.TryGetValue(score, out var c) ? c : (int?)null;

    /// <summary>Costo totale di sei punteggi acquisto-punti. null se l'array non ha sei elementi
    /// o se anche uno solo dei punteggi è fuori 8-15.</summary>
    public static int? PointBuyTotalCost(int[]? scores)
    {
        if (scores is null || scores.Length != 6) return null;

        var total = 0;
        foreach (var s in scores)
        {
            var cost = PointBuyCost(s);
            if (cost is null) return null;
            total += cost.Value;
        }
        return total;
    }

    /// <summary>Punti rimasti dal budget (27 meno il costo totale). null se un punteggio non è
    /// ammesso (fuori 8-15); può essere negativo se il giocatore ha speso più del budget.</summary>
    public static int? PointBuyRemaining(int[]? scores)
    {
        var total = PointBuyTotalCost(scores);
        return total is null ? null : PointBuyBudget - total.Value;
    }

    /// <summary>Vero se la specie ha bonus di caratteristica non nulli: cioè è una voce 2014 già
    /// digitata (§4.7 punto 3). Le voci di pacchetto e quelle 2024 non hanno bonus, quindi qui
    /// vale sempre false quando <paramref name="race"/> è la voce di catalogo giusta o è null.</summary>
    public static bool RaceGrantsAbilityBonuses(Race? race) => RaceBonuses(race).Any(b => b != 0);

    /// <summary>Le (fino a tre) caratteristiche di un background come chiavi inglesi. Stesso
    /// formato testuale ("Forza, Saggezza, Carisma") e stesso parser tollerante di
    /// <see cref="ParseSaveProficiencies"/>: riusato, non riscritto.</summary>
    public static IReadOnlyList<string> BackgroundAbilityKeys(string? abilityScoresText)
        => ParseSaveProficiencies(abilityScoresText);

    /// <summary>Vero se il wizard deve applicare i bonus del background: solo se il background ne
    /// concede almeno uno E la specie scelta non ne ha già di suoi (§4.7 punto 3). Un background a
    /// testo libero non ha caratteristiche associate, quindi qui risulta sempre false.</summary>
    public static bool ShouldApplyBackgroundBonuses(Race? race, IReadOnlyList<string> backgroundAbilityKeys)
        => backgroundAbilityKeys.Count > 0 && !RaceGrantsAbilityBonuses(race);

    /// <summary>Le due ripartizioni ammesse dal background 2024 (§4.7 punto 1).</summary>
    public enum BackgroundAbilitySplit { TwoAndOne, OneEachOfThree }

    /// <summary>Costruisce la mappa caratteristica→bonus dalla scelta del giocatore.
    /// <paramref name="chosenAbilities"/> sono le chiavi inglesi scelte, nell'ordine di
    /// applicazione: per <see cref="BackgroundAbilitySplit.TwoAndOne"/> la prima riceve +2 e la
    /// seconda +1 (servono esattamente due chiavi distinte); per
    /// <see cref="BackgroundAbilitySplit.OneEachOfThree"/> ciascuna riceve +1 (una-tre chiavi
    /// distinte). Combinazioni non valide (conteggio sbagliato, chiavi vuote/duplicate) → mappa
    /// vuota: nessun bonus applicato, invece di un'eccezione o di un bonus scorretto.</summary>
    public static IReadOnlyDictionary<string, int> BuildBackgroundBonusMap(
        BackgroundAbilitySplit split, IReadOnlyList<string>? chosenAbilities)
    {
        var distinct = (chosenAbilities ?? Array.Empty<string>())
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .Select(a => a.Trim().ToLowerInvariant())
            .Distinct()
            .ToList();

        return split switch
        {
            BackgroundAbilitySplit.TwoAndOne when distinct.Count == 2
                => new Dictionary<string, int> { [distinct[0]] = 2, [distinct[1]] = 1 },
            BackgroundAbilitySplit.OneEachOfThree when distinct.Count is >= 1 and <= 3
                => distinct.ToDictionary(a => a, _ => 1),
            _ => new Dictionary<string, int>()
        };
    }

    /// <summary>Applica la mappa di bonus di background ai punteggi BASE (non ancora clampati),
    /// nell'ordine canonico FOR,DES,COS,INT,SAG,CAR. Tetto a 20 solo sui punteggi toccati da
    /// QUESTO bonus (§4.7 punto 2): il clamp generale 1..30 resta compito di
    /// <see cref="FinalAbilityScores"/>, applicato DOPO — 20 non sostituisce 30, lo precede.
    /// baseScores più corto/null → mancanti trattati come 10, stesso comportamento di
    /// <see cref="FinalAbilityScores"/>.</summary>
    public static int[] ApplyBackgroundBonuses(int[]? baseScores, IReadOnlyDictionary<string, int>? bonusMap)
    {
        var result = new int[6];
        for (var i = 0; i < 6; i++)
        {
            var baseVal = baseScores is not null && i < baseScores.Length ? baseScores[i] : 10;
            var bonus = bonusMap is not null && bonusMap.TryGetValue(AbilityKeyOrder[i], out var v) ? v : 0;
            result[i] = bonus > 0 ? Math.Min(baseVal + bonus, 20) : baseVal;
        }
        return result;
    }

    /// <summary>Serializza la mappa di bonus nel formato salvato in
    /// <c>characters.background_ability_choice</c> (§4.7 punto 1): "chiave:+N" separati da virgola,
    /// es. "strength:+2,wisdom:+1". Serve a ricostruire la scelta in modifica/level-up, perché i
    /// punteggi sono salvati già sommati.</summary>
    public static string FormatBackgroundAbilityChoice(IReadOnlyDictionary<string, int>? bonusMap)
        => bonusMap is null
            ? string.Empty
            : string.Join(",", bonusMap.Where(kv => kv.Value != 0).Select(kv => $"{kv.Key}:+{kv.Value}"));

    /// <summary>Controparte di <see cref="FormatBackgroundAbilityChoice"/>: token non riconosciuti
    /// (chiave ignota, valore non numerico) vengono scartati invece di far fallire il parsing
    /// dell'intera stringa. Vuoto/null → mappa vuota.</summary>
    public static IReadOnlyDictionary<string, int> ParseBackgroundAbilityChoice(string? text)
    {
        var result = new Dictionary<string, int>();
        if (string.IsNullOrWhiteSpace(text)) return result;

        foreach (var token in text.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = token.Split(':');
            if (parts.Length != 2) continue;

            var key = parts[0].Trim().ToLowerInvariant();
            if (!AbilityKeyOrder.Contains(key)) continue;

            var valueText = parts[1].Trim().TrimStart('+');
            if (int.TryParse(valueText, out var value)) result[key] = value;
        }
        return result;
    }

    /// <summary>CA base senza armatura indossata: 10 + modificatore Destrezza (regola 5e).</summary>
    public static int BaseArmorClass(int dexterityScore) => 10 + CharacterCalculations.GetModifier(dexterityScore);

    // ----- Validazione per passo (passo N di 6: Specie, Classe, Background, Caratteristiche, Dettagli, Riepilogo) -----

    /// <summary>Passo 1 (Specie): serve un nome, dal catalogo o a testo libero.</summary>
    public static string? ValidateSpeciesStep(string? raceName)
        => string.IsNullOrWhiteSpace(raceName)
            ? "Scegli una specie dal catalogo, oppure scrivine una a testo libero."
            : null;

    /// <summary>Passo 2 (Classe): serve un nome, dal catalogo o a testo libero.</summary>
    public static string? ValidateClassStep(string? className)
        => string.IsNullOrWhiteSpace(className)
            ? "Scegli una classe dal catalogo, oppure scrivine una a testo libero."
            : null;

    /// <summary>Passo 3 (Background): sempre valido. Il background è facoltativo (§4.7): un PG
    /// può nascere senza, e un background a testo libero è ammesso quanto uno di catalogo.</summary>
    public static string? ValidateBackgroundStep(string? backgroundName) => null;

    /// <summary>Passo 4 (Caratteristiche): sei punteggi in 1..30, più il vincolo del metodo scelto
    /// (l'array standard richiede una permutazione esatta dei suoi sei valori; l'acquisto punti
    /// richiede punteggi 8-15 entro il budget di 27; il tiro manuale non ha altri vincoli).</summary>
    public static string? ValidateAbilitiesStep(AbilityScoreMethod method, int[]? baseScores)
    {
        if (baseScores is null || baseScores.Length != 6) return "Servono sei punteggi di caratteristica.";
        foreach (var s in baseScores)
            if (s < 1 || s > 30) return "Ogni punteggio deve essere tra 1 e 30.";

        if (method == AbilityScoreMethod.StandardArray && !IsValidStandardArrayAssignment(baseScores))
            return $"Con l'array standard assegna esattamente i valori {string.Join(", ", StandardArrayScores)}, uno per caratteristica.";

        if (method == AbilityScoreMethod.PointBuy)
        {
            var remaining = PointBuyRemaining(baseScores);
            if (remaining is null) return "Con l'acquisto punti ogni punteggio deve essere tra 8 e 15.";
            if (remaining < 0) return $"Hai speso troppi punti: {PointBuyBudget - remaining.Value} su {PointBuyBudget} disponibili.";
        }

        return null;
    }

    /// <summary>Passo 5 (Dettagli): nome obbligatorio, PF massimi e CA non negativi.</summary>
    public static string? ValidateDetailsStep(Character? draft)
    {
        if (draft is null || string.IsNullOrWhiteSpace(draft.Name)) return "Il nome è obbligatorio.";
        if (draft.MaxHitPoints < 1) return "I punti ferita massimi devono essere almeno 1.";
        if (draft.ArmorClass < 0) return "La Classe Armatura non può essere negativa.";
        return null;
    }

    /// <summary>Passo 6 (Riepilogo): controllo finale prima del salvataggio — riesegue le
    /// validazioni di TUTTI i passi che toccano il <see cref="Character"/> salvato, comprese le
    /// caratteristiche.
    ///
    /// Le caratteristiche si rivalidano qui e non si danno per buone dal passo 4: i pallini di
    /// passo permettono di tornare indietro e poi risalire senza passare da "Avanti", che è l'unico
    /// punto in cui <see cref="ValidateAbilitiesStep"/> veniva invocata. Per quella strada si
    /// arrivava a salvare sei punteggi da 15 con l'array standard, o 54 punti spesi su 27
    /// disponibili con l'acquisto punti.</summary>
    public static string? ValidateSummaryStep(Character? draft, AbilityScoreMethod method, int[]? baseScores)
    {
        if (draft is null) return "Dati del personaggio mancanti.";
        return ValidateSpeciesStep(draft.Race)
            ?? ValidateClassStep(draft.Class)
            ?? ValidateAbilitiesStep(method, baseScores)
            ?? ValidateDetailsStep(draft);
    }

    // =====================================================================
    // Creazione guidata — passo Classe (2026-08-06): slot incantesimo e
    // caratteristica da incantatore al 1° livello, vincolo/applicazione
    // delle competenze abilità. Le firme qui sotto usano nomi italiani per
    // restare nel vocabolario di SkillCatalog/SkillChoiceRules/
    // ClassProgression/LevelUpRules, che queste funzioni orchestrano senza
    // duplicarne la logica — v.
    // docs/superpowers/specs/2026-08-06-creazione-guidata-design.md.
    // =====================================================================

    /// <summary>Scrive gli slot incantesimo di 1° livello sul draft, dalla STESSA fonte che userà
    /// il motore di progressione (<see cref="ClassProgression.SlotFinoAl"/>), non da un calcolo
    /// proprio del wizard. Azzera sempre prima di applicare — stesso stile idempotente di
    /// <c>ApplyClassSaveProficiencies</c> nel wizard — così un ripensamento (si torna a una classe
    /// senza slot, o se ne cambia una per un'altra) non lascia sul draft gli slot della classe
    /// abbandonata. Una tabella assente o senza slot (<paramref name="classTable"/> null/vuoto, o
    /// senza righe che li dichiarano) produce tutti zero: l'osservabile "nessuno slot" è lo stesso
    /// sia che si scriva zero sia che non si scriva nulla, quindi non serve distinguere i due casi.
    /// Una lista con più di nove valori (dato malformato) ignora gli eccedenti, non li scrive fuori
    /// dai nove cerchi che <c>Character</c> conosce.</summary>
    public static void ApplySpellSlotsLevel1(Character draft, string? classTable)
    {
        var slots = ClassProgression.SlotFinoAl(classTable, 1);

        draft.SpellSlots1Max = slots.ElementAtOrDefault(0);
        draft.SpellSlots2Max = slots.ElementAtOrDefault(1);
        draft.SpellSlots3Max = slots.ElementAtOrDefault(2);
        draft.SpellSlots4Max = slots.ElementAtOrDefault(3);
        draft.SpellSlots5Max = slots.ElementAtOrDefault(4);
        draft.SpellSlots6Max = slots.ElementAtOrDefault(5);
        draft.SpellSlots7Max = slots.ElementAtOrDefault(6);
        draft.SpellSlots8Max = slots.ElementAtOrDefault(7);
        draft.SpellSlots9Max = slots.ElementAtOrDefault(8);
    }

    /// <summary>Scrive la caratteristica da incantatore — chiave inglese minuscola, MAI il nome
    /// italiano: è la trappola già nota (<c>CharacterCalculations.ParseSpellcastingAbility</c> fa
    /// uno switch esatto sulle tre stringhe inglesi e torna null su qualunque altra cosa, lasciando
    /// CD e bonus d'attacco vuoti con build e test verdi) — dalla STESSA fonte del planner,
    /// <see cref="LevelUpRules.CaratteristicaIncantatore"/>: non è derivabile dalla caratteristica
    /// principale (il Ranger dichiara Destrezza ma lancia con Saggezza).
    ///
    /// Scrive solo se il campo è ancora vuoto: se vale già qualcosa — impostato a mano, o da un
    /// giro precedente di questa stessa funzione — quella vince. Chi vuole ricalcolarla (tipicamente
    /// al cambio di classe) deve azzerare il campo PRIMA di chiamare, esplicitamente: da qui dentro
    /// non c'è modo di distinguere "l'ha scritta l'utente" da "l'ha scritta un giro precedente".</summary>
    public static void ApplySpellcastingAbility(Character draft, string? className)
    {
        if (!string.IsNullOrWhiteSpace(draft.SpellcastingAbility)) return;
        draft.SpellcastingAbility = LevelUpRules.CaratteristicaIncantatore(className);
    }

    /// <summary>Il vincolo di scelta abilità della classe, dalle due sorgenti possibili — stessa
    /// precedenza già in uso nel wizard per dado vita e caratteristica principale: la riga di
    /// campagna vince quando esiste (anche se il suo campo testuale è vuoto o non riconosciuto — è
    /// la classe di QUESTO tavolo, e non si ripiega sul manuale), il pacchetto è il ripiego solo
    /// quando non c'è affatto una riga di campagna per questa classe. Non un calcolo nuovo: passa
    /// da <see cref="SkillChoiceRules.DaTesto"/>/<see cref="SkillChoiceRules.DaPacchetto"/>, che
    /// applicano già il degrado totale (D10 dello spec) quando il vincolo non è rappresentabile.</summary>
    public static VincoloAbilita? RisolviVincoloAbilita(CharacterClass? classeDiCampagna, PackageClass? classeDiPacchetto)
        => classeDiCampagna is not null
            ? SkillChoiceRules.DaTesto(classeDiCampagna.SkillChoices)
            : SkillChoiceRules.DaPacchetto(classeDiPacchetto?.SkillChoices);

    /// <summary>Le competenze concesse dal background, dalle due sorgenti possibili, con la stessa
    /// precedenza di <see cref="RisolviVincoloAbilita"/>. Sono concesse, non scelte (D12 dello
    /// spec): il chiamante le applica sul draft con <see cref="ApplicaCompetenze"/>, non le mostra
    /// come un vincolo di scelta.</summary>
    public static IReadOnlyList<SkillType> RisolviCompetenzeConcesse(Background? backgroundDiCampagna, PackageBackground? backgroundDiPacchetto)
        => backgroundDiCampagna is not null
            ? SkillCatalog.DaElenco(backgroundDiCampagna.SkillProficiencies)
            : SkillCatalog.DaElencoDiNomi(backgroundDiPacchetto?.SkillProficiencies);

    /// <summary>Riscrive le 18 competenze abilità di <paramref name="draft"/> a partire dalle due
    /// fonti indipendenti — le scelte dal vincolo di classe e le concessioni del background —
    /// invece di alterarle in base al valore precedente: i bool di <c>Character</c> non ricordano
    /// da dove viene ciascuna spunta, quindi ricalcolarle daccapo da entrambi gli insiemi a ogni
    /// cambiamento è l'unico modo che non perde né inventa competenze quando classe o background
    /// cambiano (§E dello spec — il punto più facile da sbagliare di tutto il passaggio). Una
    /// competenza fuori da entrambi gli insiemi risulta non competente: nel wizard non esiste
    /// un'altra via per marcarla, quindi non c'è nulla da preservare.</summary>
    public static void ApplicaCompetenze(
        Character draft,
        IReadOnlyCollection<SkillType> scelte,
        IReadOnlyCollection<SkillType> concesse)
    {
        foreach (var abilita in SkillCatalog.Tutte)
        {
            var giustificata = scelte.Contains(abilita) || concesse.Contains(abilita);
            SkillCatalog.ImpostaCompetenza(draft, abilita, giustificata);
        }
    }

    /// <summary>Passo Classe: il conteggio esatto blocca "Avanti" SOLO quando esiste un vincolo —
    /// il picker libero (vincolo assente) non blocca mai, qualunque sia <paramref name="esito"/>
    /// (D11 dello spec).</summary>
    public static string? ValidateClassSkillChoices(VincoloAbilita? vincolo, EsitoScelteAbilita esito)
        => vincolo is not null && !esito.Completa
            ? (esito.Messaggio ?? "Completa la scelta delle competenze di classe prima di continuare.")
            : null;

    // =====================================================================
    // Creazione guidata — passo Progressione (2026-08-06): entrare in una campagna già avviata
    // (creare al 5° livello, non solo al 1°) collegando CreationChain.Deriva al wizard. Il fold vero
    // vive in CreationChain (fuori perimetro di questa fetta); qui stanno solo le funzioni pure che
    // decidono COSA MOSTRARE di una TappaCreazione già calcolata — mai un secondo calcolo dei suoi
    // numeri. V. docs/superpowers/specs/2026-08-06-creazione-guidata-design.md.
    // =====================================================================

    /// <summary>I tre stati di una riga del passo Progressione. Non due: "nessuna decisione"
    /// (<see cref="Confermata"/>) e "una decisione che non blocca" (<see cref="Facoltativa"/>) sono
    /// esiti diversi — v. il commento XML su <see cref="TappaCreazione.HaScelteFacoltative"/> in
    /// <c>CreationChain.cs</c> — e trattarli allo stesso modo farebbe sparire in silenzio l'invito ad
    /// annotare le invocazioni occulte di un Warlock o la sottoclasse di una classe senza
    /// catalogo.</summary>
    public enum StatoTappa { Confermata, RichiedeScelte, Facoltativa }

    /// <summary>Lo stato di una tappa, per l'icona e per decidere se la riga mostra "[media ▾]" o
    /// il pulsante "Scegli". <see cref="TappaCreazione.RichiedeScelte"/> vince su
    /// <see cref="TappaCreazione.HaScelteFacoltative"/>: una tappa non è mai entrambe le cose per
    /// come le costruisce <c>CreationChain.Deriva</c> (una <see cref="DecisioneLibera"/> non fa mai
    /// salire <c>RichiedeScelte</c>), ma l'ordine qui non dipende da quella garanzia.</summary>
    public static StatoTappa StatoDiTappa(TappaCreazione tappa) => tappa.RichiedeScelte
        ? StatoTappa.RichiedeScelte
        : tappa.HaScelteFacoltative ? StatoTappa.Facoltativa : StatoTappa.Confermata;

    /// <summary>Il testo sintetico di una tappa auto-confermata (nessuna decisione, né bloccante né
    /// facoltativa): sempre i PF guadagnati, più UNA fra due informazioni — il bonus di competenza
    /// se è appena salito, altrimenti il dado vita usato per il calcolo — mai entrambe, per restare
    /// corta quanto una riga di elenco. Legge solo campi già calcolati da
    /// <see cref="LevelUpPlan"/> (le due <see cref="Proposta{T}"/>): nessun ricalcolo qui, che
    /// duplicherebbe la stessa aritmetica di <c>LevelUpPlanner.Pianifica</c> con la possibilità di
    /// divergerne.</summary>
    public static string RiepilogoAutomatico(TappaCreazione tappa)
    {
        var piano = tappa.Piano;
        var deltaPf = piano.PuntiFeritaMax.Proposto - piano.PuntiFeritaMax.Attuale;
        var seconda = piano.BonusCompetenza.Proposto != piano.BonusCompetenza.Attuale
            ? $"competenza +{piano.BonusCompetenza.Proposto - piano.BonusCompetenza.Attuale}"
            : $"dado vita {piano.DadoVita}";
        return $"+{deltaPf} PF, {seconda}";
    }

    /// <summary>Etichetta breve di una decisione ancora aperta, per la riga sintetica del passo
    /// Progressione: non il titolo pieno del catalogo (un privilegio può chiamarsi "Sottoclasse del
    /// Barbaro" o portare il nome intero del talento), ma un promemoria di COSA manca, corto quanto
    /// una riga di elenco.
    ///
    /// MINORE 8 del gate del 2026-08-06: nessun ripiego generico "Scegli un talento" per
    /// <see cref="DecisioneFraOpzioni"/> — <c>LevelUpPlanner</c> assegna la stessa chiave
    /// <c>L{n}:talento</c> anche a stile di combattimento e dono epico, e un Ranger o un Paladino al
    /// 2° leggerebbero "Scegli un talento" mentre il pannello sotto dice "Stile di combattimento". Si
    /// ripiega su <c>decisione.Titolo</c> — lo stesso ramo già in uso per le decisioni libere — che
    /// per queste è il nome del privilegio, non un'etichetta inventata qui.</summary>
    private static string EtichettaBreveDecisione(Decisione decisione) => decisione switch
    {
        DecisioneFraOpzioni f when f.Chiave.EndsWith(":sottoclasse", StringComparison.Ordinal) => "Scegli la sottoclasse",
        DecisionePunteggi => "Ripartisci i punteggi",
        DecisioneLibera l => l.Titolo,
        _ => decisione.Titolo
    };

    /// <summary>Il testo sintetico di una tappa con almeno una decisione ancora aperta (bloccante o
    /// facoltativa): la RISPOSTA già data (<see cref="RiepilogoRisposta"/>) quando esiste, altrimenti
    /// l'etichetta breve della decisione — per una tappa con talento e ripartizione punteggi
    /// entrambi mostra i due promemoria/risposte, non uno solo.
    ///
    /// MINORE 3 del secondo giro del gate del 2026-08-06: senza <paramref name="risposte"/>, una
    /// decisione GIÀ risposta (un talento scelto al 4° livello, che apre la decisione FIGLIA sulla
    /// ripartizione dei punteggi e lascia la tappa "richiede scelte" finché quella non è completa)
    /// veniva riproposta come "da fare" insieme a quella davvero ancora aperta — lo stesso difetto
    /// che <see cref="RiepilogoRisposta"/>/<c>RiepilogoConfermata</c> risolvono per le tappe
    /// confermate, in uno stato che quella correzione non copriva. <paramref name="risposte"/>
    /// assente, o senza voce per una decisione, equivale a "nessuna risposta ancora": stesso
    /// comportamento di prima della correzione — le chiamate esistenti restano valide.</summary>
    public static string RiepilogoScelte(TappaCreazione tappa, IReadOnlyDictionary<string, Risposta>? risposte = null)
        => string.Join(" · ", tappa.Piano.Decisioni
            .Select(d =>
            {
                var risposta = risposte is not null && risposte.TryGetValue(d.Chiave, out var r) ? r : null;
                return RiepilogoRisposta(d, risposta) ?? EtichettaBreveDecisione(d);
            })
            .Distinct());

    /// <summary>Quante tappe hanno ancora una decisione che blocca l'avanzamento — per il footer del
    /// passo Progressione ("N scelte da fare"). Le facoltative non contano: non bloccano, per
    /// contratto.</summary>
    public static int ScelteRestanti(IReadOnlyList<TappaCreazione> tappe) => tappe.Count(t => t.RichiedeScelte);

    /// <summary>Passo Progressione: blocca "Avanti" SOLO quando la catena governa davvero la
    /// progressione (<paramref name="catenaAttiva"/> — livello richiesto oltre il 1° E la classe ha
    /// una tabella) e il fold non è ancora completo. Il ripiego senza tabella (§E dello spec) non
    /// blocca mai, qualunque sia l'esito passato: il tavolo homebrew non deve restare bloccato.</summary>
    public static string? ValidateProgressionStep(bool catenaAttiva, bool completa, string? motivo)
        => catenaAttiva && !completa
            ? (motivo ?? "Completa le scelte di progressione prima di continuare.")
            : null;

    /// <summary>Soglia di troncamento delle descrizioni lunghe (sottoclassi, talenti — sfiorano i
    /// 3.000 caratteri) mostrate in accordion nel passo Progressione: sopra questa lunghezza compare
    /// il pulsante "Espandi".</summary>
    public const int DescrizioneTroncamentoSoglia = 220;

    /// <summary>Troncamento con ellissi, sicuro su testo vuoto/null. Stessa idea già in uso in
    /// <c>Shared/CharacterTabs/LevelUpDialog.razor</c> (metodo privato <c>Troncata</c>), qui
    /// promossa perché la consuma anche il passo Progressione del wizard, non solo il dialogo di
    /// level-up.</summary>
    public static string TroncaDescrizione(string? testo, int soglia = DescrizioneTroncamentoSoglia)
        => string.IsNullOrEmpty(testo) || testo.Length <= soglia ? testo ?? string.Empty : testo[..soglia].TrimEnd() + "…";

    /// <summary>Le facce del dado dalla sua media (media = facce/2 + 1, quindi facce = (media-1)*2):
    /// evita di riparsare il dado vita con una logica propria, che duplicherebbe
    /// <see cref="LevelUpPlanner"/> con un fallback diverso in caso di stringa non riconosciuta.
    ///
    /// Clampata a un minimo di 2: il dado vita è testo libero in <c>Pages/Classes.razor</c>, e un
    /// refuso tipo "d1" (invece di "d10") dà media 1 e quindi facce 0 — chi chiama fa poi
    /// <c>Math.Clamp(valore, 1, facceMax)</c>, che con max 0 e min 1 solleva
    /// <see cref="ArgumentException"/> durante il render (dialogo/passo inutilizzabile finché non si
    /// ricarica). SERIO 5 del gate del 2026-08-06: promossa da <c>Shared/CharacterTabs/LevelUpDialog.razor</c>
    /// (dove viveva privata, con lo stesso commento) perché la stessa formula era duplicata inline
    /// nel markup del passo Progressione del wizard — un solo posto da correggere per entrambi.</summary>
    public static int FacceMax(int mediaDado) => Math.Max(2, (mediaDado - 1) * 2);

    // =====================================================================
    // Pipeline di salvataggio (SERIO 4 del gate del 2026-08-06): AssemblaBaseline e DerivaEsito
    // erano funzioni pure del componente CharacterWizard, dove nessun test le raggiungeva — contro
    // il pattern chiave del progetto (logica di dominio in helper puri static, non nei .razor). Qui
    // sotto gli stessi calcoli, con lo stato del componente sostituito da parametri espliciti: il
    // componente ne resta un chiamante sottile (v. i wrapper omonimi in CharacterWizard.razor).
    // =====================================================================

    /// <summary>Assembla il baseline di 1° livello da cui la catena (<see cref="CreationChain.Deriva"/>)
    /// parte: clona <paramref name="draft"/>, lo riporta al 1° livello e sincronizza ciò che il
    /// wizard possiede ma non scrive MAI sul draft prima del salvataggio — i punteggi finali, la
    /// scelta caratteristiche di background, i tiri salvezza di classe. V. il principio della spec
    /// (<c>docs/superpowers/specs/2026-08-06-creazione-guidata-design.md</c>): il wizard possiede il
    /// 1°, il planner possiede il 2→N — e D3: questa sincronizzazione deve SEMPRE precedere il fold
    /// (<see cref="DerivaEsito"/>), mai seguirlo, o un incremento di caratteristica maturato nel
    /// fold (l'ASI del 4°, per esempio) verrebbe sovrascritto in silenzio da un sync tardivo.
    ///
    /// <paramref name="catenaUtile"/> decide il seed di PF/dadi vita: vero SOLO quando la catena
    /// guiderà davvero la progressione (livello richiesto oltre il 1° e la classe ha una tabella) —
    /// in quel caso il seed è la FORMULA del 1° livello (<see cref="SuggestMaxHp"/>/
    /// <see cref="BuildHitDice"/>), che il fold userà come punto di partenza. Altrimenti (livello 1,
    /// o classe senza tabella) resta quello che l'utente vede e può modificare nel passo Dettagli:
    /// forzarlo qui sarebbe un'edit fantasma, scartata in silenzio al salvataggio.
    ///
    /// SERIO 1 del gate del 2026-08-06: <see cref="SuggestMaxHp"/> e <see cref="BuildHitDice"/>
    /// tornano una sentinella (0, "") quando <paramref name="classHitDie"/> non è riconosciuto — una
    /// classe di campagna col dado vuoto, o una scritta a mano con un casing diverso da quello del
    /// manuale. Scritta senza controllo, quella sentinella blocca "Crea personaggio" senza via
    /// d'uscita (<see cref="ValidateSummaryStep"/> rifiuta PF sotto 1, e il campo che li mostrerebbe
    /// è disabilitato).
    ///
    /// MINORE 1 del secondo giro del gate del 2026-08-06: il ripiego per i PF NON è più
    /// <c>draft.MaxHitPoints</c> — quel campo può contenere i punti ferita di un livello PIÙ ALTO,
    /// digitati mentre la classe era ancora a testo libero (campo editabile) o senza tabella, e
    /// sopravvivere invariato a un cambio di classe che rende la catena "utile": il vecchio ripiego
    /// li avrebbe presi per buoni come seed di 1° livello, e il fold ci avrebbe aggiunto sopra la
    /// crescita di altri livelli. Il ripiego è invece <c>SuggestMaxHp("d8", modCos, 1)</c> — la
    /// STESSA stima generica che <c>LevelUpPlanner.FacceDado</c> usa per ogni livello successivo
    /// quando il dado non si riconosce (il suo avviso: "Dado vita non riconosciuto: uso d8 come
    /// stima."): seed e fold parlano la stessa lingua, non due. <paramref name="pfBaseline1LivelloManuale"/>,
    /// se presente, vince su questa stima generica — è il valore che l'utente ha scritto nel campo
    /// PF del passo Dettagli, riabilitato apposta quando il dado non si riconosce (§E della spec:
    /// l'utente deve poter correggere una stima inventata, non restare bloccato su un campo
    /// disabilitato). Si applica SOLO quando la sentinella scatta davvero: se il dado torna
    /// riconoscibile, un override rimasto da uno stato precedente non vince silenziosamente su un
    /// calcolo vero.</summary>
    public static Character AssemblaBaseline(
        Character draft,
        int[] finalScores,
        IReadOnlyDictionary<string, int>? backgroundBonusMap,
        string? savingThrowsText,
        bool catenaUtile,
        string? classHitDie,
        int? pfBaseline1LivelloManuale = null)
    {
        var baseline = CharacterClone.Clona(draft);
        baseline.Level = 1;

        baseline.Strength = finalScores[0];
        baseline.Dexterity = finalScores[1];
        baseline.Constitution = finalScores[2];
        baseline.Intelligence = finalScores[3];
        baseline.Wisdom = finalScores[4];
        baseline.Charisma = finalScores[5];

        baseline.BackgroundAbilityChoice = FormatBackgroundAbilityChoice(backgroundBonusMap);
        ApplicaTiriSalvezze(baseline, savingThrowsText);

        if (catenaUtile)
        {
            var modCos = CharacterCalculations.GetModifier(baseline.Constitution);
            var suggeritiPf = SuggestMaxHp(classHitDie, modCos, 1);
            baseline.MaxHitPoints = suggeritiPf > 0
                ? suggeritiPf
                : (pfBaseline1LivelloManuale ?? SuggestMaxHp("d8", modCos, 1));
            baseline.HitPoints = baseline.MaxHitPoints;

            var suggeritiDadi = BuildHitDice(classHitDie, 1);
            baseline.HitDiceMax = string.IsNullOrEmpty(suggeritiDadi) ? draft.HitDiceMax : suggeritiDadi;
        }
        else
        {
            baseline.MaxHitPoints = draft.MaxHitPoints;
            baseline.HitPoints = draft.MaxHitPoints;
            baseline.HitDiceMax = draft.HitDiceMax;
        }

        return baseline;
    }

    /// <summary>Il branching a tre vie della catena (v. "La catena" della spec): livello 1 (nessuna
    /// catena: il baseline stesso è il personaggio), classe senza tabella (ripiego dichiarato, §E:
    /// niente motore, il livello resta quello richiesto) o il fold vero via
    /// <see cref="CreationChain.Deriva"/>. UN'UNICA fonte per l'anteprima (passi Progressione,
    /// Dettagli, Riepilogo) e per il salvataggio: se vivessero in due punti diversi potrebbero
    /// divergere in silenzio, esattamente la classe di difetto che questa fetta deve evitare.
    ///
    /// Muta <paramref name="baseline"/> nel solo ramo "classe senza tabella" (vi scrive il livello
    /// richiesto): il chiamante non deve passargli un baseline che gli serve ancora intatto dopo la
    /// chiamata — <see cref="AssemblaBaseline"/> ne produce sempre uno fresco, adatto a essere
    /// consumato una volta sola da questa funzione.</summary>
    public static EsitoCatena DerivaEsito(
        Character baseline,
        bool mostraProgressione,
        bool classeSenzaTabella,
        int livelloRichiesto,
        string? testoProgressione,
        IReadOnlyList<PackageSubclass>? sottoclassi,
        IReadOnlyList<PackageFeat>? talenti,
        IReadOnlyDictionary<int, IReadOnlyDictionary<string, Risposta>>? rispostePerLivello,
        IReadOnlyDictionary<int, int>? tiriPerLivello,
        string? dadoVitaClasse)
    {
        if (!mostraProgressione)
            return new EsitoCatena(baseline, Array.Empty<TappaCreazione>(), Completa: true, Motivo: null, Avvisi: Array.Empty<string>());

        if (classeSenzaTabella)
        {
            baseline.Level = livelloRichiesto;
            return new EsitoCatena(baseline, Array.Empty<TappaCreazione>(), Completa: true, Motivo: null, Avvisi: Array.Empty<string>());
        }

        return CreationChain.Deriva(
            baseline, livelloRichiesto, testoProgressione, sottoclassi, talenti,
            rispostePerLivello, tiriPerLivello, dadoVitaClasse);
    }

    /// <summary>Cosa ha risposto il giocatore a una decisione già risolta, per la riga sintetica di
    /// una tappa confermata (SERIO 2 del gate del 2026-08-06): senza questo, una tappa che si
    /// auto-conferma perché la sua decisione è già stata risposta (una sottoclasse scelta al 3°, un
    /// talento al 4°) mostra solo punti ferita e dado, e non si vede più COSA si è scelto — l'unica
    /// via per rivederlo tornava a essere cambiare classe, che azzera tutto.
    ///
    /// Il nome scelto per una <see cref="DecisioneFraOpzioni"/> (uniti se più di uno), il testo
    /// annotato (troncato corto, non ai 220 caratteri delle descrizioni di catalogo) per una
    /// <see cref="DecisioneLibera"/>. Null se non c'è ancora risposta, o per una
    /// <see cref="DecisionePunteggi"/>: il suo effetto è già visibile nei punteggi finali mostrati
    /// altrove nel wizard, ripeterlo qui allungherebbe la riga senza un'informazione nuova.</summary>
    public static string? RiepilogoRisposta(Decisione decisione, Risposta? risposta) => decisione switch
    {
        DecisioneFraOpzioni when risposta is { Scelte.Count: > 0 } => string.Join(", ", risposta!.Scelte),
        DecisioneLibera when !string.IsNullOrWhiteSpace(risposta?.Testo) => TroncaDescrizione(risposta!.Testo, 40),
        _ => null
    };
}
