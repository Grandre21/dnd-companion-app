using DndCompanion.Models;
using DndCompanion.Services;
using Xunit;

namespace DndCompanion.Tests;

/// <summary>
/// Test di <see cref="RestCalculations"/>: riposo lungo e riposo breve. RiposoLungo/RiposoBreve
/// sono pure (calcolano, non scrivono); <see cref="RestCalculations.Applica"/> è l'unico punto che
/// muta il personaggio, e lo fa sull'istanza ricevuta.
/// </summary>
public class RestCalculationsTests
{
    private static Character Pg(
        int hp, int maxHp, string? hitDiceMax = "5d10", int hitDiceSpent = 0,
        int con = 10, int tempHp = 0, int deathSuccesses = 0, int deathFailures = 0,
        bool inspiration = false) => new()
    {
        Name = "Test",
        HitPoints = hp,
        MaxHitPoints = maxHp,
        HitDiceMax = hitDiceMax,
        HitDiceSpent = hitDiceSpent,
        Constitution = con,
        TempHitPoints = tempHp,
        DeathSaveSuccesses = deathSuccesses,
        DeathSaveFailures = deathFailures,
        HeroicInspiration = inspiration,
    };

    // ---- RiposoLungo ----

    [Fact]
    public void RiposoLungo_un_pg_a_pf_pieni_resta_a_pf_pieni()
    {
        var pg = Pg(hp: 30, maxHp: 30, hitDiceSpent: 0);

        var esito = RestCalculations.RiposoLungo(pg);

        Assert.Equal(30, esito.HitPoints);
        Assert.Contains("PF già al massimo (30)", esito.Riepilogo);
    }

    [Fact]
    public void RiposoLungo_un_pg_ferito_torna_ai_pf_massimi()
    {
        var pg = Pg(hp: 12, maxHp: 30);

        var esito = RestCalculations.RiposoLungo(pg);

        Assert.Equal(30, esito.HitPoints);
        Assert.Contains("PF 12 → 30", esito.Riepilogo);
    }

    [Fact]
    public void RiposoLungo_azzera_i_pf_temporanei()
    {
        var pg = Pg(hp: 20, maxHp: 20, tempHp: 5);

        var esito = RestCalculations.RiposoLungo(pg);

        Assert.Equal(0, esito.TempHitPoints);
        Assert.Contains("PF temporanei azzerati", esito.Riepilogo);
    }

    [Fact]
    public void RiposoLungo_recupera_meta_dei_dadi_vita_arrotondata_per_difetto()
    {
        // 5 dadi vita totali, tutti spesi: metà arrotondata per difetto = 2.
        var pg = Pg(hp: 20, maxHp: 20, hitDiceMax: "5d10", hitDiceSpent: 5);

        var esito = RestCalculations.RiposoLungo(pg);

        Assert.Equal(3, esito.HitDiceSpent); // 5 spesi - 2 recuperati
        Assert.Contains("2 dadi vita recuperati", esito.Riepilogo);
    }

    [Fact]
    public void RiposoLungo_con_un_solo_dado_vita_speso_ne_recupera_almeno_uno()
    {
        // 1 dado vita totale: metà (0,5) arrotondata per difetto darebbe 0, ma il minimo è 1.
        var pg = Pg(hp: 8, maxHp: 8, hitDiceMax: "1d8", hitDiceSpent: 1);

        var esito = RestCalculations.RiposoLungo(pg);

        Assert.Equal(0, esito.HitDiceSpent);
        Assert.Contains("1 dado vita recuperato", esito.Riepilogo);
    }

