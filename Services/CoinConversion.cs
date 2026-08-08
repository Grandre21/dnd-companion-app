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
}
