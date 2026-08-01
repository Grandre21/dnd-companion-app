using DndCompanion.Models.Packages;

namespace DndCompanion.Services;

/// <summary>Aggiungere, modificare e rimuovere una sottoclasse nel campo testuale
/// <c>classes.subclasses</c>. Logica pura: la pagina Classi tiene qui la manipolazione dell'elenco,
/// come il resto del progetto tiene la logica di dominio fuori dai <c>.razor</c>.
///
/// Sta a <see cref="SubclassText"/> come un editor sta a un lettore: quella classe sa solo leggere e
/// scrivere il formato, questa decide <b>che cosa</b> scrivere dopo un'aggiunta, una modifica o una
/// rimozione — comprese le domande che il solo formato testuale non pone da sé, come «questo nome è
/// già in elenco?» o «il campo si può riscrivere senza perdere niente?».
///
/// Il confronto dei nomi è sempre quello di <see cref="CatalogKey.NormalizeName"/>: due sottoclassi
/// che differiscono solo per accenti, maiuscole o spazi iniziali/finali sono la stessa voce —
/// altrimenti «Cammino del Berserker» e « cammino del berserker» convivrebbero nello stesso elenco
/// come due scelte diverse.</summary>
public static class SubclassEditor
{
    /// <summary>Aggiunge una sottoclasse, o la sostituisce se l'elenco ne ha già una con lo stesso
    /// nome: salvare due volte la stessa voce non deve duplicarla. Quando si sta modificando una
    /// voce esistente e nel frattempo le si cambia il nome, <paramref name="nomePrecedente"/> toglie
    /// anche quella — senza, "Cammino del berserker" rinominato in "Cammino della furia" lascerebbe
    /// entrambe le voci in elenco.
    ///
    /// Se il campo non è vuoto e non è <see cref="SubclassText.SoloElenco"/> — cioè contiene testo
    /// che <see cref="SubclassText.Leggi"/> non saprebbe restituire intatto — il testo torna
    /// invariato: è la stessa guardia che nel resto del progetto autorizza un re-import a
    /// riscrivere il campo, e senza di lei "aggiungi sottoclasse" da un campo con una nota scritta a
    /// mano la cancellerebbe in silenzio insieme al resto.</summary>
    public static string Aggiungi(string? testoAttuale, PackageSubclass nuova, string? nomePrecedente = null)
    {
        ArgumentNullException.ThrowIfNull(nuova);
        if (!PuoModificare(testoAttuale)) return testoAttuale ?? string.Empty;

        var chiaveNuova = CatalogKey.NormalizeName(nuova.Name);
        var chiavePrecedente = CatalogKey.NormalizeName(nomePrecedente);

        var elenco = SubclassText.Leggi(testoAttuale).ToList();

        bool Sostituita(PackageSubclass s)
        {
            var chiave = CatalogKey.NormalizeName(s.Name);
            return chiave == chiaveNuova
                   || (chiavePrecedente.Length > 0 && chiave == chiavePrecedente);
        }

        // La voce sostituita resta al **suo** posto: l'ordine dell'elenco è quello che si legge nella
        // card della pagina Classi e nel menu delle tre schermate del personaggio, quindi togliere e
        // accodare faceva saltare in fondo una sottoclasse a cui si era solo corretto un refuso.
        var posizione = elenco.FindIndex(Sostituita);
        elenco.RemoveAll(Sostituita);
        elenco.Insert(posizione < 0 ? elenco.Count : posizione, nuova);

        return SubclassText.Serializza(elenco);
    }

