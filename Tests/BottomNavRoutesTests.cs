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

    // COMPORTAMENTO CAMBIATO il 2026-07-31. Prima una sotto-rotta NON accendeva la sezione
    // ("il confronto è sull'intero percorso"): finché non esistevano sotto-rotte era una scelta
    // prudente. Ora ne esiste una vera — `characters/nuovo`, il wizard di creazione diventato
    // pagina — e con la vecchia regola la barra restava senza alcuna voce attiva per tutta la
    // creazione del personaggio. Un sottopercorso appartiene alla sua sezione.
    [Fact]
    public void Sub_route_belongs_to_its_section()
    {
        Assert.True(BottomNavRoutes.IsActive("characters/nuovo", "characters"));
        Assert.True(BottomNavRoutes.IsActive("spells/123", "spells"));
    }

    // Il confronto resta però sul SEGMENTO intero: "charactersXYZ" non è dentro "characters".
    [Fact]
    public void Prefix_without_separator_is_not_the_section()
    {
        Assert.False(BottomNavRoutes.IsActive("charactersXYZ", "characters"));
        Assert.False(BottomNavRoutes.IsActive("party-old", "party"));
    }

    // La Home ha rotta vuota, che sarebbe prefisso di qualunque percorso: non deve restare
    // accesa ovunque, altrimenti la barra mostrerebbe sempre due voci attive.
    [Fact]
    public void Home_does_not_match_every_route()
    {
        Assert.False(BottomNavRoutes.IsActive("characters", ""));
        Assert.False(BottomNavRoutes.IsActive("characters/nuovo", ""));
    }

    [Fact]
    public void Null_path_is_treated_as_home()
        => Assert.True(BottomNavRoutes.IsActive(null, ""));
}
