using DndCompanion.Models;

namespace DndCompanion.Services;

/// <summary>
/// Il nuovo assetto delle cinque monete dopo una compattazione, con l'indicazione se cambia
/// qualcosa rispetto a quello attuale. <see cref="CoinConversion.Compatta"/> calcola senza toccare
/// il personaggio: la scrittura passa sempre da <see cref="CoinConversion.Applica"/>.
/// </summary>
public sealed record EsitoCompattazione
{
    public required int PlatinumPieces { get; init; }
    public required int GoldPieces { get; init; }
    public required int ElectrumPieces { get; init; }
    public required int SilverPieces { get; init; }
    public required int CopperPieces { get; init; }

    /// <summary>False se la compattazione lascia il gruzzolo identico: la UI non offre un pulsante che non fa nulla.</summary>
    public required bool Cambia { get; init; }
}

/// <summary>
/// Equivalenza in oro e compattazione del gruzzolo (D&amp;D 5e) come sole funzioni pure: calcolano
/// senza toccare il personaggio e riportano l'esito. La scrittura sul PG passa sempre da
/// <see cref="Applica"/> — mai il contrario, altrimenti un eventuale annullamento in UI arriverebbe
/// a scrittura già fatta.
/// </summary>
public static class CoinConversion
{
    public const long ValoreRame = 1;
    public const long ValoreArgento = 10;
    public const long ValoreElectrum = 50;
    public const long ValoreOro = 100;
    public const long ValorePlatino = 1000;

    /// <summary>Somma in rame delle cinque valute, in long: PlatinumPieces è un int, e
    /// int.MaxValue * 1000 andrebbe in overflow silenzioso in un int. Le valute negative (il DB non
    /// ha ancora vincoli CHECK) contano come 0, così un dato corrotto non produce un totale
    /// assurdo.</summary>
    public static long TotaleInRame(int platino, int oro, int electrum, int argento, int rame) =>
        Math.Max(0, (long)platino) * ValorePlatino +
        Math.Max(0, (long)oro) * ValoreOro +
        Math.Max(0, (long)electrum) * ValoreElectrum +
        Math.Max(0, (long)argento) * ValoreArgento +
        Math.Max(0, (long)rame) * ValoreRame;

    public static long TotaleInRame(Character c) =>
        TotaleInRame(c.PlatinumPieces, c.GoldPieces, c.ElectrumPieces, c.SilverPieces, c.CopperPieces);

    /// <summary>Equivalente in monete d'oro. Decimal (non double): è denaro, e la divisione per 100
    /// è esatta in base 10.</summary>
    public static decimal TotaleInOro(Character c) => TotaleInRame(c) / 100m;

    /// <summary>Due decimali, separatore virgola (non punto), senza zeri finali inutili, senza
    /// l'unità di misura (l'etichetta "mo" la mette il markup). La virgola è una scelta deliberata:
    /// è testo italiano rivolto al giocatore, e InvariantGlobalization impedisce di ottenerla con una
    /// cultura (CultureInfo it-IT non dà la virgola come atteso sotto trimming), quindi si formatta
    /// invariant (che dà il punto) e si sostituisce. Il peso dell'inventario (FormatWeight in
    /// CharacterItemsTab.razor) resta invece col punto: è un'incoerenza nota e accettata, non un
    /// pattern riusato.</summary>
    public static string FormattaTotaleInOro(long totaleInRame) =>
        (totaleInRame / 100m).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',');

    public static string FormattaTotaleInOro(Character c) => FormattaTotaleInOro(TotaleInRame(c));

    /// <summary>Riordina il gruzzolo verso i tagli alti senza cambiarne il valore. Si compattano
    /// solo rame, argento e oro: platino ed electrum non vengono mai creati dalla compattazione, e
    /// le quantità già possedute di quelle due restano invariate. In 5e l'electrum è una moneta
    /// desueta e il platino è ingombrante al tavolo — un giocatore che compatta vuole meno spiccioli,
    /// non vedersi trasformare il tesoro in tagli che poi nessuno accetta al tavolo.</summary>
    public static EsitoCompattazione Compatta(int platino, int oro, int electrum, int argento, int rame)
    {
        var platinoClampato = Math.Max(0, platino);
        var electrumClampato = Math.Max(0, electrum);

        var resto = Math.Max(0, (long)oro) * ValoreOro +
                    Math.Max(0, (long)argento) * ValoreArgento +
                    Math.Max(0, (long)rame) * ValoreRame;

        // "oroCompatto" nasce da un long ma la proprietà è int: il travaso è sicuro finché il
        // gruzzolo è plausibile, ma da dati corrotti (es. oro vicino a int.MaxValue) potrebbe
        // eccedere int.MaxValue. Caso patologico -> nessuna compattazione, nessuna eccezione, stessa
        // tolleranza al malformato di ClassResourceRules (v. RestCalculations.cs).
        if (resto / ValoreOro > int.MaxValue)
        {
            return new EsitoCompattazione
            {
                PlatinumPieces = platinoClampato,
                GoldPieces = Math.Max(0, oro),
                ElectrumPieces = electrumClampato,
                SilverPieces = Math.Max(0, argento),
                CopperPieces = Math.Max(0, rame),
                Cambia = false,
            };
        }

        var oroCompatto = (int)(resto / ValoreOro);
        resto %= ValoreOro;
        var argentoCompatto = (int)(resto / ValoreArgento);
        resto %= ValoreArgento;
        var rameCompatto = (int)resto;

        var cambia = platinoClampato != platino
            || oroCompatto != oro
            || electrumClampato != electrum
            || argentoCompatto != argento
            || rameCompatto != rame;

        return new EsitoCompattazione
        {
            PlatinumPieces = platinoClampato,
            GoldPieces = oroCompatto,
            ElectrumPieces = electrumClampato,
            SilverPieces = argentoCompatto,
            CopperPieces = rameCompatto,
            Cambia = cambia,
        };
    }

    public static EsitoCompattazione Compatta(Character c) =>
        Compatta(c.PlatinumPieces, c.GoldPieces, c.ElectrumPieces, c.SilverPieces, c.CopperPieces);

    /// <summary>Scrive l'esito sul personaggio ricevuto: mutazione in place, non una copia — la
    /// scheda ne tiene un riferimento vivo. Nessuna I/O: il salvataggio resta a chi chiama.</summary>
    public static void Applica(Character c, EsitoCompattazione esito)
    {
        c.PlatinumPieces = esito.PlatinumPieces;
        c.GoldPieces = esito.GoldPieces;
        c.ElectrumPieces = esito.ElectrumPieces;
        c.SilverPieces = esito.SilverPieces;
        c.CopperPieces = esito.CopperPieces;
    }
}
