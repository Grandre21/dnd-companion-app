using DndCompanion.Services;
using Xunit;

namespace DndCompanion.Tests;

// Logica pura del catalogo mostri (MonsterCatalog): grado sfida come numero ordinabile.
public class MonsterCatalogTests
{
    [Theory]
    // Frazioni canoniche 5e (non parsabili come double)
    [InlineData("0", 0d)]
    [InlineData("1/8", 0.125d)]
    [InlineData("1/4", 0.25d)]
    [InlineData("1/2", 0.5d)]
    // Interi e decimali (cultura invariante)
    [InlineData("1", 1d)]
    [InlineData("5", 5d)]
    [InlineData("30", 30d)]
    [InlineData("0.5", 0.5d)]
    [InlineData("1.5", 1.5d)]
    // Spazi attorno: tollerati sia dallo switch (Trim) sia da TryParse
    [InlineData("  1/4  ", 0.25d)]
    [InlineData("  7  ", 7d)]
    // Sentinella "ignoto" = -1
    [InlineData("", -1d)]
    [InlineData("   ", -1d)]
    [InlineData(null, -1d)]
    [InlineData("n/a", -1d)]
    [InlineData("1/3", -1d)] // frazione non canonica: non gestita
    public void ParseChallengeRating_mappa_il_grado_sfida(string? cr, double expected)
        => Assert.Equal(expected, MonsterCatalog.ParseChallengeRating(cr));

    [Fact]
    public void ParseChallengeRating_ordina_le_frazioni_prima_degli_interi()
    {
        var crs = new[] { "2", "1/4", "0", "1/8", "1", "1/2" };
        var ordered = crs.OrderBy(MonsterCatalog.ParseChallengeRating).ToArray();
        Assert.Equal(new[] { "0", "1/8", "1/4", "1/2", "1", "2" }, ordered);
    }

    [Fact]
    public void ParseChallengeRating_i_valori_ignoti_precedono_tutti_nell_ordinamento()
    {
        // -1 è la sentinella: i CR ignoti finiscono in testa (comportamento storico del catalogo).
        var crs = new[] { "1", "boh", "0" };
        var ordered = crs.OrderBy(MonsterCatalog.ParseChallengeRating).ToArray();
        Assert.Equal(new[] { "boh", "0", "1" }, ordered);
    }
}
