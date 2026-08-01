using DndCompanion.Models;
using DndCompanion.Models.Packages;

namespace DndCompanion.Services;

/// <summary>Le sottoclassi che il manuale offre per una classe. Logica pura.
///
/// Vive a parte da <see cref="ClassProgression"/> perché risponde a una domanda diversa: non «cosa
/// dà questa classe al livello N», ma «quali scelte ho al 3° livello e come si chiamano». Il campo
/// <c>Subclass</c> del personaggio è testo libero e resta tale — un tavolo può inventarsi la
/// propria sottoclasse — ma quando il manuale ne conosce una, sceglierla da un elenco evita di
/// scrivere un nome che poi nessuna schermata riconosce.</summary>
public static class SubclassCatalog
{
    /// <summary>Le sottoclassi della classe che porta quel nome, vuoto se la classe non è nel
    /// manuale. Il confronto è normalizzato come nel resto dei cataloghi.</summary>
    public static IReadOnlyList<PackageSubclass> PerClasse(
        IEnumerable<PackageClass>? classiDiManuale, string? nomeClasse)
    {
        if (string.IsNullOrWhiteSpace(nomeClasse)) return Array.Empty<PackageSubclass>();
        var chiave = CatalogKey.NormalizeName(nomeClasse);

        var classe = (classiDiManuale ?? Enumerable.Empty<PackageClass>())
            .Where(c => c is not null)
            .FirstOrDefault(c => CatalogKey.NormalizeName(c.Name) == chiave);

        // Il filtro sui null è difesa in profondità: `NormalizeLists` normalizza le liste, non i loro
        // elementi, e a respingere un `"subclasses": [null]` è la validazione del parser (che dal
        // 2026-08-01 controlla anche questa sezione). Resta perché l'helper è pubblico e può ricevere
        // liste che da quel controllo non sono passate.
        return (classe?.Subclasses ?? Enumerable.Empty<PackageSubclass>())
            .Where(s => s is not null && !string.IsNullOrWhiteSpace(s.Name))
            .ToList();
    }

    /// <summary>La sottoclasse scelta, se il manuale la conosce. Null quando il nome è stato
    /// scritto a mano: è un caso legittimo, non un errore.</summary>
    public static PackageSubclass? Trova(
        IEnumerable<PackageClass>? classiDiManuale, string? nomeClasse, string? nomeSottoclasse)
    {
        if (string.IsNullOrWhiteSpace(nomeSottoclasse)) return null;
        var chiave = CatalogKey.NormalizeName(nomeSottoclasse);

        return PerClasse(classiDiManuale, nomeClasse)
            .FirstOrDefault(s => CatalogKey.NormalizeName(s.Name) == chiave);
    }

    /// <summary>I privilegi che la sottoclasse ha già sbloccato a un dato livello, nella stessa
    /// forma usata per quelli di classe: la scheda li mostra con lo stesso markup, e il formato di
    /// serializzazione resta uno solo.</summary>
    public static IReadOnlyList<ClassLevelRow> PrivilegiFinoAl(PackageSubclass? sottoclasse, int livello)
        => sottoclasse is null
            ? Array.Empty<ClassLevelRow>()
            : ClassProgression.PrivilegiFinoAl(ClassProgression.Serializza(sottoclasse.Levels), livello);

    /// <summary>Come deve comportarsi il campo Sottoclasse dopo che la classe è cambiata (o quando
    /// si apre la modifica di un personaggio esistente).</summary>
    /// <param name="Valore">Il nome da tenere nel campo: vuoto se la scelta non ha più senso.</param>
    /// <param name="ScrittaAMano">Vero se va mostrata nel campo libero invece che nel menu.</param>
    public sealed record SceltaSottoclasse(string Valore, bool ScrittaAMano);

