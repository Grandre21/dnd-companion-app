using DndCompanion.Models;

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
}
