using System.Text;
using DndCompanion.Models.Packages;

namespace DndCompanion.Services;

/// <summary>Le sottoclassi dentro la colonna testuale <c>classes.subclasses</c>. Logica pura.
///
/// Sta a <see cref="ClassProgression"/> come una sottoclasse sta a una classe: stessa scelta di
/// fondo — i dati vivono in un campo di testo, in una forma che una persona legge a occhio e il
/// codice rilegge senza ambiguità — e stessa sintassi per i livelli, perché il formato di
/// serializzazione dei privilegi deve restare uno solo.
///
/// <code>
/// ## Cammino del berserker
/// id: srd-2024-it/sottoclasse/cammino-del-berserker
/// Chi percorre questo cammino incanala la furia in una violenza cieca.
/// L3 — Frenesia
/// L6 — Ira incontenibile
///
/// ## Cammino del guerriero totemico
/// L3 — Spirito totemico
/// </code>
///
/// Le regole di lettura, tutte scelte per non perdere niente di ciò che qualcuno ha scritto a mano:
/// <list type="bullet">
/// <item>una riga che comincia per <c>##</c> apre un blocco; il resto della riga è il nome, e senza
/// nome il blocco non esiste (chiude il precedente e viene ignorato);</item>
/// <item><c>id:</c> vale solo come <b>prima</b> riga non vuota del blocco. Serve alla fedeltà
/// dell'export — l'id di una sottoclasse non ha altri usi nell'app — e altrove è descrizione;</item>
/// <item>una riga nel formato <c>L&lt;n&gt; — …</c> è un livello, tutto il resto è descrizione;</item>
/// <item>le righe vuote <b>dentro</b> la descrizione sono stacchi di capoverso e si conservano — le
/// descrizioni SRD ne hanno da cinque a sette ciascuna; quelle prima del primo contenuto e quelle in
/// coda no, altrimenti il testo crescerebbe di una riga a ogni giro di export;</item>
/// <item>ciò che precede il primo <c>##</c> si ignora: è testo di chi non conosce il formato, e non
/// va scambiato per una sottoclasse senza nome.</item>
/// </list>
///
/// Limite noto e accettato: una riga di descrizione che cominci per <c>##</c> o che imiti
/// <c>L3 — …</c> viene riletta per quel che sembra. Le descrizioni dello SRD sono prosa, e l'unica
/// alternativa era una sintassi di escape che avrebbe reso il campo illeggibile a occhio — cioè
/// avrebbe tolto la ragione per cui il formato è testo.</summary>
public static class SubclassText
{
    private const string Intestazione = "##";
    private const string PrefissoId = "id:";