    /// <summary>Decide che fare della sottoclasse corrente quando cambia la classe, o all'apertura
    /// di una scheda già compilata. Tre casi, e il terzo è quello che conta:
    ///
    /// <list type="bullet">
    /// <item>è fra quelle della classe → si tiene, e sta nel menu;</item>
    /// <item>non è di nessuna classe del manuale → si tiene, ma nel campo libero: può essere una
    /// sottoclasse inventata dal tavolo, e cancellarla sarebbe peggio che mostrarla;</item>
    /// <item>appartiene al manuale ma a <b>un'altra</b> classe → si toglie. Senza questo, il menu
    /// mostrava «Nessuna» mentre il campo conservava il valore, e si salvava un Mago con il
    /// Cammino del berserker.</item>
    /// </list>
    /// </summary>
    /// <param name="righeDiCampagna">Le classi di questa campagna. Servono a porre la <b>stessa</b>
    /// domanda delle schermate che offrono la scelta (<see cref="ClassProgression.ClasseDelManuale"/>):
    /// se il tavolo ha sostituito quella classe con una propria, il manuale non c'entra e non deve
    /// togliere niente. Ometterle vale «nessuna riga di campagna» — è ciò che vuole chi ragiona sul
    /// solo manuale — ma chi le ha in mano le passi: senza, il criterio si accontenta del nome
    /// presente nel manuale, e un tavolo con la propria «Mago» perde la sottoclasse «Campione» alla
    /// sola apertura della modifica.</param>
    public static SceltaSottoclasse RisolviScelta(
        IEnumerable<PackageClass>? classiDiManuale, string? nomeClasse, string? sottoclasseCorrente,
        IEnumerable<CharacterClass>? righeDiCampagna = null)
    {
        var corrente = sottoclasseCorrente ?? string.Empty;
        if (string.IsNullOrWhiteSpace(corrente)) return new SceltaSottoclasse(string.Empty, false);

        var chiave = CatalogKey.NormalizeName(corrente);
        var classi = (classiDiManuale ?? Enumerable.Empty<PackageClass>())
            .Where(c => c is not null)
            .ToList();

        // Si torna il nome **come lo scrive il manuale**, non quello che si aveva in mano: il
        // confronto normalizza accenti, maiuscole e spazi, ma il `<select>` accosta le stringhe per
        // intero — un «invocatore» salvato a mano lascerebbe il menu senza selezione pur essendo la
        // scelta giusta, e chi salva di nuovo si ritroverebbe il campo svuotato.
        var nelManuale = PerClasse(classi, nomeClasse)
            .FirstOrDefault(s => CatalogKey.NormalizeName(s.Name) == chiave);
        if (nelManuale is not null) return new SceltaSottoclasse(nelManuale.Name, false);

        // Il ramo «è di un'altra classe» si valuta solo se la classe corrente è ancora quella del
        // manuale. Altrimenti non c'è nessun confronto da fare: il «Guerriero del sale» di un tavolo
        // può chiamare «Campione» la propria sottoclasse, e togliergliela sarebbe una perdita — che
        // si consumerebbe alla sola apertura della modifica, senza che nessuno abbia toccato niente.
        //
        // Non basta che il nome compaia nel manuale: vale anche per una campagna che ha sostituito
        // quella classe con la propria (stesso nome, righe sue). È la domanda che pongono le
        // schermate prima di offrire il menu, e qui deve essere la stessa — scollegare è distruttivo
        // quanto conservare, quindi il criterio che cancella non può essere il più debole dei due.
        var classeNelManuale = !string.IsNullOrWhiteSpace(nomeClasse)
            && ClassProgression.ClasseDelManuale(righeDiCampagna, nomeClasse)
            && classi.Any(c => CatalogKey.NormalizeName(c.Name) == CatalogKey.NormalizeName(nomeClasse));

        var diAltraClasse = classeNelManuale
            && classi.Any(c => (c.Subclasses ?? Enumerable.Empty<PackageSubclass>())
                .Where(s => s is not null)
                .Any(s => CatalogKey.NormalizeName(s.Name) == chiave));

        return diAltraClasse
            ? new SceltaSottoclasse(string.Empty, false)
            : new SceltaSottoclasse(corrente, true);
    }

    /// <summary>Il livello a cui la sottoclasse comincia a dare qualcosa (3 in tutto lo SRD), o
    /// null se non dichiara privilegi. Serve a dire a chi crea un personaggio di livello 1 o 2
    /// perché il campo è ancora vuoto, invece di lasciarlo sembrare dimenticato.</summary>
    public static int? PrimoLivello(PackageSubclass? sottoclasse)
    {
        var livelli = (sottoclasse?.Levels ?? Enumerable.Empty<PackageClassLevel>())
            .Where(l => l is not null && l.Features is { Count: > 0 })
            .Select(l => l.Level)
            .ToList();
        return livelli is { Count: > 0 } ? livelli.Min() : null;
    }
}
