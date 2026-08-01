using DndCompanion.Models;
using DndCompanion.Models.Packages;

namespace DndCompanion.Services;

/// <summary>Una voce scegliibile nel tracker, da qualunque sorgente arrivi.</summary>
/// <param name="Chiave">Identificatore stabile e univoco fra le due sorgenti, usato come chiave
/// delle quantità: gli uuid del database e gli id del pacchetto vivono in spazi diversi, e senza
/// prefisso una collisione sarebbe silenziosa.</param>
/// <param name="PfTesto">I punti ferita come li scrive il manuale («19 (3d10 + 3)»): la
/// conversione a intero resta a <see cref="CombatImport.ParseLeadingHp"/>, che è già la sola
/// regola del progetto per farlo.</param>
public sealed record MonsterChoice(string Chiave, string Nome, string? PfTesto, bool DalManuale);

/// <summary>L'esito della scelta: le voci da mostrare e quante il tetto ne ha lasciate fuori. Le
/// due cose viaggiano insieme perché la seconda si legge solo accanto alla prima — «altri 12
/// corrispondono» non significa niente senza l'elenco che li ha esclusi.</summary>
public sealed record MonsterChoices(IReadOnlyList<MonsterChoice> Voci, int Troncate);

/// <summary>Scelta dei mostri da mettere nel tracker iniziativa, unendo le righe di campagna e le
/// voci del manuale. Logica pura.
///
/// Il tracker leggeva solo il database, per cui i 331 mostri del pacchetto non erano utilizzabili
/// al tavolo. Metterli tutti in un elenco non basta: sono troppi da scorrere su un telefono e
/// renderebbero altrettanti stepper, quindi la ricerca fa parte del meccanismo, non è un
/// ornamento.</summary>
public static class MonsterPicker
{
    public const string PrefissoDb = "db:";
    public const string PrefissoManuale = "pkg:";

    /// <summary>Le voci da mostrare, già filtrate e ordinate.
    ///
    /// A ricerca vuota compaiono le sole righe di campagna: sono quelle che il master ha preparato
    /// per il suo tavolo, ed è il comportamento che il tracker ha sempre avuto. Il manuale entra
    /// quando lo si cerca — 331 voci a schermo senza averle chieste sarebbero rumore.</summary>
    /// <param name="limite">Quante mostrarne al massimo; le altre le conta <c>Troncate</c>.</param>
    public static MonsterChoices Scegli(
        IEnumerable<Monster>? righeDiCampagna,
        IEnumerable<PackageMonster>? vociDiManuale,
        string? ricerca,
        int limite)
    {
        var daDb = (righeDiCampagna ?? Enumerable.Empty<Monster>())
            .Select(m => new MonsterChoice(PrefissoDb + m.Id, m.Name, m.HitPoints, false));

        IEnumerable<MonsterChoice> tutte;
        if (string.IsNullOrWhiteSpace(ricerca))
        {
            tutte = daDb;
        }
        else
        {
            var chiave = CatalogKey.NormalizeName(ricerca);
            var daManuale = (vociDiManuale ?? Enumerable.Empty<PackageMonster>())
                .Select(p => new MonsterChoice(PrefissoManuale + p.Id, p.Name, p.HitPoints, true));

            tutte = daDb.Concat(daManuale)
                .Where(v => CatalogKey.NormalizeName(v.Nome).Contains(chiave, StringComparison.Ordinal));
        }

        var ordinate = tutte
            // Le voci di campagna prima di quelle di manuale: a parità di nome, quella del tavolo
            // è la versione che il master ha ritoccato.
            .OrderBy(v => v.DalManuale ? 1 : 0)
            .ThenBy(v => v.Nome, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Il limite vale solo sulla ricerca. A ricerca vuota si vedono le sole righe di campagna —
        // quelle che il master ha preparato per il suo tavolo — e troncarle sarebbe una perdita
        // secca rispetto a prima, quando l'elenco le mostrava tutte.
        if (string.IsNullOrWhiteSpace(ricerca)) return new MonsterChoices(ordinate, 0);

        var tetto = Math.Max(0, limite);
        return new MonsterChoices(
            ordinate.Take(tetto).ToList(),
            Math.Max(0, ordinate.Count - tetto));
    }

    /// <summary>Vero se la chiave indica una voce di manuale.</summary>
    public static bool DalManuale(string? chiave)
        => chiave is not null && chiave.StartsWith(PrefissoManuale, StringComparison.Ordinal);

    /// <summary>L'identificatore senza prefisso, per risalire alla riga o alla voce.</summary>
    public static string IdSenzaPrefisso(string chiave)
    {
        if (chiave.StartsWith(PrefissoManuale, StringComparison.Ordinal))
            return chiave[PrefissoManuale.Length..];
        if (chiave.StartsWith(PrefissoDb, StringComparison.Ordinal))
            return chiave[PrefissoDb.Length..];
        return chiave;
    }
}
