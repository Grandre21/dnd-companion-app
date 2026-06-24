using DndCompanion.Models;
using DndCompanion.Services;
using Xunit;

namespace DndCompanion.Tests;

// Normalizzazione del draft PG prima del salvataggio (CharacterNormalizer.Normalize):
// trim dei testi (vuoti -> null per i nullable), default di Size, clamp dei numerici.
public class CharacterNormalizerTests
{
    [Fact]
    public void Normalize_trims_required_text_fields()
    {
        var c = new Character { Name = "  Gandalf  ", Class = " Mago ", Race = "  Umano " };
        CharacterNormalizer.Normalize(c);
        Assert.Equal("Gandalf", c.Name);
        Assert.Equal("Mago", c.Class);
        Assert.Equal("Umano", c.Race);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalize_defaults_blank_size_to_media(string? size)
    {
        var c = new Character { Name = "X", Size = size! };
        CharacterNormalizer.Normalize(c);
        Assert.Equal("Media", c.Size);
    }

    [Fact]
    public void Normalize_trims_size_when_present()
    {
        var c = new Character { Name = "X", Size = "  Grande  " };
        CharacterNormalizer.Normalize(c);
        Assert.Equal("Grande", c.Size);
    }

    [Fact]
    public void Normalize_converts_blank_optional_fields_to_null_and_trims_the_rest()
    {
        var c = new Character { Name = "X", Subclass = "   ", Background = "", Notes = "  Ciao  " };
        CharacterNormalizer.Normalize(c);
        Assert.Null(c.Subclass);
        Assert.Null(c.Background);
        Assert.Equal("Ciao", c.Notes);
    }

    [Theory]
    [InlineData(0, 1)]    // clamp verso il minimo
    [InlineData(-5, 1)]
    [InlineData(1, 1)]
    [InlineData(5, 5)]
    [InlineData(20, 20)]
    [InlineData(25, 20)]  // clamp verso il massimo
    public void Normalize_clamps_level_between_1_and_20(int level, int expected)
    {
        var c = new Character { Name = "X", Level = level };
        CharacterNormalizer.Normalize(c);
        Assert.Equal(expected, c.Level);
    }

    [Fact]
    public void Normalize_floors_negative_numerics_to_zero()
    {
        var c = new Character
        {
            Name = "X",
            ArmorClass = -3,
            HitPoints = -10,
            TempHitPoints = -1,
            Speed = -9,
            HitDiceSpent = -2,
            ExperiencePoints = -100,
            CopperPieces = -1,
            SilverPieces = -1,
            ElectrumPieces = -1,
            GoldPieces = -1,
            PlatinumPieces = -1,
        };
        CharacterNormalizer.Normalize(c);
        Assert.Equal(0, c.ArmorClass);
        Assert.Equal(0, c.HitPoints);
        Assert.Equal(0, c.TempHitPoints);
        Assert.Equal(0, c.Speed);
        Assert.Equal(0, c.HitDiceSpent);
        Assert.Equal(0, c.ExperiencePoints);
        Assert.Equal(0, c.CopperPieces);
        Assert.Equal(0, c.SilverPieces);
        Assert.Equal(0, c.ElectrumPieces);
        Assert.Equal(0, c.GoldPieces);
        Assert.Equal(0, c.PlatinumPieces);
    }

    [Fact]
    public void Normalize_preserves_valid_values()
    {
        var c = new Character
        {
            Name = "Aragorn", Level = 10, ArmorClass = 18, HitPoints = 84,
            Speed = 9, GoldPieces = 250,
        };
        CharacterNormalizer.Normalize(c);
        Assert.Equal("Aragorn", c.Name);
        Assert.Equal(10, c.Level);
        Assert.Equal(18, c.ArmorClass);
        Assert.Equal(84, c.HitPoints);
        Assert.Equal(9, c.Speed);
        Assert.Equal(250, c.GoldPieces);
    }
}
