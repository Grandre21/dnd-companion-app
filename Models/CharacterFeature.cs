namespace DndCompanion.Models;

/// <summary>
/// L'annotazione dell'utente su un privilegio: le sue parole, il momento del turno in cui si usa,
/// e l'eventuale contatore collegato. Elemento del jsonb <c>character_features</c> di
/// <see cref="Character"/> (POCO, non una tabella a sé): stesso pattern di
/// <see cref="ClassResource"/> dentro <c>class_resources</c>.
///
/// <b>Il nome del privilegio non è un dato di questa voce: è la sua chiave.</b> L'elenco dei
/// privilegi si deriva dal pacchetto SRD (v. spec 2026-08-08, D2) e non si salva mai — così al
/// level-up le schede nuove compaiono da sole e «quali privilegi ho» conserva una sola risposta
/// possibile. Una voce il cui <see cref="Nome"/> non corrisponde a nessun privilegio derivato è
/// semplicemente una voce propria, scritta a mano: non è un errore e non si cancella.
///
/// <b>Cinque campi e nessun campo effetto</b>, come <see cref="ClassResource"/> ne ha quattro e
/// nessuna formula. Niente bonus, niente formule, niente riferimenti a caratteristiche: da «mostra
/// la nota dell'Ira» a «applica il +2 al danno» il passo sembra breve ed è un burrone — semantica
/// D&amp;D senza fondo, e descrizioni ufficiali che questo repo non ha licenza di ridistribuire.
/// La scheda cartacea dell'utente è la prova che la sua prosa basta.
/// </summary>
public class CharacterFeature
{
    /// <summary>Il nome del privilegio annotato. Si confronta normalizzato
    /// (<c>CatalogKey.NormalizeName</c>), mai per uguaglianza cruda.</summary>
    public string Nome { get; set; } = string.Empty;

    /// <summary>Cosa fa, con le parole dell'utente. È il prodotto della funzione, non un ripiego.</summary>
    public string Nota { get; set; } = string.Empty;

    /// <summary>Momento del turno: <c>azione</c>, <c>bonus</c>, <c>reazione</c>, <c>passivo</c>,
    /// <c>turno</c>. <b>Null significa «da classificare»</b> e si rende come tale: in combattimento
    /// un tag indovinato è peggio di un tag mancante.</summary>
    public string? Azione { get; set; }

    /// <summary>Nome della <see cref="ClassResource"/> collegata, se il privilegio ha usi da
    /// contare. Null quando non ne ha.</summary>
    public string? Risorsa { get; set; }

    /// <summary>Se true la scheda mostra l'interruttore «attivo». Lo stato acceso NON sta qui:
    /// vive in localStorage (v. spec, D4), perché scriverlo su characters significherebbe un
    /// Update di riga intera a ogni accensione.</summary>
    public bool Attivabile { get; set; }
}
