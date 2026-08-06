using DndCompanion.Models;

namespace DndCompanion.Services;

/// <summary>
/// Il nuovo stato dei campi che un riposo può cambiare, più le righe di riepilogo in italiano che
/// la UI mostra nel toast ("PF 22 → 47", "3 dadi vita recuperati"). <see cref="RestCalculations"/>
/// calcola senza toccare il personaggio: la scrittura passa sempre da
/// <see cref="RestCalculations.Applica"/>, così chi chiama può mostrare il riepilogo (o chiedere
/// conferma) prima di applicarlo.
/// </summary>
public sealed record EsitoRiposo
{
    public required int HitPoints { get; init; }
    public required int TempHitPoints { get; init; }
    public required int HitDiceSpent { get; init; }
    public required int DeathSaveSuccesses { get; init; }
    public required int DeathSaveFailures { get; init; }

    /// <summary>I nove slot "usati", dal 1° al 9° cerchio. Null se il riposo non li tocca (riposo
    /// breve): <see cref="RestCalculations.Applica"/> in tal caso li lascia come sono.</summary>
    public IReadOnlyList<int>? SpellSlotsUsed { get; init; }

    /// <summary>Lo stato completo delle risorse di classe dopo il riposo (tutte, non solo quelle
    /// ripristinate: le altre sono copiate invariate) — stesso pattern di <see cref="SpellSlotsUsed"/>,
    /// solo con una lista di lunghezza variabile invece di nove campi fissi. Null se il personaggio
    /// non ha risorse di classe: <see cref="RestCalculations.Applica"/> in tal caso non tocca
    /// <c>Character.ClassResources</c>.</summary>
    public IReadOnlyList<ClassResource>? ClassResources { get; init; }

    /// <summary>Nomi delle risorse effettivamente ripristinate (Spesi passati da &gt;0 a 0 da questo
    /// riposo) — non tutte quelle con la ricarica giusta: una già a 0 non è "cambiata" e non compare
    /// qui, per non annunciare un ripristino che non c'è stato. Null se il personaggio non ha risorse
    /// di classe.</summary>
    public IReadOnlyList<string>? RisorseRipristinate { get; init; }

    /// <summary>Righe pronte per il toast, già in italiano.</summary>
    public required IReadOnlyList<string> Riepilogo { get; init; }
}

/// <summary>
/// Riposo lungo e riposo breve (D&amp;D 5e) come sole funzioni pure: calcolano il nuovo stato senza
/// toccare il personaggio e lo riportano come <see cref="EsitoRiposo"/>. La scrittura sul PG passa
/// sempre da <see cref="Applica"/> — mai il contrario, altrimenti un eventuale annullamento in UI
/// arriverebbe a scrittura già fatta.
///
/// L'app non tira il dado (è il giocatore a tirare al tavolo): il riposo breve riceve il totale già
/// tirato, non lo calcola.
/// </summary>
public static class RestCalculations
{
    /// <summary>PF al massimo, PF temporanei azzerati, metà dei dadi vita totali recuperati
    /// (arrotondata per difetto, minimo 1: mai sotto 0 dadi spesi), slot incantesimo ripristinati,
    /// tiri salvezza contro morte azzerati. Non tocca l'ispirazione eroica: non si perde né si
    /// guadagna col riposo.</summary>
    public static EsitoRiposo RiposoLungo(Character c)
    {
        var pfPrima = c.HitPoints;
        var pfDopo = c.MaxHitPoints;

        var totaleDadi = CharacterCalculations.GetHitDiceTotal(c.HitDiceMax);
        var spesiPrima = c.HitDiceSpent;
        // "Minimo 1" vale sulla quota recuperabile: se non c'è nulla da recuperare (dadi già
        // tutti disponibili, o HitDiceMax vuoto/malformato -> totaleDadi 0) il recupero effettivo
        // resta 0 — il clamp a 0 sotto lo garantisce, e la riga di riepilogo usa il delta reale,
        // non la quota teorica, per non annunciare un recupero che non è avvenuto.
        var quotaRecupero = Math.Max(1, totaleDadi / 2);
        var spesiDopo = Math.Max(0, spesiPrima - quotaRecupero);
        var recuperati = spesiPrima - spesiDopo;

        var slotUsatiPrima = SlotUsatiCorrenti(c);
        var slotDaRipristinare = slotUsatiPrima.Any(v => v > 0);

        var (risorseNuove, risorseRipristinate) = RipristinaRisorse(c, riposoLungo: true);

        var righe = new List<string> { RigaPf(pfPrima, pfDopo, c.MaxHitPoints) };
        if (c.TempHitPoints != 0) righe.Add("PF temporanei azzerati");
        if (recuperati > 0)
        {
            righe.Add(recuperati == 1 ? "1 dado vita recuperato" : $"{recuperati} dadi vita recuperati");
        }
        if (slotDaRipristinare) righe.Add("Slot incantesimo ripristinati");
        if (c.DeathSaveSuccesses > 0 || c.DeathSaveFailures > 0) righe.Add("Tiri salvezza contro la morte azzerati");
        if (risorseRipristinate.Count > 0) righe.Add(RigaRisorseRipristinate(risorseRipristinate));

        return new EsitoRiposo
        {
            HitPoints = pfDopo,
            TempHitPoints = 0,
            HitDiceSpent = spesiDopo,
            DeathSaveSuccesses = 0,
            DeathSaveFailures = 0,
            SpellSlotsUsed = new int[9],
            ClassResources = risorseNuove,
            RisorseRipristinate = risorseRipristinate.Count > 0 ? risorseRipristinate : null,
            Riepilogo = righe,
        };
    }