    /// <summary>Vero se <paramref name="nome"/> è già di <b>un'altra</b> voce dell'elenco — cioè di
    /// una diversa da quella che si sta modificando (<paramref name="nomeInModifica"/>). Serve a
    /// fermare un rinomino distruttivo prima che avvenga: <see cref="Aggiungi"/> sostituisce per nome
    /// normalizzato, quindi ribattezzare «Campione» in «Cavaliere» quando un «Cavaliere» esiste già ne
    /// lascia una sola, e descrizione e privilegi dell'altra sono persi senza un avviso. Il nome è
    /// l'unica chiave che questo elenco ha, e questa è la sola domanda che <see cref="Aggiungi"/> non
    /// può porsi da sé: dal suo punto di vista sostituire è esattamente ciò che le si chiede.</summary>
    public static bool CollideConUnAltra(string? testoAttuale, string? nome, string? nomeInModifica)
    {
        var chiave = CatalogKey.NormalizeName(nome);
        if (chiave.Length == 0) return false;

        var quante = SubclassText.Leggi(testoAttuale)
            .Count(s => CatalogKey.NormalizeName(s.Name) == chiave);

        // Con **due** voci già omonime la collisione c'è comunque, anche se sto modificando proprio
        // quel nome: `Aggiungi` le collasserebbe in una sola, perdendo l'altra. Il caso è
        // raggiungibile — il parser controlla presenza e unicità dell'`id` delle sottoclassi, non
        // l'unicità dei nomi, quindi un file con due «Campione» entra a catalogo; e nel ramo a testo
        // libero della pagina Classi due `## Campione` si scrivono a mano.
        if (quante > 1) return true;

        return quante == 1 && chiave != CatalogKey.NormalizeName(nomeInModifica);
    }

    /// <summary>Toglie dall'elenco la sottoclasse che porta quel nome (confronto normalizzato).
    /// Nessun effetto se non c'è, o se il campo è già vuoto: rimuovere due volte la stessa voce non
    /// deve sollevare errori. Stessa guardia di <see cref="Aggiungi"/> sul testo che non è un elenco
    /// puro.</summary>
    public static string Rimuovi(string? testoAttuale, string? nome)
    {
        if (!PuoModificare(testoAttuale)) return testoAttuale ?? string.Empty;

        var chiave = CatalogKey.NormalizeName(nome);
        var elenco = SubclassText.Leggi(testoAttuale)
            .Where(s => CatalogKey.NormalizeName(s.Name) != chiave);

        return SubclassText.Serializza(elenco);
    }

    /// <summary>Vero se il campo si può gestire dall'elenco strutturato senza perdere niente: vuoto
    /// (non c'è ancora nulla da perdere) o <see cref="SubclassText.SoloElenco"/> (il campo è già
    /// soltanto un elenco). Falso per un campo con prosa — anche una sola riga di nota sopra un
    /// elenco altrimenti valido — perché un giro di riscrittura la scarterebbe in silenzio: è la
    /// condizione che la pagina Classi verifica prima di offrire "aggiungi/modifica/rimuovi", non
    /// per decidere se ci sono sottoclassi da mostrare (quello lo dice
    /// <see cref="SubclassText.SembraElenco"/>).</summary>
    public static bool PuoModificare(string? testoAttuale)
        => string.IsNullOrWhiteSpace(testoAttuale) || SubclassText.SoloElenco(testoAttuale);

    /// <summary>Costruisce la sottoclasse dai campi del mini-form della pagina Classi: nome,
    /// descrizione e i privilegi per livello nello stesso formato testuale della tabella di classe
    /// (<c>L3 — Frenesia</c>, v. <see cref="ClassProgression"/>). Tiene questa conversione fuori dal
    /// <c>.razor</c>, come il resto della logica di dominio del progetto.</summary>
    public static PackageSubclass Costruisci(string? nome, string? descrizione, string? livelliTesto, string? id = null)
        => new()
        {
            Id = (id ?? string.Empty).Trim(),
            Name = (nome ?? string.Empty).Trim(),
            Description = (descrizione ?? string.Empty).Trim(),
            Levels = ClassProgression.Leggi(livelliTesto)
                .Select(r => new PackageClassLevel
                {
                    Level = r.Livello,
                    Features = r.Privilegi.ToList(),
                    SpellSlots = r.Slot.ToList(),
                })
                .ToList(),
        };
}
