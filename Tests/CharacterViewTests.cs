using DndCompanion.Models;
using DndCompanion.Shared.CharacterTabs;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace DndCompanion.Tests;

// Helper puri di formattazione/a11y e mapping degli slot incantesimo (CharacterView).
// Gli slot usano valori distinti per livello: un case mal-cablato (es. case 7 → slot 8) rompe il test.
public class CharacterViewTests
{
    // ===== Formattazione / accessibilità =====

    [Theory]
    [InlineData(0, "+0")]   // lo zero mostra comunque il segno +
    [InlineData(3, "+3")]
    [InlineData(10, "+10")]
    [InlineData(-1, "-1")]
    [InlineData(-10, "-10")]
    public void FormatBonus_prefixes_sign_including_zero(int value, string expected)
        => Assert.Equal(expected, CharacterView.FormatBonus(value));

    [Theory]
    [InlineData(true, "true")]
    [InlineData(false, "false")]
    public void AriaBool_maps_to_lowercase_string(bool value, string expected)
        => Assert.Equal(expected, CharacterView.AriaBool(value));

    [Theory]
    [InlineData("Enter")]
    [InlineData(" ")] // la chiave dello spazio è " ", non "Space"/"Spacebar"
    public async Task OnKey_invokes_action_on_enter_or_space(string key)
    {
        var invoked = false;
        await CharacterView.OnKey(new KeyboardEventArgs { Key = key }, () => { invoked = true; return Task.CompletedTask; });
        Assert.True(invoked);
    }

    [Theory]
    [InlineData("a")]
    [InlineData("Spacebar")]
    [InlineData("Escape")]
    [InlineData("")]
    public async Task OnKey_ignores_other_keys(string key)
    {
        var invoked = false;
        await CharacterView.OnKey(new KeyboardEventArgs { Key = key }, () => { invoked = true; return Task.CompletedTask; });
        Assert.False(invoked);
    }

    // ===== Mapping slot incantesimo (livelli 1-9) =====

    [Fact]
    public void GetSpellSlotMax_reads_the_matching_level_property()
    {
        var c = new Character
        {
            SpellSlots1Max = 11, SpellSlots2Max = 12, SpellSlots3Max = 13,
            SpellSlots4Max = 14, SpellSlots5Max = 15, SpellSlots6Max = 16,
            SpellSlots7Max = 17, SpellSlots8Max = 18, SpellSlots9Max = 19,
        };
        for (int level = 1; level <= 9; level++)
            Assert.Equal(10 + level, CharacterView.GetSpellSlotMax(c, level));
    }

    [Fact]
    public void GetSpellSlotUsed_reads_the_matching_level_property()
    {
        var c = new Character
        {
            SpellSlots1Used = 21, SpellSlots2Used = 22, SpellSlots3Used = 23,
            SpellSlots4Used = 24, SpellSlots5Used = 25, SpellSlots6Used = 26,
            SpellSlots7Used = 27, SpellSlots8Used = 28, SpellSlots9Used = 29,
        };
        for (int level = 1; level <= 9; level++)
            Assert.Equal(20 + level, CharacterView.GetSpellSlotUsed(c, level));
    }

    [Fact]
    public void SetSpellSlotUsed_writes_only_the_matching_level()
    {
        // Ogni livello su un Character NUOVO: così una scrittura spuria verso un altro slot
        // (di indice più basso o più alto) non viene mascherata dall'iterazione successiva.
        for (int level = 1; level <= 9; level++)
        {
            var c = new Character();
            CharacterView.SetSpellSlotUsed(c, level, 42);

            for (int other = 1; other <= 9; other++)
                Assert.Equal(other == level ? 42 : 0, CharacterView.GetSpellSlotUsed(c, other));
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    [InlineData(-1)]
    public void GetSpellSlot_out_of_range_returns_zero(int level)
    {
        var c = new Character { SpellSlots1Max = 5, SpellSlots1Used = 3 };
        Assert.Equal(0, CharacterView.GetSpellSlotMax(c, level));
        Assert.Equal(0, CharacterView.GetSpellSlotUsed(c, level));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    [InlineData(-1)]
    public void SetSpellSlotUsed_out_of_range_is_noop(int level)
    {
        var c = new Character();
        CharacterView.SetSpellSlotUsed(c, level, 99); // nessuna eccezione
        for (int lvl = 1; lvl <= 9; lvl++)
            Assert.Equal(0, CharacterView.GetSpellSlotUsed(c, lvl)); // nessuno slot valido toccato
    }
}
