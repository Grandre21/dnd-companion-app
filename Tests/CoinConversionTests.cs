using DndCompanion.Models;
using DndCompanion.Services;
using Xunit;

namespace DndCompanion.Tests;

/// <summary>
/// Test di <see cref="CoinConversion"/>: totale equivalente in oro e compattazione del gruzzolo.
/// TotaleInRame/TotaleInOro/FormattaTotaleInOro/Compatta sono pure (calcolano, non scrivono);
/// <see cref="CoinConversion.Applica"/> è l'unico punto che muta il personaggio, e lo fa
/// sull'istanza ricevuta.
/// </summary>
public class CoinConversionTests
{
    private static Character Pg(
        int platino = 0, int oro = 0, int electrum = 0, int argento = 0, int rame = 0) => new()
    {
        Name = "Test",
        PlatinumPieces = platino,
        GoldPieces = oro,
        ElectrumPieces = electrum,
        SilverPieces = argento,
        CopperPieces = rame,
    };

    // ---- Invariante: la compattazione non cambia il totale ----

    // Verifica due proprietà del ciclo Compatta+Applica:
    // 1) il totale in rame resta invariato (l'invariante economico: la compattazione non deve
    //    creare né distruggere valore);
    // 2) per gli ingressi NON già compatti (giaCompatto = false), l'esito deve differire da
    //    quello di partenza (almeno un taglio cambiato, e Cambia == true).
    // È la (2) a rendere il test non vacuo: un "Compatta" ridotto a no-op (restituisce gli
    // stessi valori, Cambia = false) supererebbe comunque l'assert (1) — un totale che non
    // cambia mai è banalmente invariato — ma fallirebbe qui. giaCompatto è calcolato a mano per
    // ciascun caso (v. sotto), non tramite CoinConversion stesso: verificarlo con la funzione
    // sotto test renderebbe l'asserzione circolare.
    [Theory]
    [InlineData(0, 12, 3, 4, 143, false)] // 1383 rame -> (0,13,3,8,3): cambia
    [InlineData(0, 0, 0, 0, 0, true)] // già compatto (tutto zero)
    [InlineData(2, 0, 1, 0, 5000, false)] // 5000 rame -> 50 oro: cambia
    [InlineData(5, 200, 7, 99, 1, false)] // 20991 rame di resto -> (5,209,7,9,1): cambia
    [InlineData(1, 1, 0, 1, 1, true)] // 111 rame di resto -> già (1,1,0,1,1): non cambia
    public void Compatta_e_Applica_non_cambiano_il_totale_in_rame(
        int platino, int oro, int electrum, int argento, int rame, bool giaCompatto)
    {
        var pg = Pg(platino, oro, electrum, argento, rame);
        var totalePrima = CoinConversion.TotaleInRame(pg);

        var esito = CoinConversion.Compatta(pg);
        CoinConversion.Applica(pg, esito);

        Assert.Equal(totalePrima, CoinConversion.TotaleInRame(pg));

        if (!giaCompatto)
        {
            Assert.True(esito.Cambia);
            Assert.True(
                pg.PlatinumPieces != platino || pg.GoldPieces != oro || pg.ElectrumPieces != electrum
                || pg.SilverPieces != argento || pg.CopperPieces != rame);
        }
    }

    // ---- Esempio approvato dall'utente ----

    [Fact]
    public void Compatta_esempio_approvato_dall_utente()
    {
        var pg = Pg(platino: 0, oro: 12, electrum: 3, argento: 4, rame: 143);

        var esito = CoinConversion.Compatta(pg);

        Assert.Equal(0, esito.PlatinumPieces);
        Assert.Equal(13, esito.GoldPieces);
        Assert.Equal(3, esito.ElectrumPieces);
        Assert.Equal(8, esito.SilverPieces);
        Assert.Equal(3, esito.CopperPieces);
        Assert.True(esito.Cambia);
    }

    // ---- Platino ed electrum non vengono mai creati ----

    [Fact]
    public void Compatta_da_solo_rame_non_crea_platino_ne_electrum()
    {
        var pg = Pg(rame: 5000);

        var esito = CoinConversion.Compatta(pg);

        Assert.Equal(0, esito.PlatinumPieces);
        Assert.Equal(50, esito.GoldPieces);
        Assert.Equal(0, esito.ElectrumPieces);
        Assert.Equal(0, esito.SilverPieces);
        Assert.Equal(0, esito.CopperPieces);
    }

