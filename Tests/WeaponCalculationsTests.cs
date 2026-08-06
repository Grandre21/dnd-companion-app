using DndCompanion.Models;
using DndCompanion.Services;
using Xunit;

namespace DndCompanion.Tests;

/// <summary>
/// Test del bonus d'attacco calcolato in <see cref="WeaponCalculations"/>, secondo le regole
/// 5e 2024: mischia → Forza; accurata → il migliore fra Forza e Destrezza; a distanza → Destrezza;
/// più competenza, salvo eccezione.
/// </summary>
public class WeaponCalculationsTests
{
    private static Character Personaggio(int str = 10, int dex = 10, int level = 1) => new()
    {
        Name = "Test",
        Strength = str,
        Dexterity = dex,
        Level = level,
    };

    private static InventoryItem Arma(
        bool finesse = false, bool ranged = false, bool nonCompetente = false) => new()
    {
        Name = "Test",
        ItemType = "weapon",
        IsFinesse = finesse,
        IsRanged = ranged,
        IsNotProficient = nonCompetente,
    };

    [Fact]
    public void Mischia_usa_la_forza()
    {
        var pg = Personaggio(str: 16, dex: 10, level: 1);
        var arma = Arma();

        Assert.Equal(3, WeaponCalculations.ModificatoreArma(arma, pg));
        Assert.Equal(5, WeaponCalculations.BonusAttacco(arma, pg)); // +3 Forza, +2 competenza
    }

    [Fact]
    public void Accurata_prende_la_destrezza_quando_e_maggiore_della_forza()
    {
        var pg = Personaggio(str: 10, dex: 18, level: 1);
        var arma = Arma(finesse: true);

        Assert.Equal(4, WeaponCalculations.ModificatoreArma(arma, pg)); // Destrezza +4 > Forza 0
    }

    [Fact]
    public void Accurata_prende_la_forza_quando_e_maggiore_della_destrezza()
    {
        var pg = Personaggio(str: 16, dex: 10, level: 1);
        var arma = Arma(finesse: true);

        Assert.Equal(3, WeaponCalculations.ModificatoreArma(arma, pg)); // Forza +3 > Destrezza 0
    }

    [Fact]
    public void A_distanza_usa_la_destrezza()
    {
        var pg = Personaggio(str: 16, dex: 14, level: 1);
        var arma = Arma(ranged: true);

        Assert.Equal(2, WeaponCalculations.ModificatoreArma(arma, pg)); // Destrezza, non Forza
    }

    [Fact]
    public void Non_competente_non_aggiunge_il_bonus_di_competenza()
    {
        var pg = Personaggio(str: 16, dex: 10, level: 5); // competenza +3
        var arma = Arma(nonCompetente: true);

        Assert.Equal(3, WeaponCalculations.BonusAttacco(arma, pg)); // solo il modificatore
    }

    [Fact]
    public void Accurata_e_a_distanza_insieme_vince_laccuratezza_cioe_il_migliore_dei_due()
    {
        // Caso homebrew: una balestra leggera segnata anche "accurata".
        // Precedenza dichiarata in WeaponCalculations.ModificatoreArma: accurata vince, quindi si
        // prende il migliore fra Forza e Destrezza anche se l'arma è anche a distanza.
        var pg = Personaggio(str: 18, dex: 12, level: 1);
        var arma = Arma(finesse: true, ranged: true);

        Assert.Equal(4, WeaponCalculations.ModificatoreArma(arma, pg)); // Forza +4 > Destrezza +1
    }

    [Fact]
    public void Caratteristica_bassa_da_modificatore_negativo()
    {
        var pg = Personaggio(str: 8, dex: 8, level: 1);
        var arma = Arma();

        Assert.Equal(-1, WeaponCalculations.ModificatoreArma(arma, pg));
        Assert.Equal(1, WeaponCalculations.BonusAttacco(arma, pg)); // -1 Forza, +2 competenza
    }

    [Fact]
    public void Barbaro_livello_5_forza_18_spadone_non_accurato_fa_piu_sette()
    {
        // Caso concreto dalla scheda cartacea che ha originato questo lavoro.
        var pg = Personaggio(str: 18, dex: 10, level: 5);
        var arma = Arma(); // spadone: mischia, non accurato

        Assert.Equal(4, WeaponCalculations.ModificatoreArma(arma, pg));
        Assert.Equal(7, WeaponCalculations.BonusAttacco(arma, pg)); // +4 Forza, +3 competenza
    }
}
