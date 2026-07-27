using DndCompanion.Services;

namespace DndCompanion.Tests;

public class SpellClassNamesTests
{
    [Theory]
    [InlineData("Wizard, Sorcerer", "Mago", true)]
    [InlineData("Mago, Stregone", "Mago", true)]
    [InlineData("Wizard", "Stregone", false)]
    [InlineData("Chierico", "Chierico", true)]
    [InlineData("Cleric", "Chierico", true)]
    [InlineData("", "Mago", false)]
    [InlineData(null, "Mago", false)]
    public void Matches_RiconosceEntrambeLeLingue(string? campo, string classeItaliana, bool atteso)
        => Assert.Equal(atteso, SpellClassNames.Matches(campo, classeItaliana));

    // Il campo è testo libero digitato a mano: spazi, maiuscole e separatori variano.
    [Theory]
    [InlineData("  wizard ,  bard  ")]
    [InlineData("Wizard;Bard")]
    [InlineData("Wizard/Bard")]
    public void Matches_TolleraSpaziMaiuscoleESeparatoriDiversi(string campo)
        => Assert.True(SpellClassNames.Matches(campo, "Mago"));

    // Il confronto è per TOKEN, non per sottostringa: "Bardo" non deve farsi trovare
    // da chi cerca una classe il cui nome ne è un prefisso, e viceversa.
    [Fact]
    public void Matches_ConfrontoPerTokenNonPerSottostringa()
    {
        Assert.False(SpellClassNames.Matches("Bardolino", "Bardo"));
        Assert.True(SpellClassNames.Matches("Bardo", "Bardo"));
    }

    [Fact]
    public void Matches_ClasseSconosciuta_RestituisceFalso()
        => Assert.False(SpellClassNames.Matches("Mago", "Artefice"));

    [Fact]
    public void Pairs_ContieneLeOttoClassiIncantatrici()
    {
        Assert.Equal(8, SpellClassNames.Pairs.Count);
        Assert.Contains(SpellClassNames.Pairs, p => p.Italian == "Mago" && p.English == "Wizard");
    }
}
