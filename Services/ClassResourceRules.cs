using DndCompanion.Models;

namespace DndCompanion.Services;

/// <summary>
/// Le risorse di classe con i loro usi (Ira, Ispirazione bardica, Focus del monaco, ...) — le
/// caselline a matita accanto ai privilegi sulla scheda cartacea. Logica pura sul campo jsonb
/// <c>characters.class_resources</c> (v. spec 2026-08-06, sezione «Le risorse di classe»).
///
/// Nome della classe statica diverso dalla proprietà che manipola (<c>Character.ClassResources</c>)
/// apposta: <c>ClassResources</c> per l'helper avrebbe reso ambiguo ogni <c>using</c> che vede
/// entrambi.
///
/// <b>Cosa NON fa</b>: non decide quale riposo ripristina quali risorse — quella regola vive in
/// <c>RestCalculations</c> e solo lì, per non avere due implementazioni della stessa cosa (il
/// difetto di giuntura tipico di questo repo). <see cref="SiRipristinaCon"/> esiste solo perché
/// quella regola, ovunque sia scritta, deve confrontare lo stesso valore di <c>Ricarica</c> con lo
/// stesso significato: un predicato puro qui evita che le due implementazioni divergano.
/// </summary>
public static class ClassResourceRules
{
    /// <summary>I tre valori ammessi per <see cref="ClassResource.Ricarica"/>. "nessuna" è per le
    /// risorse che si ripristinano in altro modo (una volta per turno, a discrezione del master):
    /// il contatore c'è, il riposo non lo tocca mai.</summary>
    private static readonly HashSet<string> RicaricheAmmesse =
        new(StringComparer.Ordinal) { "lungo", "breve", "nessuna" };

    /// <summary>Tetto a <see cref="ClassResource.Max"/> dentro <see cref="Normalizza"/>: il
    /// componente disegna un pallino per uso, e un jsonb scritto a mano (o un bug futuro) con un
    /// <c>Max</c> a sei cifre bloccherebbe il rendering. 99 usi bastano e avanzano per qualunque
    /// risorsa SRD o scritta a mano.</summary>
    private const int MaxUsiAmmessi = 99;

    /// <summary>Le risorse tipiche di ciascuna classe, coi nomi presi ESATTAMENTE come compaiono
    /// fra i <c>features</c> del pacchetto SRD — v. <c>Tests/ClassResourceRulesTests.cs</c> che
    /// incrocia questa mappa col pacchetto. Le classi assenti da qui non hanno risorse SRD da
    /// contare: <see cref="Suggerite"/> restituisce lista vuota, ma il pulsante «Aggiungi risorsa»
    /// resta comunque disponibile per scriverne una a mano.</summary>
    private static readonly Dictionary<string, (string Nome, string Ricarica)[]> PerClasse =
        new(StringComparer.Ordinal)
        {
            [CatalogKey.NormalizeName("Barbaro")] = new[] { ("Ira", "lungo") },
            [CatalogKey.NormalizeName("Bardo")] = new[] { ("Ispirazione bardica", "lungo") },
            [CatalogKey.NormalizeName("Druido")] = new[] { ("Forma selvatica", "breve") },
            [CatalogKey.NormalizeName("Guerriero")] = new[]
            {
                ("Secondo fiato", "breve"),
                ("Azione impetuosa", "breve"),
            },
            [CatalogKey.NormalizeName("Mago")] = new[] { ("Recupero arcano", "lungo") },
            [CatalogKey.NormalizeName("Monaco")] = new[] { ("Focus del monaco", "breve") },
            [CatalogKey.NormalizeName("Paladino")] = new[] { ("Imposizione delle mani", "lungo") },
            [CatalogKey.NormalizeName("Stregone")] = new[] { ("Stregoneria innata", "lungo") },
        };

