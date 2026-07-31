using System.Globalization;
using System.Text;
using DndCompanion.Models;
using DndCompanion.Models.Packages;

namespace DndCompanion.Services;

/// <summary>Un livello della tabella di classe: i privilegi che si sbloccano e — per gli
/// incantatori — gli slot disponibili, dal 1° al 9° cerchio.</summary>
public sealed record ClassLevelRow(int Livello, IReadOnlyList<string> Privilegi, IReadOnlyList<int> Slot);

/// <summary>Progressione di classe (privilegi per livello) dentro il campo testuale
/// <c>CharacterClass.Features</c>. Logica pura.
///
/// Il formato di scambio porta 20 livelli per classe in <c>PackageClass.Levels</c>, ma la tabella
/// <c>classes</c> non ha una colonna per livello: l'unico posto dove quei dati stanno senza una
/// migrazione è <c>Features</c>, che è testo libero. Questa classe definisce la sola forma che
/// consente di riconoscerli e rileggerli, invece di appiattirli in prosa:
///
/// <code>
/// L1 — Ira, Difesa senza armatura
/// L3 — Sottoclasse del Chierico · Slot 4/2
/// </code>
///
/// Resta leggibile a occhio nella pagina Classi — chi non sa nulla di questo formato vede un
/// elenco sensato — e resta interpretabile dalla scheda del personaggio, che mostra i soli
/// privilegi già raggiunti.</summary>
public static class ClassProgression
{
    private const string SeparatorePrivilegi = ", ";
    private const string PrefissoSlot = " · Slot ";

    /// <summary>Trattini accettati in lettura. La scrittura usa sempre l'em dash; un testo passato
    /// da un editor che "corregge" la punteggiatura non deve diventare illeggibile.</summary>
    private static readonly char[] Trattini = { '—', '–', '-' };

    /// <summary>Rende i livelli nel formato testuale. I livelli senza privilegi né slot vengono
    /// omessi: una riga vuota non aggiunge nulla e allunga soltanto la tabella.</summary>
    public static string Serializza(IEnumerable<PackageClassLevel>? livelli)
    {
        if (livelli is null) return string.Empty;

        var sb = new StringBuilder();
        // Le voci null si scartano: il parser le lascia passare di proposito (`NormalizeLists`
        // normalizza le liste, non i loro elementi) e la validazione controlla id e nome delle
        // classi, non dei livelli. Un `"levels": [null]` in un file di terze parti farebbe altrimenti
        // fallire l'intero import con una NullReferenceException, dove prima passava senza storie.
        foreach (var lv in livelli.Where(l => l is not null).OrderBy(l => l.Level))
        {
            var privilegi = (lv.Features ?? new List<string>())
                .Select(f => f?.Trim() ?? string.Empty)
                .Where(f => f.Length > 0)
                .ToList();
            var slot = TagliaSlot(lv.SpellSlots);

            if (privilegi.Count == 0 && slot.Count == 0) continue;

            if (sb.Length > 0) sb.Append('\n');
            sb.Append('L').Append(lv.Level.ToString(CultureInfo.InvariantCulture)).Append(" — ");
            sb.Append(string.Join(SeparatorePrivilegi, privilegi));
            if (slot.Count > 0)
            {
                sb.Append(PrefissoSlot);
                sb.Append(string.Join("/", slot.Select(s => s.ToString(CultureInfo.InvariantCulture))));
            }
        }
        return sb.ToString();
    }

