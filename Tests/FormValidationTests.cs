using DndCompanion.Models;
using DndCompanion.Services;
using Xunit;

namespace DndCompanion.Tests;

// Validazione di dominio lato client per i form (FormValidation): ritorna il primo errore o null.
public class FormValidationTests
{
    private static Monster ValidMonster() => new()
    {
        Name = "Goblin", ArmorClass = 15,
        Strength = 8, Dexterity = 14, Constitution = 10,
        Intelligence = 10, Wisdom = 8, Charisma = 8
    };

    private static Race ValidRace() => new() { Name = "Elfo", Speed = 30 };

    private static Background ValidBackground() => new() { Name = "Soldato" };

    [Fact]
    public void Valid_monster_returns_null() => Assert.Null(FormValidation.ValidateMonster(ValidMonster()));

    [Fact]
    public void Monster_blank_name_is_rejected()
    {
        var m = ValidMonster(); m.Name = "  ";
        Assert.Equal("Il nome è obbligatorio", FormValidation.ValidateMonster(m));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(41)]
    public void Monster_armor_class_out_of_range_is_rejected(int ac)
    {
        var m = ValidMonster(); m.ArmorClass = ac;
        Assert.Equal("La CA deve essere tra 0 e 40", FormValidation.ValidateMonster(m));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(31)]
    public void Monster_ability_score_out_of_range_is_rejected(int score)
    {
        var m = ValidMonster(); m.Strength = score;
        Assert.Equal("Forza: il punteggio deve essere tra 1 e 30", FormValidation.ValidateMonster(m));
    }

    // Ogni caratteristica fuori range riporta il PROPRIO label (ordine Str,Des,Cos,Int,Sag,Car).
    [Theory]
    [InlineData(0, "Forza")]
    [InlineData(1, "Destrezza")]
    [InlineData(2, "Costituzione")]
    [InlineData(3, "Intelligenza")]
    [InlineData(4, "Saggezza")]
    [InlineData(5, "Carisma")]
    public void Monster_each_ability_out_of_range_reports_its_label(int index, string label)
    {
        var m = ValidMonster();
        switch (index)
        {
            case 0: m.Strength = 0; break;
            case 1: m.Dexterity = 0; break;
            case 2: m.Constitution = 0; break;
            case 3: m.Intelligence = 0; break;
            case 4: m.Wisdom = 0; break;
            case 5: m.Charisma = 0; break;
        }
        Assert.Equal($"{label}: il punteggio deve essere tra 1 e 30", FormValidation.ValidateMonster(m));
    }

    [Fact]
    public void Valid_race_returns_null() => Assert.Null(FormValidation.ValidateRace(ValidRace()));

    [Fact]
    public void Race_blank_name_is_rejected()
    {
        var r = ValidRace(); r.Name = "";
        Assert.Equal("Il nome è obbligatorio", FormValidation.ValidateRace(r));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(121)]
    public void Race_speed_out_of_range_is_rejected(int speed)
    {
        var r = ValidRace(); r.Speed = speed;
        Assert.Equal("La velocità deve essere tra 0 e 120", FormValidation.ValidateRace(r));
    }

    [Fact]
    public void InRange_is_inclusive()
    {
        Assert.True(FormValidation.InRange(0, 0, 40));
        Assert.True(FormValidation.InRange(40, 0, 40));
        Assert.False(FormValidation.InRange(41, 0, 40));
    }

    [Fact]
    public void Valid_background_returns_null() => Assert.Null(FormValidation.ValidateBackground(ValidBackground()));

    [Fact]
    public void Background_blank_name_is_rejected()
    {
        var b = ValidBackground(); b.Name = "  ";
        Assert.Equal("Il nome è obbligatorio", FormValidation.ValidateBackground(b));
    }
}
