using DndCompanion.Models;
using DndCompanion.Models.Packages;

namespace DndCompanion.Services;

/// <summary>Esito della decisione: o una riga già presente da riusare, o una riga nuova da
/// inserire. Mai entrambe, mai nessuna.</summary>
public sealed record SpellMaterializationResult(Spell? Existing, Spell? ToInsert);

/// <summary>Un incantesimo che vive solo nel file non può essere aggiunto alla lista di un PG:
/// `character_spells.spell_id` è una chiave esterna verso `spells(id)` (§4.1). Prima di creare il
/// legame, la voce di pacchetto va materializzata — ma solo se non c'è già (§4.4).
/// Logica pura: decide, non scrive.</summary>
public static class SpellMaterialization
{
    public static SpellMaterializationResult Resolve(
        PackageSpell packageSpell,
        IEnumerable<Spell> campaignSpells,
        string campaignId,
        string? userId)
    {
        // Il filtro sulla campagna non è ridondante: la lista arriva da una pagina che potrebbe
        // averla caricata per un'altra campagna, e riusare l'uuid di una riga che sta altrove
        // creerebbe un legame che la chiave esterna di QUESTA campagna non regge.
        var candidate = campaignSpells
            .Where(s => s.CampaignId == campaignId)
            .Where(s => CatalogKey.For(s.SourceId, s.Name) == packageSpell.Id
                        || CatalogKey.NormalizeName(s.Name) == CatalogKey.NormalizeName(packageSpell.Name))
            .ToList();

        if (candidate.Count > 0)
        {
            var scelta = CatalogMerge.Representative(candidate, s => s.SourceId, s => s.Id)!;
            return new SpellMaterializationResult(scelta, null);
        }

        return new SpellMaterializationResult(null, new Spell
        {
            Name = packageSpell.Name,
            // `?? 0`: PackageSpell.Level è int? (Task 2), perché il parser accetta voci minimali e
            // con `int` un livello assente sarebbe indistinguibile da un trucchetto.
            Level = packageSpell.Level ?? 0,
            School = packageSpell.School,
            CastingTime = packageSpell.CastingTime,
            Range = packageSpell.Range,
            Components = packageSpell.Components,
            Duration = packageSpell.Duration,
            Description = packageSpell.Description,
            // La colonna è testo libero, la voce di pacchetto è una lista: si uniscono con lo
            // stesso separatore che SpellClassNames sa poi spezzare.
            Classes = string.Join(", ", packageSpell.Classes),
            SourceId = packageSpell.Id,
            CampaignId = campaignId,
            AddedBy = userId,
        });
    }
}
