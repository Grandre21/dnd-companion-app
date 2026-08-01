using DndCompanion.Models;
using DndCompanion.Models.Packages;

namespace DndCompanion.Services;

/// <summary>Le sottoclassi che una classe offre, da qualunque sorgente. Logica pura.
///
/// Vive a parte da <see cref="ClassProgression"/> perché risponde a una domanda diversa: non «cosa
/// dà questa classe al livello N», ma «quali scelte ho al 3° livello e come si chiamano». Il campo
/// <c>Subclass</c> del personaggio è testo libero e resta tale — un tavolo può inventarsi la
/// propria sottoclasse — ma quando il catalogo ne conosce una, sceglierla da un elenco evita di
/// scrivere un nome che poi nessuna schermata riconosce.
///
/// Due livelli, e vanno tenuti distinti: <see cref="PerClasse"/> guarda il <b>pacchetto</b> (il
/// manuale precaricato o un file appena letto), <see cref="Disponibili"/> guarda il <b>catalogo di
/// campagna</b> e ripiega sul pacchetto. Le schermate chiamano la seconda: dal 2026-08-01 le
/// sottoclassi hanno una casa nei dati (<c>classes.subclasses</c>, v. <see cref="SubclassText"/>),
/// quindi anche una classe del tavolo può averne di proprie.</summary>
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

    /// <summary>Le sottoclassi che una classe offre <b>davvero</b>, da qualunque sorgente: la
    /// colonna <c>subclasses</c> della riga di campagna se c'è, altrimenti il manuale. È la funzione
    /// che le schermate devono chiamare: <see cref="PerClasse"/> guarda il solo pacchetto, e con
    /// quella una classe del tavolo — o una importata e poi arricchita a mano — non aveva modo di
    /// offrire le proprie.
    ///
    /// L'ordine è lo stesso di <see cref="ClassProgression.Risolvi"/>, e non è negoziabile: una riga
    /// di campagna è la classe <i>di questo tavolo</i> e vince sul manuale; fra più righe omonime si
    /// sceglie fra quelle che un elenco ce l'hanno, con <see cref="CatalogMerge.Representative"/> a
    /// fare da spareggio, perché l'ordine di lettura dal database non è definito e il menu non deve
    /// cambiare da un caricamento all'altro.
    ///
    /// Quando nessuna omonima dichiara sottoclassi, il ripiego sul pacchetto dipende da <b>da
    /// dove</b> viene la riga: se è importata dal manuale è semplicemente vecchia — creata prima che
    /// l'import portasse le sottoclassi — e il pacchetto ne è la versione aggiornata; se è una
    /// classe del tavolo, il manuale non c'entra, e offrire l'Invocatore per una «Mago» che quel
    /// tavolo ha deliberatamente sostituito farebbe dire alla stessa schermata due cose
    /// incoerenti.</summary>
    public static IReadOnlyList<PackageSubclass> Disponibili(
        IEnumerable<CharacterClass>? righeDiCampagna,
        IEnumerable<PackageClass>? vociDiPacchetto,
        string? nomeClasse)
    {
        if (string.IsNullOrWhiteSpace(nomeClasse)) return Array.Empty<PackageSubclass>();
        var chiave = CatalogKey.NormalizeName(nomeClasse);

        var omonime = (righeDiCampagna ?? Enumerable.Empty<CharacterClass>())
            .Where(c => c is not null && CatalogKey.NormalizeName(c.Name) == chiave)
            .ToList();

        var conElenco = CatalogMerge.Representative(
            omonime.Where(c => SubclassText.SembraElenco(c.Subclasses)),
            c => c.SourceId,
            c => c.Id);
        if (conElenco is not null) return SubclassText.Leggi(conElenco.Subclasses);

        if (omonime.Any(c => !CatalogKey.IsFromAppPackage(c.SourceId))) return Array.Empty<PackageSubclass>();

        return PerClasse(vociDiPacchetto, nomeClasse);
    }

    /// <summary>La sottoclasse scelta, se il manuale la conosce. Null quando il nome è stato
    /// scritto a mano: è un caso legittimo, non un errore.</summary>
    public static PackageSubclass? Trova(
        IEnumerable<PackageClass>? classiDiManuale, string? nomeClasse, string? nomeSottoclasse)
        => Cerca(PerClasse(classiDiManuale, nomeClasse), nomeSottoclasse);

    /// <summary>Come <see cref="Trova(IEnumerable{PackageClass}, string, string)"/>, ma sulle
    /// sottoclassi <b>risolte</b> (v. <see cref="Disponibili"/>): è questa che la scheda deve usare
    /// per trovare i privilegi da mostrare, altrimenti una sottoclasse del tavolo si vede nel menu e
    /// poi non porta niente.</summary>
    public static PackageSubclass? Trova(
        IEnumerable<CharacterClass>? righeDiCampagna,
        IEnumerable<PackageClass>? vociDiPacchetto,
        string? nomeClasse,
        string? nomeSottoclasse)
        => Cerca(Disponibili(righeDiCampagna, vociDiPacchetto, nomeClasse), nomeSottoclasse);

    private static PackageSubclass? Cerca(IEnumerable<PackageSubclass> fra, string? nomeSottoclasse)
    {
        if (string.IsNullOrWhiteSpace(nomeSottoclasse)) return null;
        var chiave = CatalogKey.NormalizeName(nomeSottoclasse);

        return fra.FirstOrDefault(s => CatalogKey.NormalizeName(s.Name) == chiave);
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
    /// <param name="righeDiCampagna">Le classi di questa campagna: sono la prima sorgente delle
    /// sottoclassi (v. <see cref="Disponibili"/>) e servono a porre la <b>stessa</b> domanda delle
    /// schermate che offrono la scelta. Ometterle vale «nessuna riga di campagna» — è ciò che vuole
    /// chi ragiona sul solo manuale — ma chi le ha in mano le passi: senza, il criterio si accontenta
    /// del nome presente nel manuale, e un tavolo con la propria «Mago» perde la sottoclasse
    /// «Campione» alla sola apertura della modifica.</param>
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
        var righe = (righeDiCampagna ?? Enumerable.Empty<CharacterClass>())
            .Where(c => c is not null)
            .ToList();

        // Si torna il nome **come lo scrive il catalogo**, non quello che si aveva in mano: il
        // confronto normalizza accenti, maiuscole e spazi, ma il `<select>` accosta le stringhe per
        // intero — un «invocatore» salvato a mano lascerebbe il menu senza selezione pur essendo la
        // scelta giusta, e chi salva di nuovo si ritroverebbe il campo svuotato.
        var aCatalogo = Disponibili(righe, classi, nomeClasse);
        var scelta = aCatalogo.FirstOrDefault(s => CatalogKey.NormalizeName(s.Name) == chiave);
        if (scelta is not null) return new SceltaSottoclasse(scelta.Name, false);

        // Se per questa classe non esiste nessun elenco, il menu non è stato nemmeno offerto: non
        // c'è un insieme da cui la scelta possa essere «uscita», e cancellarla sarebbe una perdita
        // che si consuma alla sola apertura della modifica, senza che nessuno abbia toccato niente.
        // È il caso del «Guerriero del sale» che chiama «Campione» la propria sottoclasse — e quello
        // di una campagna che ha sostituito una classe del manuale con la propria: la domanda deve
        // essere la stessa che pongono le schermate prima di offrire il menu (`Disponibili`), perché
        // scollegare è distruttivo quanto conservare, e il criterio che cancella non può essere il
        // più debole dei due.
        if (aCatalogo.Count == 0) return new SceltaSottoclasse(corrente, true);

        // Resta il caso che conta: la classe offre un elenco, il valore non ne fa parte, ed è la
        // sottoclasse di **un'altra** classe. Senza questo ramo il menu mostrava «Nessuna» mentre il
        // campo conservava il valore, e si salvava un Mago con il Cammino del berserker.
        return DiUnAltraClasse(righe, classi, nomeClasse, chiave)
            ? new SceltaSottoclasse(string.Empty, false)
            : new SceltaSottoclasse(corrente, true);
    }

    /// <summary>Vero se quel nome di sottoclasse appartiene a una classe <b>diversa</b> da quella
    /// data, in qualunque sorgente. Le omonime si escludono: sono la stessa classe, e se avessero la
    /// sottoclasse la si sarebbe già trovata fra le disponibili.</summary>
    private static bool DiUnAltraClasse(
        List<CharacterClass> righe, List<PackageClass> classi, string? nomeClasse, string chiaveSottoclasse)
    {
        var chiaveClasse = CatalogKey.NormalizeName(nomeClasse);

        bool Altrove(string? nome, IEnumerable<PackageSubclass>? sottoclassi)
            => CatalogKey.NormalizeName(nome) != chiaveClasse
               && (sottoclassi ?? Enumerable.Empty<PackageSubclass>())
                   .Any(s => s is not null && CatalogKey.NormalizeName(s.Name) == chiaveSottoclasse);

        // Le classi che il tavolo ha scritto valgono sempre: se una sua classe rivendica quel nome, la
        // risposta è diretta. Il **materiale del manuale** si consulta solo se la classe corrente è
        // ancora quella del manuale: se il tavolo l'ha sostituita con una propria, che «Cammino del
        // berserker» sia del Barbaro SRD non dice niente su una sottoclasse inventata per una classe
        // che di SRD non ha più nulla — e cancellarla sarebbe una perdita, alla sola apertura della
        // modifica. È la regola scritta nel DIARIO, e vale anche quando la classe del tavolo un elenco
        // proprio ce l'ha: il criterio che cancella non può essere più forte di quello che offre.
        //
        // «Materiale del manuale» comprende le righe **importate** dal manuale, non solo il pacchetto:
        // da quando l'import scrive la colonna, il Barbaro importato porta lo stesso testo SRD che
        // porta il pacchetto. Guardare solo il pacchetto faceva dipendere l'esito dal fatto che il
        // tavolo avesse importato le classi — stesso contenuto, due risposte diverse.
        var delTavolo = righe.Where(c => !CatalogKey.IsFromAppPackage(c.SourceId)).ToList();
        var diManuale = righe.Where(c => CatalogKey.IsFromAppPackage(c.SourceId)).ToList();

        if (delTavolo.Any(c => Altrove(c.Name, SubclassText.Leggi(c.Subclasses)))) return true;

        return ClassProgression.ClasseDelManuale(righe, nomeClasse)
               && (diManuale.Any(c => Altrove(c.Name, SubclassText.Leggi(c.Subclasses)))
                   || classi.Any(c => Altrove(c.Name, c.Subclasses)));
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
