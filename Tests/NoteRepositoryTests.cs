using DndCompanion.Models;
using DndCompanion.Services.Repositories;
using Xunit;

namespace DndCompanion.Tests;

// Logica pura di visibilità/ordinamento note estratta da NoteRepository (FilterAndSortVisible).
// È una regola di SICUREZZA: un utente vede solo le note condivise + le proprie private.
public class NoteRepositoryTests
{
    private const string Me = "user-me";
    private const string Other = "user-other";

    private static Note N(string id, string owner, bool shared,
        DateTime? updated = null, DateTime? created = null) => new()
    {
        Id = id,
        OwnerId = owner,
        CampaignId = "camp",
        Title = id,
        IsShared = shared,
        UpdatedAt = updated,
        CreatedAt = created,
    };

    [Fact]
    public void FilterAndSortVisible_includes_shared_and_own_excludes_others_private()
    {
        var notes = new[]
        {
            N("own-private", Me, shared: false),
            N("other-shared", Other, shared: true),
            N("other-private", Other, shared: false),
            N("own-shared", Me, shared: true),
        };

        var visible = NoteRepository.FilterAndSortVisible(notes, Me)
            .Select(n => n.Id)
            .ToHashSet();

        Assert.Contains("own-private", visible);
        Assert.Contains("other-shared", visible);
        Assert.Contains("own-shared", visible);
        Assert.DoesNotContain("other-private", visible); // la nota privata altrui NON è visibile
    }

    [Fact]
    public void FilterAndSortVisible_orders_by_updated_desc()
    {
        var notes = new[]
        {
            N("oldest", Me, false, updated: new DateTime(2026, 1, 1)),
            N("newest", Me, false, updated: new DateTime(2026, 1, 10)),
            N("middle", Me, false, updated: new DateTime(2026, 1, 5)),
        };

        var order = NoteRepository.FilterAndSortVisible(notes, Me).Select(n => n.Id).ToList();

        Assert.Equal(new[] { "newest", "middle", "oldest" }, order);
    }

    [Fact]
    public void FilterAndSortVisible_falls_back_to_created_when_updated_null()
    {
        var notes = new[]
        {
            N("has-updated", Me, false, updated: new DateTime(2026, 1, 2)),
            N("only-created", Me, false, updated: null, created: new DateTime(2026, 1, 9)),
        };

        var order = NoteRepository.FilterAndSortVisible(notes, Me).Select(n => n.Id).ToList();

        // only-created (creata il 09) è più recente di has-updated (modificata il 02).
        Assert.Equal(new[] { "only-created", "has-updated" }, order);
    }

    [Fact]
    public void FilterAndSortVisible_treats_missing_dates_as_oldest()
    {
        var notes = new[]
        {
            N("dated", Me, false, updated: new DateTime(2026, 1, 1)),
            N("undated", Me, false),
        };

        var order = NoteRepository.FilterAndSortVisible(notes, Me).Select(n => n.Id).ToList();

        Assert.Equal(new[] { "dated", "undated" }, order);
    }
}
