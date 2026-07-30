using DndCompanion.Services;
using Xunit;

namespace DndCompanion.Tests;

// Voce attiva della barra di navigazione (BottomNavRoutes.IsActive): confronto fra il percorso
// corrente relativo al <base href> e la rotta della voce.
public class BottomNavRoutesTests
{
    [Fact]
    public void Empty_path_is_home()
        => Assert.True(BottomNavRoutes.IsActive("", ""));

    [Fact]
    public void Empty_path_is_not_another_section()
        => Assert.False(BottomNavRoutes.IsActive("", "characters"));

    [Fact]
    public void Matching_route_is_active()
        => Assert.True(BottomNavRoutes.IsActive("spells", "spells"));

    [Fact]
    public void Different_route_is_not_active()
        => Assert.False(BottomNavRoutes.IsActive("spells", "notes"));

    // La query string non deve far perdere la sezione: "spells?q=fuoco" resta Incantesimi.
    [Fact]
    public void Query_string_is_ignored()
        => Assert.True(BottomNavRoutes.IsActive("spells?q=fuoco", "spells"));

    [Fact]
    public void Fragment_is_ignored()
        => Assert.True(BottomNavRoutes.IsActive("spells#dettaglio", "spells"));

    [Fact]
    public void Query_string_before_fragment_is_ignored()
        => Assert.True(BottomNavRoutes.IsActive("notes?filtro=miei#nota-3", "notes"));

    // Con lo slash finale (o iniziale) è sempre la stessa sezione.
    [Fact]
    public void Trailing_slash_is_ignored()
        => Assert.True(BottomNavRoutes.IsActive("combat/", "combat"));

    [Fact]
    public void Leading_slash_is_ignored()
        => Assert.True(BottomNavRoutes.IsActive("/combat", "combat"));

    // Percorso vuoto con la sola query (Home con parametri, es. il ritorno OAuth).
    [Fact]
    public void Home_with_query_is_still_home()
        => Assert.True(BottomNavRoutes.IsActive("?code=abc", ""));

    [Fact]
    public void Comparison_is_case_insensitive()
        => Assert.True(BottomNavRoutes.IsActive("Characters", "characters"));

    // Una sotto-rotta non è la sezione: se un domani esistesse "spells/123" non deve
    // accendere la voce "spells" per sbaglio (il confronto è sull'intero percorso).
    [Fact]
    public void Sub_route_is_not_the_section()
        => Assert.False(BottomNavRoutes.IsActive("spells/123", "spells"));

    [Fact]
    public void Null_path_is_treated_as_home()
        => Assert.True(BottomNavRoutes.IsActive(null, ""));
}
