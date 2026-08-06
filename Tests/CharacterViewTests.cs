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

    // ===== Danno e cura (tastierino dei PF) =====

    [Theory]
    [InlineData(10, 5, 20, 3, 10, 2)]   // danno sotto il cuscinetto: i PF veri non si toccano
    [InlineData(10, 5, 20, 5, 10, 0)]   // danno esatto al cuscinetto: azzerato, PF veri intatti
    [InlineData(10, 5, 20, 8, 7, 0)]    // danno oltre il cuscinetto: l'eccedenza scala i PF veri
    [InlineData(10, 5, 20, 27, 0, 0)]   // colpo enorme (il caso che si sbaglia a mano): pavimento zero, non un negativo
    [InlineData(10, 0, 20, 4, 6, 0)]    // senza cuscinetto, il danno va tutto sui PF veri
    public void ApplyDamage_scala_prima_il_cuscinetto_temporaneo(
        int hp, int temp, int max, int danno, int hpAtteso, int tempAtteso)
    {
        var c = new Character { HitPoints = hp, TempHitPoints = temp, MaxHitPoints = max };
        CharacterView.ApplyDamage(c, danno);
        Assert.Equal(hpAtteso, c.HitPoints);
        Assert.Equal(tempAtteso, c.TempHitPoints);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void ApplyDamage_con_valore_non_positivo_non_fa_nulla(int danno)
    {
        var c = new Character { HitPoints = 10, TempHitPoints = 3, MaxHitPoints = 20 };
        CharacterView.ApplyDamage(c, danno);
        Assert.Equal(10, c.HitPoints);
        Assert.Equal(3, c.TempHitPoints);
    }

    [Fact]
    public void ApplyDamage_non_tocca_i_tiri_salvezza_contro_morte()
    {
        var c = new Character
        {
            HitPoints = 5, MaxHitPoints = 20,
            DeathSaveSuccesses = 1, DeathSaveFailures = 2,
        };
        CharacterView.ApplyDamage(c, 100);
        Assert.Equal(0, c.HitPoints);
        Assert.Equal(1, c.DeathSaveSuccesses);
        Assert.Equal(2, c.DeathSaveFailures);
    }

    [Theory]
    [InlineData(5, 20, 10, 15)]   // cura normale entro il massimo
    [InlineData(18, 20, 10, 20)]  // cura oltre il massimo: il tetto è MaxHitPoints
    [InlineData(0, 20, 5, 5)]     // dagli 0 PF sale comunque: pura aritmetica, non un "rianima"
    public void ApplyHealing_sale_entro_il_massimo(int hp, int max, int cura, int hpAtteso)
    {
        var c = new Character { HitPoints = hp, MaxHitPoints = max };
        CharacterView.ApplyHealing(c, cura);
        Assert.Equal(hpAtteso, c.HitPoints);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void ApplyHealing_con_valore_non_positivo_non_fa_nulla(int cura)
    {
        var c = new Character { HitPoints = 10, MaxHitPoints = 20 };
        CharacterView.ApplyHealing(c, cura);
        Assert.Equal(10, c.HitPoints);
    }

    [Fact]
    public void ApplyHealing_non_tocca_il_cuscinetto_ne_i_tiri_salvezza()
    {
        var c = new Character
        {
            HitPoints = 5, MaxHitPoints = 20, TempHitPoints = 4,
            DeathSaveSuccesses = 2, DeathSaveFailures = 1,
        };
        CharacterView.ApplyHealing(c, 50);
        Assert.Equal(20, c.HitPoints);
        Assert.Equal(4, c.TempHitPoints);
        Assert.Equal(2, c.DeathSaveSuccesses);
        Assert.Equal(1, c.DeathSaveFailures);
    }
}
