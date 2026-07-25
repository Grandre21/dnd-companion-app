namespace DndCompanion.Services;

/// <summary>Unione fra le voci di un pacchetto e le righe di catalogo della campagna (§4.3, §6).
/// Logica pura.
///
/// Due principi, da non confondere:
/// 1. le righe di database sono dati dell'utente e restano SEMPRE tutte visibili;
/// 2. una voce di pacchetto viene oscurata se il database contiene già qualcosa che le corrisponde.
/// Il "rappresentante" non nasconde nulla: dice quale riga un import aggiorna e quale la
/// materializzazione riusa.</summary>
public static class CatalogMerge
{
    /// <summary>Gli id delle voci di pacchetto che il database già copre e che quindi non vanno
    /// mostrate.
    ///
    /// Una voce di pacchetto ha DUE chiavi — il suo id di provenienza e il suo nome — e va
    /// nascosta se il database ne contiene una qualsiasi delle due. Per questo la firma prende le
    /// voci intere e non un elenco di chiavi già calcolate: con una chiave sola il caso più
    /// frequente (una riga scritta a mano, o creata da "duplica e modifica", omonima di una voce
    /// di pacchetto) sfuggirebbe, e l'utente vedrebbe due volte la stessa cosa.</summary>
    public static HashSet<string> HiddenPackageIds<TPkg, TRow>(
        IEnumerable<TPkg> packageEntries,
        Func<TPkg, string> packageIdOf,
        Func<TPkg, string> packageNameOf,
        IEnumerable<TRow> dbRows,
        Func<TRow, string?> sourceIdOf,
        Func<TRow, string> nameOf)
    {
        var dbKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in dbRows)
        {
            dbKeys.Add(CatalogKey.For(sourceIdOf(row), nameOf(row)));
            // Una riga con provenienza copre anche la voce omonima: chi ha importato "Elfo" non
            // deve vederselo ricomparire perché il pacchetto lo identifica per nome.
            dbKeys.Add(CatalogKey.NormalizeName(nameOf(row)));
        }

        var hidden = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in packageEntries)
        {
            var id = packageIdOf(entry);
            if (dbKeys.Contains(id) || dbKeys.Contains(CatalogKey.NormalizeName(packageNameOf(entry))))
                hidden.Add(id);
        }
        return hidden;
    }

    /// <summary>Fra più righe con la stessa chiave, quella che le rappresenta: prima la riga
    /// senza provenienza (è una voce propria dell'utente, la più specifica), poi l'id
    /// ordinalmente minore — arbitrario, ma deterministico su tutti i cataloghi, perché
    /// `spells` e `monsters` non hanno `created_at`.
    ///
    /// NOTA: in Fase 1 non ha ancora chiamanti — serve a PackageImportPlan e alla
    /// materializzazione, entrambi di Fase 2. Nasce qui perché applica la stessa regola di
    /// precedenza di HiddenPackageIds e conviene fissarla e testarla in un colpo solo.</summary>
    public static T? Representative<T>(
        IEnumerable<T> rows,
        Func<T, string?> sourceIdOf,
        Func<T, string> idOf) where T : class
        => rows
            .OrderBy(r => string.IsNullOrWhiteSpace(sourceIdOf(r)) ? 0 : 1)
            .ThenBy(idOf, StringComparer.Ordinal)
            .FirstOrDefault();
}