    [Fact]
    public void RiposoLungo_dadi_vita_gia_tutti_disponibili_non_scende_sotto_zero()
    {
        // Nessun dado speso: la quota di recupero (minimo 1) non deve portare HitDiceSpent sotto 0.
        var pg = Pg(hp: 20, maxHp: 20, hitDiceMax: "5d10", hitDiceSpent: 0);

        var esito = RestCalculations.RiposoLungo(pg);

        Assert.Equal(0, esito.HitDiceSpent);
        Assert.DoesNotContain(esito.Riepilogo, r => r.Contains("recuperat"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("non valido")]
    public void RiposoLungo_con_hit_dice_max_vuoto_o_malformato_non_esplode(string? hitDiceMax)
    {
        var pg = Pg(hp: 5, maxHp: 20, hitDiceMax: hitDiceMax, hitDiceSpent: 0);

        var esito = RestCalculations.RiposoLungo(pg);

        Assert.Equal(20, esito.HitPoints);
        Assert.Equal(0, esito.HitDiceSpent);
    }

    [Fact]
    public void RiposoLungo_ripristina_tutti_gli_slot_incantesimo()
    {
        var pg = Pg(hp: 20, maxHp: 20);
        pg.SpellSlots1Used = 2;
        pg.SpellSlots3Used = 1;

        var esito = RestCalculations.RiposoLungo(pg);

        Assert.NotNull(esito.SpellSlotsUsed);
        Assert.All(esito.SpellSlotsUsed!, v => Assert.Equal(0, v));
        Assert.Contains("Slot incantesimo ripristinati", esito.Riepilogo);
    }

    [Fact]
    public void RiposoLungo_senza_slot_usati_non_annuncia_il_ripristino()
    {
        var pg = Pg(hp: 20, maxHp: 20);

        var esito = RestCalculations.RiposoLungo(pg);

        Assert.DoesNotContain("Slot incantesimo ripristinati", esito.Riepilogo);
    }

    [Fact]
    public void RiposoLungo_azzera_i_tiri_salvezza_contro_morte()
    {
        var pg = Pg(hp: 20, maxHp: 20, deathSuccesses: 2, deathFailures: 1);

        var esito = RestCalculations.RiposoLungo(pg);

        Assert.Equal(0, esito.DeathSaveSuccesses);
        Assert.Equal(0, esito.DeathSaveFailures);
        Assert.Contains("Tiri salvezza contro la morte azzerati", esito.Riepilogo);
    }

    [Fact]
    public void RiposoLungo_non_tocca_l_ispirazione_eroica()
    {
        // RiposoLungo non ha nemmeno un campo per l'ispirazione: la garanzia è che Applica non la
        // scriva, verificato lasciando il personaggio con ispirazione attiva dopo l'applicazione.
        var pg = Pg(hp: 20, maxHp: 20, inspiration: true);

        var esito = RestCalculations.RiposoLungo(pg);
        RestCalculations.Applica(pg, esito);

        Assert.True(pg.HeroicInspiration);
    }

    // ---- RiposoBreve ----

    [Fact]
    public void RiposoBreve_cura_il_totale_tirato_piu_il_modificatore_per_dado()
    {
        var pg = Pg(hp: 10, maxHp: 30, con: 14, hitDiceMax: "5d10", hitDiceSpent: 0); // mod Cos +2

        var esito = RestCalculations.RiposoBreve(pg, dadiSpesi: 2, totaleTirato: 12);

        Assert.Equal(26, esito.HitPoints); // 10 + 12 + (2*2)
        Assert.Equal(2, esito.HitDiceSpent);
        Assert.Contains("PF 10 → 26", esito.Riepilogo);
    }

    [Fact]
    public void RiposoBreve_non_supera_i_pf_massimi()
    {
        var pg = Pg(hp: 28, maxHp: 30, con: 14, hitDiceMax: "5d10", hitDiceSpent: 0);

        var esito = RestCalculations.RiposoBreve(pg, dadiSpesi: 2, totaleTirato: 12);

        Assert.Equal(30, esito.HitPoints);
    }

    [Fact]
    public void RiposoBreve_con_modificatore_di_costituzione_negativo_non_toglie_pf()
    {
        // Cos 6 -> mod -2. Tiro basso (4 su 2 dadi): 4 + (-2*2) = 0, mai negativo.
        var pg = Pg(hp: 10, maxHp: 30, con: 6, hitDiceMax: "5d10", hitDiceSpent: 0);

        var esito = RestCalculations.RiposoBreve(pg, dadiSpesi: 2, totaleTirato: 4);

        Assert.Equal(10, esito.HitPoints); // nessuna cura, ma nemmeno una perdita di PF
    }

    [Fact]
    public void RiposoBreve_con_dadi_vita_gia_tutti_spesi_non_cura_nulla()
    {
        var pg = Pg(hp: 10, maxHp: 30, hitDiceMax: "5d10", hitDiceSpent: 5); // 0 disponibili

        var esito = RestCalculations.RiposoBreve(pg, dadiSpesi: 2, totaleTirato: 14);

        Assert.Equal(10, esito.HitPoints);
        Assert.Equal(5, esito.HitDiceSpent);
        Assert.Contains("Nessun dado vita speso", esito.Riepilogo);
    }

    [Fact]
    public void RiposoBreve_vincola_i_dadi_spesi_a_quelli_disponibili()
    {
        // 1 solo dado disponibile: la richiesta di 3 viene ridotta a 1.
        var pg = Pg(hp: 10, maxHp: 30, con: 0, hitDiceMax: "5d10", hitDiceSpent: 4);

        var esito = RestCalculations.RiposoBreve(pg, dadiSpesi: 3, totaleTirato: 18);

        Assert.Equal(5, esito.HitDiceSpent); // 4 + 1, non 4 + 3
    }

    [Fact]
    public void RiposoBreve_non_tocca_slot_incantesimo_ne_tiri_salvezza_contro_morte()
    {
        var pg = Pg(hp: 10, maxHp: 30, deathSuccesses: 1, deathFailures: 2);
        pg.SpellSlots2Used = 1;

        var esito = RestCalculations.RiposoBreve(pg, dadiSpesi: 1, totaleTirato: 6);
        RestCalculations.Applica(pg, esito);

        Assert.Null(esito.SpellSlotsUsed);
        Assert.Equal(1, pg.SpellSlots2Used);
        Assert.Equal(1, pg.DeathSaveSuccesses);
        Assert.Equal(2, pg.DeathSaveFailures);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("non valido")]
    public void RiposoBreve_con_hit_dice_max_vuoto_o_malformato_non_esplode(string? hitDiceMax)
    {
        var pg = Pg(hp: 10, maxHp: 30, hitDiceMax: hitDiceMax, hitDiceSpent: 0);

        var esito = RestCalculations.RiposoBreve(pg, dadiSpesi: 2, totaleTirato: 10);

        Assert.Equal(10, esito.HitPoints); // 0 dadi disponibili -> nessuna cura
        Assert.Equal(0, esito.HitDiceSpent);
    }

    // ---- Applica ----

    [Fact]
    public void Applica_muta_l_istanza_ricevuta_non_una_copia()
    {
        var pg = Pg(hp: 10, maxHp: 30);
        var esito = RestCalculations.RiposoLungo(pg);

        RestCalculations.Applica(pg, esito);

        Assert.Equal(30, pg.HitPoints); // lo stesso riferimento, ora aggiornato
    }

    // ---- MediaDadoVita ----

    [Theory]
    [InlineData("1d8", 5)]
    [InlineData("1d6", 4)]
    [InlineData("1d12", 7)]
    [InlineData("3d12+2d8", 7)] // il primo blocco, "3d12"
    [InlineData(null, 0)]
    [InlineData("", 0)]
    [InlineData("non valido", 0)]
    public void MediaDadoVita_calcola_n_su_2_piu_1_del_primo_blocco(string? hitDiceMax, int atteso)
        => Assert.Equal(atteso, RestCalculations.MediaDadoVita(hitDiceMax));

    // ---- Risorse di classe ----

    [Fact]
    public void RiposoLungo_ripristina_le_risorse_con_ricarica_lungo_e_breve()
    {
        var pg = Pg(hp: 20, maxHp: 20);
        pg.ClassResources = new List<ClassResource>
        {
            new() { Nome = "Ira", Max = 2, Spesi = 2, Ricarica = "lungo" },
            new() { Nome = "Secondo fiato", Max = 1, Spesi = 1, Ricarica = "breve" },
        };

        var esito = RestCalculations.RiposoLungo(pg);
        RestCalculations.Applica(pg, esito);

        Assert.Equal(0, pg.ClassResources.Single(r => r.Nome == "Ira").Spesi);
        Assert.Equal(0, pg.ClassResources.Single(r => r.Nome == "Secondo fiato").Spesi);
        Assert.Contains("Ira e Secondo fiato ripristinate", esito.Riepilogo);
    }

    [Fact]
    public void RiposoBreve_ripristina_solo_le_risorse_con_ricarica_breve()
    {
        var pg = Pg(hp: 20, maxHp: 20);
        pg.ClassResources = new List<ClassResource>
        {
            new() { Nome = "Ira", Max = 2, Spesi = 2, Ricarica = "lungo" },
            new() { Nome = "Secondo fiato", Max = 1, Spesi = 1, Ricarica = "breve" },
        };

        var esito = RestCalculations.RiposoBreve(pg, dadiSpesi: 0, totaleTirato: 0);
        RestCalculations.Applica(pg, esito);

        Assert.Equal(2, pg.ClassResources.Single(r => r.Nome == "Ira").Spesi); // "lungo": non tocca
        Assert.Equal(0, pg.ClassResources.Single(r => r.Nome == "Secondo fiato").Spesi);
        Assert.Contains("Secondo fiato ripristinata", esito.Riepilogo);
        Assert.DoesNotContain(esito.Riepilogo, r => r.Contains("Ira"));
    }

    [Fact]
    public void RiposoLungo_non_tocca_le_risorse_con_ricarica_nessuna()
    {
        var pg = Pg(hp: 20, maxHp: 20);
        pg.ClassResources = new List<ClassResource>
        {
            new() { Nome = "Attacco furtivo", Max = 0, Spesi = 3, Ricarica = "nessuna" },
        };

        var esito = RestCalculations.RiposoLungo(pg);
        RestCalculations.Applica(pg, esito);

        Assert.Equal(3, pg.ClassResources.Single().Spesi);
        Assert.DoesNotContain(esito.Riepilogo, r => r.Contains("ripristinat"));
    }

    [Fact]
    public void RiposoLungo_con_lista_risorse_vuota_non_aggiunge_righe_ne_esplode()
    {
        var pg = Pg(hp: 20, maxHp: 20);
        pg.ClassResources = new List<ClassResource>();

        var esito = RestCalculations.RiposoLungo(pg);

        Assert.Null(esito.ClassResources);
        Assert.Null(esito.RisorseRipristinate);
        Assert.DoesNotContain(esito.Riepilogo, r => r.Contains("ripristinat"));
    }

    [Fact]
    public void RiposoLungo_con_lista_risorse_null_non_aggiunge_righe_ne_esplode()
    {
        var pg = Pg(hp: 20, maxHp: 20);
        pg.ClassResources = null!;

        var esito = RestCalculations.RiposoLungo(pg);
        RestCalculations.Applica(pg, esito); // non deve lanciare

        Assert.Null(esito.ClassResources);
        Assert.Null(esito.RisorseRipristinate);
        Assert.DoesNotContain(esito.Riepilogo, r => r.Contains("ripristinat"));
    }

    [Fact]
    public void RiposoLungo_una_risorsa_gia_piena_non_viene_annunciata()
    {
        var pg = Pg(hp: 20, maxHp: 20);
        pg.ClassResources = new List<ClassResource>
        {
            new() { Nome = "Ira", Max = 2, Spesi = 0, Ricarica = "lungo" },
        };

        var esito = RestCalculations.RiposoLungo(pg);

        Assert.DoesNotContain(esito.Riepilogo, r => r.Contains("ripristinat"));
        Assert.Null(esito.RisorseRipristinate);
    }

    [Fact]
    public void RiposoLungo_con_ricarica_malformata_non_tocca_la_risorsa()
    {
        var pg = Pg(hp: 20, maxHp: 20);
        pg.ClassResources = new List<ClassResource>
        {
            new() { Nome = "Misteriosa", Max = 3, Spesi = 2, Ricarica = "boh" },
        };

        var esito = RestCalculations.RiposoLungo(pg);
        RestCalculations.Applica(pg, esito);

        Assert.Equal(2, pg.ClassResources.Single().Spesi);
        Assert.DoesNotContain(esito.Riepilogo, r => r.Contains("ripristinat"));
    }
}
