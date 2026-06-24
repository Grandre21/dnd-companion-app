using DndCompanion.Models;

namespace DndCompanion.Services;

/// <summary>Vista per la UI: un incantesimo noto del PG (<see cref="Entry"/>) unito ai dati di catalogo (<see cref="Spell"/>).</summary>
internal record CharacterSpellView(CharacterSpell Entry, Spell Spell);

/// <summary>
/// JOIN logico in memoria degli incantesimi noti del PG col catalogo globale. Estratto da
/// CharacterMagicTab per essere testabile. Pura: nessuno stato/I/O.
/// </summary>
internal static class CharacterSpellJoin
{
    /// <summary>
    /// Unisce ogni <see cref="CharacterSpell"/> al relativo <see cref="Spell"/> di catalogo (match per
    /// <c>SpellId</c>); <b>scarta gli orfani</b> (incantesimo rimosso dal catalogo globale). Preserva
    /// l'ordine degli entry in ingresso.
    /// </summary>
    public static List<CharacterSpellView> WithCatalog(
        IEnumerable<CharacterSpell> entries, IReadOnlyList<Spell> catalog)
    {
        var views = new List<CharacterSpellView>();
        foreach (var cs in entries)
        {
            var spell = catalog.FirstOrDefault(s => s.Id == cs.SpellId);
            if (spell is null) continue; // orfano: lo scartiamo
            views.Add(new CharacterSpellView(cs, spell));
        }
        return views;
    }
}