    /// <summary>Cura <paramref name="totaleTirato"/> più il modificatore di Costituzione per dado
    /// speso, mai sotto 0 (un modificatore negativo non deve togliere PF), senza superare i PF
    /// massimi. <paramref name="dadiSpesi"/> è vincolato ai dadi vita ancora disponibili (mai
    /// negativo, mai sopra il totale). Non tocca slot incantesimo né tiri salvezza contro morte.</summary>
    public static EsitoRiposo RiposoBreve(Character c, int dadiSpesi, int totaleTirato)
    {
        var totaleDadi = CharacterCalculations.GetHitDiceTotal(c.HitDiceMax);
        var disponibili = Math.Max(0, totaleDadi - c.HitDiceSpent);
        var dadiEffettivi = Math.Clamp(dadiSpesi, 0, disponibili);

        // Se non si spende alcun dado (richiesta a 0, negativa, o nessun dado disponibile) non
        // c'è cura: il totale tirato passato non conta, altrimenti guarirebbe senza aver speso
        // nulla — proprio il caso "dadi vita già tutti spesi".
        var cura = 0;
        if (dadiEffettivi > 0)
        {
            var modCostituzione = CharacterCalculations.GetModifier(c.Constitution);
            // La regola 5e applica il floor a 0 dado per dado; qui si ha solo il totale già
            // tirato (l'app non tira), quindi il floor si applica all'espressione intera — con lo
            // stesso effetto che deve garantire: un modificatore negativo non riduce mai i PF.
            cura = Math.Max(0, totaleTirato + modCostituzione * dadiEffettivi);
        }

        var pfPrima = c.HitPoints;
        var pfDopo = Math.Min(c.MaxHitPoints, pfPrima + cura);
        var spesiDopo = c.HitDiceSpent + dadiEffettivi;

        var (risorseNuove, risorseRipristinate) = RipristinaRisorse(c, riposoLungo: false);

        var righe = new List<string>();
        if (dadiEffettivi == 0)
        {
            righe.Add("Nessun dado vita speso");
        }
        else
        {
            righe.Add(RigaPf(pfPrima, pfDopo, c.MaxHitPoints));
            righe.Add(dadiEffettivi == 1 ? "1 dado vita speso" : $"{dadiEffettivi} dadi vita spesi");
        }
        if (risorseRipristinate.Count > 0) righe.Add(RigaRisorseRipristinate(risorseRipristinate));

        return new EsitoRiposo
        {
            HitPoints = pfDopo,
            TempHitPoints = c.TempHitPoints,
            HitDiceSpent = spesiDopo,
            DeathSaveSuccesses = c.DeathSaveSuccesses,
            DeathSaveFailures = c.DeathSaveFailures,
            SpellSlotsUsed = null,
            ClassResources = risorseNuove,
            RisorseRipristinate = risorseRipristinate.Count > 0 ? risorseRipristinate : null,
            Riepilogo = righe,
        };
    }