    /// <summary>Rilegge il formato. Le righe che non lo rispettano vengono ignorate: il campo può
    /// contenere testo scritto a mano, e in quel caso il risultato è una lista vuota — non un
    /// errore. Chi chiama deve trattare la lista vuota come "questa classe non ha una tabella" e
    /// ripiegare sul testo grezzo.</summary>
    public static IReadOnlyList<ClassLevelRow> Leggi(string? testo)
    {
        if (string.IsNullOrWhiteSpace(testo)) return Array.Empty<ClassLevelRow>();

        var righe = new List<ClassLevelRow>();
        foreach (var raw in testo.Split('\n'))
        {
            var riga = raw.Trim().TrimEnd('\r');
            if (riga.Length < 2 || (riga[0] != 'L' && riga[0] != 'l')) continue;

            var iTrattino = riga.IndexOfAny(Trattini);
            if (iTrattino < 2) continue;

            var numero = riga[1..iTrattino].Trim();
            if (!int.TryParse(numero, NumberStyles.None, CultureInfo.InvariantCulture, out var livello)) continue;
            if (livello < 1 || livello > 40) continue;

            var resto = riga[(iTrattino + 1)..].Trim();

            var slot = new List<int>();
            var iSlot = resto.IndexOf(PrefissoSlot.Trim(), StringComparison.Ordinal);
            if (iSlot >= 0)
            {
                var coda = resto[(iSlot + PrefissoSlot.Trim().Length)..].Trim();
                resto = resto[..iSlot].Trim();
                foreach (var pezzo in coda.Split('/'))
                {
                    if (int.TryParse(pezzo.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var n))
                        slot.Add(n);
                }
            }

            // Split sulla stringa ", " e non sul carattere ',': un privilegio reale dello SRD si
            // chiama «Movimento senza armatura (+4,5 m)», e spezzarlo sulla virgola decimale
            // produrrebbe due voci senza senso, in silenzio.
            var privilegi = resto.Length == 0
                ? new List<string>()
                : resto.Split(SeparatorePrivilegi, StringSplitOptions.None)
                    .Select(p => p.Trim())
                    .Where(p => p.Length > 0)
                    .ToList();

            righe.Add(new ClassLevelRow(livello, privilegi, slot));
        }
        return righe;
    }

    /// <summary>I livelli già raggiunti da un personaggio di livello <paramref name="livello"/>,
    /// compresi quelli che portano solo slot incantesimo.</summary>
    public static IReadOnlyList<ClassLevelRow> FinoAl(string? testo, int livello)
        => Leggi(testo).Where(r => r.Livello <= livello).OrderBy(r => r.Livello).ToList();

    /// <summary>I soli livelli raggiunti che sbloccano qualcosa da leggere. Sette classi su dodici
    /// hanno livelli con i soli slot — un Mago al 7°, 9°, 11°… non guadagna privilegi — e quelle
    /// righe esistono per portare gli slot, non per essere elencate: mostrarle darebbe un «L7» con
    /// il vuoto accanto, cioè l'aria di dato mancante che qui si vuole togliere.</summary>
    public static IReadOnlyList<ClassLevelRow> PrivilegiFinoAl(string? testo, int livello)
        => FinoAl(testo, livello).Where(r => r.Privilegi.Count > 0).ToList();

    /// <summary>Il primo livello successivo che porta almeno un privilegio, null se non ce ne sono
    /// altri. Per la stessa ragione salta i livelli di soli slot: annunciare «al livello 7:» e non
    /// dire nulla è peggio che tacere.</summary>
    public static ClassLevelRow? ProssimoDopo(string? testo, int livello)
        => Leggi(testo)
            .Where(r => r.Livello > livello && r.Privilegi.Count > 0)
            .OrderBy(r => r.Livello)
            .FirstOrDefault();

    /// <summary>Gli slot incantesimo della riga di livello più alta fino a
    /// <paramref name="livello"/>, vuoto se la classe non incanta. Le tabelle non ripetono gli slot
    /// a ogni livello solo quando non cambiano, quindi vale l'ultima riga che li dichiara.</summary>
    public static IReadOnlyList<int> SlotFinoAl(string? testo, int livello)
        => FinoAl(testo, livello).LastOrDefault(r => r.Slot.Count > 0)?.Slot ?? Array.Empty<int>();

    /// <summary>Vero se il testo contiene almeno una riga nel formato: basta per decidere se c'è
    /// una tabella da mostrare, non per decidere se il campo si può riscrivere (v.
    /// <see cref="SoloProgressione"/>).</summary>
    public static bool SembraProgressione(string? testo) => Leggi(testo).Count > 0;

    /// <summary>Vero se il testo è **soltanto** una tabella: ogni riga non vuota è nel formato.
    /// È la condizione che autorizza un re-import a riscrivere il campo. Con
    /// <see cref="SembraProgressione"/> basterebbe una riga riconosciuta perché una nota aggiunta
    /// in coda alla tabella («Nota: da noi il 3° arriva a fine capitolo») venisse cancellata
    /// insieme al resto, senza alcun segnale.</summary>
    public static bool SoloProgressione(string? testo)
    {
        if (string.IsNullOrWhiteSpace(testo)) return false;

        var righeNonVuote = testo.Split('\n').Count(r => !string.IsNullOrWhiteSpace(r));
        return righeNonVuote > 0 && Leggi(testo).Count == righeNonVuote;
    }

