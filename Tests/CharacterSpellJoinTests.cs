using DndCompanion.Models;
using DndCompanion.Services;
using Xunit;

namespace DndCompanion.Tests;

// JOIN degli incantesimi noti del PG col catalogo globale (CharacterSpellJoin.WithCatalog):
// match per SpellId, scarto degli orfani (spell rimosso dal catalogo), ordine preservato.
public class CharacterSpellJoinTests
{
    private static Spell S(string id, string name = "Spell", int level = 1) => new()
    {
        Id = id,
        Name = name,
        Level = level,
    };

    private static CharacterSpell CS(string spellId, string? id = null) => new()
    {
        Id = id ?? $"cs-{spellId}",
        CharacterId = "char-1",
        SpellId = spellId,
    };

    [Fact]
    public void WithCatalog_joins_entries_to_matching_spells()
    {
        var catalog = new[] { S("a", "Dardo"), S("b", "Scudo") };
        var entries = new[] { CS("a"), CS("b") };

        var views = CharacterSpellJoin.WithCatalog(entries, catalog);

        Assert.Equal(2, views.Count);
        Assert.Equal("Dardo", views[0].Spell.Name);
        Assert.Equal("Scudo", views[1].Spell.Name);
    }

    [Fact]
    public void WithCatalog_drops_orphans_not_in_catalog()
    {
        var catalog = new[] { S("a") };
        var entries = new[] { CS("a"), CS("ghost") }; // "ghost" non è in catalogo

        var views = CharacterSpellJoin.WithCatalog(entries, catalog);

        Assert.Single(views);
        Assert.Equal("a", views[0].Entry.SpellId);
    }

    [Fact]
    public void WithCatalog_returns_empty_when_catalog_empty()
        => Assert.Empty(CharacterSpellJoin.WithCatalog(new[] { CS("a") }, Array.Empty<Spell>()));

    [Fact]
    public void WithCatalog_returns_empty_when_no_entries()
        => Assert.Empty(CharacterSpellJoin.WithCatalog(Array.Empty<CharacterSpell>(), new[] { S("a") }));

    [Fact]
    public void WithCatalog_preserves_entry_order()
    {
        var catalog = new[] { S("a"), S("b"), S("c") };
        var entries = new[] { CS("c"), CS("a"), CS("b") };

        var ids = CharacterSpellJoin.WithCatalog(entries, catalog).Select(v => v.Entry.SpellId).ToList();

        Assert.Equal(new[] { "c", "a", "b" }, ids);
    }

    [Fact]
    public void WithCatalog_pairs_each_entry_with_its_own_spell()
    {
        var catalog = new[] { S("a", "Dardo"), S("b", "Scudo") };
        var entries = new[] { CS("b", id: "entry-x") };

        var view = Assert.Single(CharacterSpellJoin.WithCatalog(entries, catalog));

        Assert.Equal("entry-x", view.Entry.Id);
        Assert.Equal("b", view.Spell.Id);
        Assert.Equal("Scudo", view.Spell.Name);
    }
}