    /// <summary>Media del primo dado vita di "NdM" (es. "3d12+2d8" → dado d12): floor(M/2) + 1, 0
    /// se non parsabile. Serve solo a precompilare il "totale tirato" del riposo breve: il
    /// giocatore lo corregge comunque col risultato vero del tiro al tavolo.</summary>
    public static int MediaDadoVita(string? hitDiceMax)
    {
        if (string.IsNullOrWhiteSpace(hitDiceMax)) return 0;
        try
        {
            var primoBlocco = hitDiceMax.Split('+')[0].Trim();
            var parti = primoBlocco.Split('d');
            if (parti.Length != 2) return 0;
            if (!int.TryParse(parti[1].Trim(), out var facce) || facce <= 0) return 0;
            return facce / 2 + 1;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>Scrive l'esito sul personaggio ricevuto: mutazione in place, non una copia — la
    /// scheda ne tiene un riferimento vivo. Nessuna I/O: il salvataggio resta a chi chiama.</summary>
    public static void Applica(Character c, EsitoRiposo esito)
    {
        c.HitPoints = esito.HitPoints;
        c.TempHitPoints = esito.TempHitPoints;
        c.HitDiceSpent = esito.HitDiceSpent;
        c.DeathSaveSuccesses = esito.DeathSaveSuccesses;
        c.DeathSaveFailures = esito.DeathSaveFailures;

        if (esito.SpellSlotsUsed is { Count: 9 } slot)
        {
            c.SpellSlots1Used = slot[0];
            c.SpellSlots2Used = slot[1];
            c.SpellSlots3Used = slot[2];
            c.SpellSlots4Used = slot[3];
            c.SpellSlots5Used = slot[4];
            c.SpellSlots6Used = slot[5];
            c.SpellSlots7Used = slot[6];
            c.SpellSlots8Used = slot[7];
            c.SpellSlots9Used = slot[8];
        }

        if (esito.ClassResources is { } risorse)
        {
            c.ClassResources = new List<ClassResource>(risorse);
        }
    }

    // ---------------------------------------------------------------
    // Helper privati
    // ---------------------------------------------------------------

    /// <summary>"PF già al massimo (N)" se il riposo non cambia nulla ed è per il massimo, "PF
    /// invariati (N)" se non cambia nulla per un altro motivo (es. cura netta zero nel riposo
    /// breve), altrimenti "PF X → Y".</summary>
    private static string RigaPf(int prima, int dopo, int massimo)
    {
        if (prima != dopo) return $"PF {prima} → {dopo}";
        return prima >= massimo ? $"PF già al massimo ({dopo})" : $"PF invariati ({dopo})";
    }

    private static int[] SlotUsatiCorrenti(Character c) => new[]
    {
        c.SpellSlots1Used, c.SpellSlots2Used, c.SpellSlots3Used, c.SpellSlots4Used, c.SpellSlots5Used,
        c.SpellSlots6Used, c.SpellSlots7Used, c.SpellSlots8Used, c.SpellSlots9Used,
    };

    /// <summary>Calcola il nuovo stato delle risorse di classe per un riposo (lungo o breve) senza
    /// toccare <paramref name="c"/>: per ogni risorsa la cui <c>Ricarica</c> si ripristina con questo
    /// riposo (<see cref="ClassResourceRules.SiRipristinaCon"/> — non ricodificato qui) e che aveva
    /// almeno un uso speso, restituisce una copia con <c>Spesi</c> azzerato e il nome nell'elenco dei
    /// ripristinati; le altre (ricarica diversa, "nessuna", malformata, o già a 0) sono copiate
    /// invariate e non compaiono nell'elenco. Nessuna risorsa (lista vuota o null) → (null, lista
    /// vuota), senza eccezioni.</summary>
    private static (List<ClassResource>? Nuove, List<string> NomiRipristinati) RipristinaRisorse(
        Character c, bool riposoLungo)
    {
        if (c.ClassResources is null || c.ClassResources.Count == 0)
            return (null, new List<string>());

        var nuove = new List<ClassResource>(c.ClassResources.Count);
        var nomiRipristinati = new List<string>();

        foreach (var risorsa in c.ClassResources)
        {
            if (risorsa is null) continue;

            if (risorsa.Spesi > 0 && ClassResourceRules.SiRipristinaCon(risorsa.Ricarica, riposoLungo))
            {
                nomiRipristinati.Add(risorsa.Nome);
                nuove.Add(new ClassResource
                {
                    Nome = risorsa.Nome, Max = risorsa.Max, Spesi = 0, Ricarica = risorsa.Ricarica,
                });
            }
            else
            {
                nuove.Add(new ClassResource
                {
                    Nome = risorsa.Nome, Max = risorsa.Max, Spesi = risorsa.Spesi, Ricarica = risorsa.Ricarica,
                });
            }
        }

        return (nuove, nomiRipristinati);
    }

    /// <summary>"Ira ripristinata" per una sola risorsa, "Ira e Secondo fiato ripristinate" per due o
    /// più (elenco con virgole e "e" finale, sempre in forma plurale) — <paramref name="nomi"/> non
    /// deve essere vuota: il chiamante lo garantisce col controllo <c>Count &gt; 0</c>.</summary>
    private static string RigaRisorseRipristinate(IReadOnlyList<string> nomi)
    {
        if (nomi.Count == 1) return $"{nomi[0]} ripristinata";

        var elenco = string.Join(", ", nomi.Take(nomi.Count - 1)) + " e " + nomi[^1];
        return $"{elenco} ripristinate";
    }
}