    [Fact]
    public void Compatta_lascia_platino_ed_electrum_gia_posseduti_invariati()
    {
        var pg = Pg(platino: 2, oro: 0, electrum: 1, argento: 0, rame: 0);

        var esito = CoinConversion.Compatta(pg);

        Assert.Equal(2, esito.PlatinumPieces);
        Assert.Equal(1, esito.ElectrumPieces);
    }

    // ---- Cambia == false su gruzzolo già compatto ----

    [Fact]
    public void Compatta_su_gruzzolo_gia_compatto_non_segnala_cambiamento()
    {
        var pg = Pg(platino: 0, oro: 1, electrum: 0, argento: 1, rame: 1);

        var esito = CoinConversion.Compatta(pg);

        Assert.False(esito.Cambia);
    }

    [Fact]
    public void Compatta_su_gruzzolo_tutto_a_zero_non_segnala_cambiamento()
    {
        var pg = Pg();

        var esito = CoinConversion.Compatta(pg);

        Assert.False(esito.Cambia);
    }

    // ---- Valute negative trattate come zero ----

    [Fact]
    public void TotaleInRame_tratta_le_valute_negative_come_zero()
    {
        var pg = Pg(platino: -1, oro: -5, electrum: -2, argento: -3, rame: -100);

        var totale = CoinConversion.TotaleInRame(pg);

        Assert.Equal(0, totale);
    }

    [Fact]
    public void Compatta_con_valute_negative_non_esplode()
    {
        var pg = Pg(oro: -5, argento: -3, rame: -100);

        var esito = CoinConversion.Compatta(pg);

        Assert.Equal(0, esito.GoldPieces);
        Assert.Equal(0, esito.SilverPieces);
        Assert.Equal(0, esito.CopperPieces);
    }

    // ---- FormattaTotaleInOro ----

    [Theory]
    [InlineData(1533, "15,33")]
    [InlineData(1500, "15")]
    [InlineData(1530, "15,3")]
    public void FormattaTotaleInOro_due_decimali_virgola_senza_zeri_finali(int rame, string atteso)
    {
        var pg = Pg(rame: rame);

        Assert.Equal(atteso, CoinConversion.FormattaTotaleInOro(pg));
    }

    // ---- Nessun overflow ----

    [Fact]
    public void TotaleInRame_con_platino_al_massimo_non_va_in_negativo()
    {
        var pg = Pg(platino: int.MaxValue);

        var totale = CoinConversion.TotaleInRame(pg);

        Assert.True(totale > 0);
    }

    // ---- Applica ----

    [Fact]
    public void Applica_muta_l_istanza_ricevuta_non_una_copia()
    {
        var pg = Pg(oro: 12, electrum: 3, argento: 4, rame: 143);
        var esito = CoinConversion.Compatta(pg);

        CoinConversion.Applica(pg, esito);

        Assert.Equal(13, pg.GoldPieces); // lo stesso riferimento, ora aggiornato
    }

    // ---- Gli overload sui cinque interi coincidono con quelli su Character ----
    // (servono alla bozza dell'editor di CharacterItemsTab.razor, che non è un Character)

    [Theory]
    [InlineData(0, 12, 3, 4, 143)]
    [InlineData(2, 0, 1, 0, 5000)]
    [InlineData(0, 0, 0, 0, 0)]
    public void TotaleInRame_su_interi_coincide_con_quello_su_Character(
        int platino, int oro, int electrum, int argento, int rame)
    {
        var pg = Pg(platino, oro, electrum, argento, rame);

        Assert.Equal(
            CoinConversion.TotaleInRame(pg),
            CoinConversion.TotaleInRame(platino, oro, electrum, argento, rame));
    }

    [Theory]
    [InlineData(0, 12, 3, 4, 143)]
    [InlineData(0, 0, 0, 15, 0)]
    public void FormattaTotaleInOro_su_long_coincide_con_quello_su_Character(
        int platino, int oro, int electrum, int argento, int rame)
    {
        var pg = Pg(platino, oro, electrum, argento, rame);
        var totale = CoinConversion.TotaleInRame(platino, oro, electrum, argento, rame);

        Assert.Equal(
            CoinConversion.FormattaTotaleInOro(pg),
            CoinConversion.FormattaTotaleInOro(totale));
    }

    [Theory]
    [InlineData(0, 12, 3, 4, 143)]
    [InlineData(5, 200, 7, 99, 1)]
    [InlineData(0, 1, 0, 1, 1)]
    public void Compatta_su_interi_coincide_con_quello_su_Character(
        int platino, int oro, int electrum, int argento, int rame)
    {
        var pg = Pg(platino, oro, electrum, argento, rame);

        Assert.Equal(
            CoinConversion.Compatta(pg),
            CoinConversion.Compatta(platino, oro, electrum, argento, rame));
    }
}
