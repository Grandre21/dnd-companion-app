using DndCompanion.Models.Packages;

namespace DndCompanion.Services;

/// <summary>
/// JOIN logico in memoria fra ciò che il PG ha scritto (testo libero di <c>Character.Feats</c>,
/// nome singolo di <c>Character.Background</c>) e il pacchetto SRD già caricato in memoria.
/// Estratto da CharacterBioTab/Characters per essere testabile. Pura: nessuno stato/I/O.
/// </summary>
public static class CharacterManualJoin
{
    /// <summary>
    /// I talenti del catalogo il cui nome compare come <b>parola intera</b> in
    /// <paramref name="testoTalenti"/>. <c>Character.Feats</c> è una textarea di testo libero (può
    /// essere «Attento, Fortunato», un nome per riga, o prosa): non la spezziamo, cerchiamo invece
    /// il nome di ogni talento del catalogo al suo interno, così un paragrafo resta un paragrafo.
    /// Confronto insensibile a maiuscole/accenti (<see cref="CatalogKey.NormalizeName"/>); «attento»
    /// non deve combaciare dentro «disattento», quindi i caratteri immediatamente prima e dopo
    /// l'occorrenza non devono essere lettere o cifre. Ordinati per posizione di apparizione nel
    /// testo, non per ordine di catalogo: l'elenco segue l'ordine in cui il giocatore li ha scritti.
    /// Testo null/vuoto o catalogo vuoto → lista vuota, mai un'eccezione.
    /// </summary>
    public static IReadOnlyList<PackageFeat> TalentiRiconosciuti(
        string? testoTalenti, IReadOnlyList<PackageFeat> catalogo)
    {
        var risultato = new List<PackageFeat>();
        if (string.IsNullOrWhiteSpace(testoTalenti) || catalogo is null || catalogo.Count == 0)
            return risultato;

        var testo = CatalogKey.NormalizeName(testoTalenti);
        var trovati = new List<(int Posizione, PackageFeat Talento)>();

        foreach (var talento in catalogo)
        {
            var nome = CatalogKey.NormalizeName(talento.Name);
            if (nome.Length == 0) continue;

            var posizione = IndicePrimaOccorrenzaParolaIntera(testo, nome);
            if (posizione >= 0) trovati.Add((posizione, talento));
        }

        return trovati.OrderBy(t => t.Posizione).Select(t => t.Talento).ToList();
    }

    /// <summary>Cerca <paramref name="parola"/> in <paramref name="testo"/> (entrambi già
    /// normalizzati) e restituisce la posizione della prima occorrenza che ha caratteri non
    /// alfanumerici (o i bordi della stringa) subito prima e subito dopo; -1 se nessuna
    /// occorrenza rispetta il vincolo.</summary>
    private static int IndicePrimaOccorrenzaParolaIntera(string testo, string parola)
    {
        var indice = 0;
        while (true)
        {
            indice = testo.IndexOf(parola, indice, StringComparison.Ordinal);
            if (indice < 0) return -1;

            var confineIniziale = indice == 0 || !char.IsLetterOrDigit(testo[indice - 1]);
            var fine = indice + parola.Length;
            var confineFinale = fine >= testo.Length || !char.IsLetterOrDigit(testo[fine]);
            if (confineIniziale && confineFinale) return indice;

            indice++;
        }
    }

    /// <summary>Il background del catalogo il cui nome combacia <b>esattamente</b> (normalizzato)
    /// con <paramref name="nomeBackground"/>; il primo che combacia, <c>null</c> se nessuno.
    /// <c>Character.Background</c> è un campo singolo, non testo libero: qui il match è esatto,
    /// non "parola intera" come per i talenti.</summary>
    public static PackageBackground? BackgroundRiconosciuto(
        string? nomeBackground, IReadOnlyList<PackageBackground> catalogo)
    {
        if (string.IsNullOrWhiteSpace(nomeBackground) || catalogo is null) return null;

        var chiave = CatalogKey.NormalizeName(nomeBackground);
        return catalogo.FirstOrDefault(b => CatalogKey.NormalizeName(b.Name) == chiave);
    }
}