    /// <summary>Le risorse suggerite per «Aggiungi risorsa»: nome e ricarica già compilati,
    /// <c>Max</c> a 0 perché i contatori per livello non sono nel pacchetto dati — lo scrive
    /// l'utente. Lista vuota per le classi assenti dalla tabella e per nome nullo/vuoto/ignoto.</summary>
    public static IReadOnlyList<ClassResource> Suggerite(string? nomeClasse)
    {
        if (string.IsNullOrWhiteSpace(nomeClasse)) return Array.Empty<ClassResource>();
        if (!PerClasse.TryGetValue(CatalogKey.NormalizeName(nomeClasse), out var voci))
            return Array.Empty<ClassResource>();

        return voci
            .Select(v => new ClassResource { Nome = v.Nome, Max = 0, Spesi = 0, Ricarica = v.Ricarica })
            .ToList();
    }

    /// <summary>La rete che tiene un jsonb malformato fuori dalla scheda: mai un'eccezione, mai una
    /// scheda che non si apre. Scarta le voci senza nome, tronca <c>Max</c> in [0, <see
    /// cref="MaxUsiAmmessi"/>] (un <c>Max</c> negativo si legge come 0, uno abnorme si tronca al
    /// tetto) e <c>Spesi</c> in [0, Max], riporta <c>Ricarica</c> a un valore ammesso
    /// (case-insensitive; default "lungo" se ignoto) e scarta i duplicati per nome — tenendo la
    /// prima occorrenza, col confronto normalizzato di <see cref="CatalogKey.NormalizeName"/> così
    /// "Ira" e "IRA" non sopravvivono entrambe.</summary>
    public static List<ClassResource> Normalizza(IEnumerable<ClassResource?>? risorse)
    {
        var risultato = new List<ClassResource>();
        if (risorse is null) return risultato;

        var viste = new HashSet<string>(StringComparer.Ordinal);

        foreach (var r in risorse)
        {
            if (r is null || string.IsNullOrWhiteSpace(r.Nome)) continue;

            var nome = r.Nome.Trim();
            var chiave = CatalogKey.NormalizeName(nome);
            if (!viste.Add(chiave)) continue; // duplicato: tiene la prima occorrenza

            var max = Math.Clamp(r.Max, 0, MaxUsiAmmessi);
            var spesi = Math.Clamp(r.Spesi, 0, max);
            var ricaricaGrezza = r.Ricarica?.Trim().ToLowerInvariant() ?? string.Empty;
            var ricarica = RicaricheAmmesse.Contains(ricaricaGrezza) ? ricaricaGrezza : "lungo";

            risultato.Add(new ClassResource { Nome = nome, Max = max, Spesi = spesi, Ricarica = ricarica });
        }

        return risultato;
    }

    /// <summary>Spende <paramref name="quanti"/> usi: pura, restituisce una nuova istanza con
    /// <c>Spesi</c> clampato in [0, Max] — non muta <paramref name="risorsa"/>. <paramref
    /// name="quanti"/> negativo equivale a recuperare, che è esattamente cosa fa <see
    /// cref="Recupera"/>.</summary>
    public static ClassResource Spendi(ClassResource risorsa, int quanti)
    {
        ArgumentNullException.ThrowIfNull(risorsa);
        var max = Math.Max(risorsa.Max, 0);
        var spesi = Math.Clamp(risorsa.Spesi + quanti, 0, max);
        return new ClassResource { Nome = risorsa.Nome, Max = max, Spesi = spesi, Ricarica = risorsa.Ricarica };
    }

    /// <summary>Recupera <paramref name="quanti"/> usi: pura, mai sotto 0 né sopra <c>Max</c>.</summary>
    public static ClassResource Recupera(ClassResource risorsa, int quanti) => Spendi(risorsa, -quanti);

    /// <summary>Vero se una risorsa con questa <c>Ricarica</c> si ripristina col riposo indicato:
    /// il riposo lungo ripristina "lungo" e "breve", il riposo breve solo "breve", "nessuna" non si
    /// tocca mai. Predicato puro e basta — <b>chi</b> lo applica (quali risorse di un personaggio,
    /// quando) resta in <c>RestCalculations</c>; qui c'è solo perché quella regola non deve
    /// duplicare il confronto sui tre valori di <see cref="ClassResource.Ricarica"/>.</summary>
    public static bool SiRipristinaCon(string? ricarica, bool riposoLungo)
    {
        var valore = ricarica?.Trim().ToLowerInvariant() ?? string.Empty;
        return riposoLungo ? valore is "lungo" or "breve" : valore is "breve";
    }
}