    /// <summary>Rende le sottoclassi nel formato testuale. Le voci senza nome si scartano: un blocco
    /// senza intestazione non sarebbe rileggibile, e il nome è la sola cosa che il personaggio
    /// sceglie davvero.</summary>
    public static string Serializza(IEnumerable<PackageSubclass>? sottoclassi)
    {
        if (sottoclassi is null) return string.Empty;

        var sb = new StringBuilder();
        foreach (var s in sottoclassi.Where(s => s is not null && !string.IsNullOrWhiteSpace(s.Name)))
        {
            if (sb.Length > 0) sb.Append("\n\n");
            sb.Append(Intestazione).Append(' ').Append(s.Name.Trim());

            if (!string.IsNullOrWhiteSpace(s.Id))
                sb.Append('\n').Append(PrefissoId).Append(' ').Append(s.Id.Trim());

            // I ritorni a capo si normalizzano a '\n': il campo si scrive anche a mano in un
            // textarea, e su Windows il browser manda "\r\n" — un '\r' in coda alla riga
            // sopravviverebbe al giro e cambierebbe il testo esportato senza che nessuno abbia
            // toccato niente.
            var descrizione = (s.Description ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Trim();
            if (descrizione.Length > 0) sb.Append('\n').Append(descrizione);

            var livelli = ClassProgression.Serializza(s.Levels);
            if (livelli.Length > 0) sb.Append('\n').Append(livelli);
        }
        return sb.ToString();
    }

    /// <summary>Rilegge il formato. Un testo che non lo rispetta dà una lista vuota — non un errore:
    /// il campo resta testo libero, e chi chiama deve trattare la lista vuota come «questa classe non
    /// dichiara sottoclassi».</summary>
    public static IReadOnlyList<PackageSubclass> Leggi(string? testo)
    {
        if (string.IsNullOrWhiteSpace(testo)) return Array.Empty<PackageSubclass>();

        var voci = new List<PackageSubclass>();
        PackageSubclass? corrente = null;
        var descrizione = new List<string>();
        var livelli = new StringBuilder();
        var visteRighe = false;

        void Chiudi()
        {
            if (corrente is null) return;

            // Le righe vuote in coda si tolgono: sono quelle che stavano fra la descrizione e i
            // livelli, o dopo l'ultimo livello, e conservarle farebbe crescere il testo di una riga a
            // ogni giro di export.
            while (descrizione.Count > 0 && descrizione[^1].Length == 0)
                descrizione.RemoveAt(descrizione.Count - 1);

            corrente.Description = string.Join("\n", descrizione);
            corrente.Levels = ClassProgression.Leggi(livelli.ToString())
                .Select(r => new PackageClassLevel
                {
                    Level = r.Livello,
                    Features = r.Privilegi.ToList(),
                    SpellSlots = r.Slot.ToList(),
                })
                .ToList();
            voci.Add(corrente);

            corrente = null;
            descrizione.Clear();
            livelli.Clear();
            visteRighe = false;
        }

        foreach (var raw in testo.Split('\n'))
        {
            var riga = raw.Trim();

            if (riga.StartsWith(Intestazione, StringComparison.Ordinal))
            {
                Chiudi();
                var nome = riga[Intestazione.Length..].TrimStart('#').Trim();
                if (nome.Length > 0) corrente = new PackageSubclass { Name = nome };
                continue;
            }

            // Fuori da un blocco non c'è niente da raccogliere.
            if (corrente is null) continue;

            // Le righe vuote **dentro** la descrizione si conservano: sono gli stacchi di capoverso, e
            // tutte e dodici le descrizioni di sottoclasse del manuale ne hanno da cinque a sette.
            // Scartarle appiattiva un testo di quattromila caratteri in un blocco unico — visibile sia
            // nel file esportato sia nella scheda, che lo rende con `white-space: pre-wrap`. Solo
            // dentro la descrizione, però: prima della prima riga di contenuto non c'è capoverso da
            // separare, e in coda le toglie `Chiudi`.
            if (riga.Length == 0)
            {
                if (descrizione.Count > 0) descrizione.Add(string.Empty);
                continue;
            }

            if (!visteRighe && riga.StartsWith(PrefissoId, StringComparison.OrdinalIgnoreCase))
            {
                corrente.Id = riga[PrefissoId.Length..].Trim();
                visteRighe = true;
                continue;
            }
            visteRighe = true;

            if (ClassProgression.SembraProgressione(riga))
            {
                if (livelli.Length > 0) livelli.Append('\n');
                livelli.Append(riga);
                continue;
            }

            descrizione.Add(riga);
        }
        Chiudi();

        return voci;
    }

    /// <summary>Vero se il testo contiene almeno un blocco leggibile. Basta per decidere se una riga
    /// di catalogo ha sottoclassi da offrire, <b>non</b> per decidere se il campo si può
    /// riscrivere (v. <see cref="SoloElenco"/>).</summary>
    public static bool SembraElenco(string? testo) => Leggi(testo).Count > 0;

    /// <summary>Vero se il testo è <b>soltanto</b> un elenco di sottoclassi: la prima riga non vuota
    /// apre un blocco. È la condizione che autorizza un re-import a riscrivere il campo — con
    /// <see cref="SembraElenco"/> basterebbe un blocco riconosciuto perché una nota scritta in cima
    /// («da noi la sottoclasse si sceglie al 2°») venisse cancellata insieme al resto, in
    /// silenzio.</summary>
    public static bool SoloElenco(string? testo)
    {
        if (string.IsNullOrWhiteSpace(testo)) return false;

        var prima = testo.Split('\n')
            .Select(r => r.Trim())
            .FirstOrDefault(r => r.Length > 0);

        return prima is not null
               && prima.StartsWith(Intestazione, StringComparison.Ordinal)
               && Leggi(testo).Count > 0;
    }
}