    /// <summary>Le parole con cui lo SRD nomina la scelta di sottoclasse. Nove classi su dodici
    /// usano la parola generica («Sottoclasse del Barbaro», poi «Privilegio di sottoclasse»), ma
    /// Mago, Monaco e Paladino conservano il nome tradizionale — e cercare solo "sottoclasse"
    /// lascerebbe proprio quei tre giocatori senza l'indicazione. L'elenco è chiuso perché lo SRD
    /// lo è; se il pacchetto ne introducesse un altro, il test sul contenuto lo segnalerebbe.</summary>
    private static readonly string[] MarcatoriSottoclasse =
        { "sottoclasse", "tradizione arcana", "tradizione monastica", "giuramento sacro" };

    /// <summary>Vero se il privilegio è la scelta della sottoclasse o un suo avanzamento. Il
    /// confronto ignora le maiuscole perché il pacchetto alterna le grafie da una classe all'altra:
    /// «Sottoclasse del Barbaro» ma «Sottoclasse del ranger».</summary>
    public static bool RiguardaSottoclasse(string? privilegio)
        => privilegio is not null
           && MarcatoriSottoclasse.Any(m => privilegio.Contains(m, StringComparison.OrdinalIgnoreCase));

    /// <summary>La tabella della classe che porta un dato nome, cercata prima fra le righe di
    /// campagna e poi fra le voci di pacchetto. Null se il nome non è a catalogo — scritto a mano,
    /// o catalogo vuoto.
    ///
    /// L'ordine non è negoziabile: una riga di campagna è la classe *di questo tavolo*, e vince su
    /// quella del manuale. Fra più righe omonime si sceglie fra quelle che una tabella ce l'hanno,
    /// con <see cref="CatalogMerge.Representative"/> a fare da spareggio: l'ordine di lettura dal
    /// database non è definito e la scheda non deve cambiare da un caricamento all'altro.
    ///
    /// Quando nessuna omonima ha una tabella, il ripiego sul pacchetto dipende da **da dove viene**
    /// la riga. Se è una riga importata dal manuale (<see cref="CatalogKey.IsFromAppPackage"/>) è
    /// semplicemente vecchia — importata prima che l'import portasse i livelli — e il pacchetto ne
    /// è la versione aggiornata: si ripiega. Se invece è una classe del tavolo, il manuale non
    /// c'entra: mostrarne i privilegi darebbe quelli SRD di una classe deliberatamente sostituita,
    /// e siccome nella pagina Classi quella riga oscura la voce di pacchetto
    /// (<see cref="CatalogMerge"/>), la scheda direbbe il contrario del catalogo.</summary>
    public static string? Risolvi(
        IEnumerable<CharacterClass>? righeDiCampagna,
        IEnumerable<PackageClass>? vociDiPacchetto,
        string? nomeClasse)
    {
        if (string.IsNullOrWhiteSpace(nomeClasse)) return null;
        var chiave = CatalogKey.NormalizeName(nomeClasse);

        var omonime = (righeDiCampagna ?? Enumerable.Empty<CharacterClass>())
            .Where(c => CatalogKey.NormalizeName(c.Name) == chiave)
            .ToList();

        var conTabella = CatalogMerge.Representative(
            omonime.Where(c => SembraProgressione(c.Features)),
            c => c.SourceId,
            c => c.Id);
        if (conTabella is not null) return conTabella.Features;

        // Esiste una riga del tavolo (non importata) e non ha tabella: è la classe di questa
        // campagna, e non ha privilegi da mostrare. Le righe importate senza tabella, invece, sono
        // solo vecchie: per quelle si prosegue e si legge il pacchetto.
        if (omonime.Any(c => !CatalogKey.IsFromAppPackage(c.SourceId))) return null;

        var daPacchetto = (vociDiPacchetto ?? Enumerable.Empty<PackageClass>())
            .FirstOrDefault(c => CatalogKey.NormalizeName(c.Name) == chiave);
        return daPacchetto is null ? null : Serializza(daPacchetto.Levels);
    }

    /// <summary>Toglie gli zeri finali: <c>[4,2,0,0,0,0,0,0,0]</c> diventa <c>[4,2]</c>. Gli zeri
    /// interni restano — una tabella con un buco è comunque quello che dice il manuale.</summary>
    private static List<int> TagliaSlot(List<int>? slot)
    {
        var lista = slot is null ? new List<int>() : new List<int>(slot);
        while (lista.Count > 0 && lista[^1] == 0) lista.RemoveAt(lista.Count - 1);
        return lista;
    }
}
