using DndCompanion.Models.Packages;
using DndCompanion.Services;
using Xunit;

namespace DndCompanion.Tests;

/// <summary>
/// Test di <see cref="SkillChoiceRules"/> — il vincolo "scegli N abilità fra queste M" e la sua
/// validazione.
/// </summary>
public class SkillChoiceRulesTests
{
    private static PackageSkillChoices Scelte(int count, params string[] from)
        => new() { Count = count, From = from.ToList() };

    // -----------------------------------------------------------------------------------
    // DaPacchetto — il degrado è totale, mai parziale
    // -----------------------------------------------------------------------------------

    [Fact]
    public void DaPacchetto_di_null_torna_null()
        => Assert.Null(SkillChoiceRules.DaPacchetto(null));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void DaPacchetto_con_count_non_positivo_torna_null(int count)
        => Assert.Null(SkillChoiceRules.DaPacchetto(Scelte(count, "Atletica")));

    [Fact]
    public void DaPacchetto_con_From_vuoto_torna_null()
        => Assert.Null(SkillChoiceRules.DaPacchetto(new PackageSkillChoices { Count = 2, From = new() }));

    [Fact]
    public void DaPacchetto_con_un_solo_nome_non_riconosciuto_invalida_lintero_vincolo()
    {
        // Il perno della regola: "Atletica" e "Sopravvivenza" sono validissime, ma basta
        // "Cucina" (homebrew, non rappresentabile su Character) a far degradare tutto a null.
        var risultato = SkillChoiceRules.DaPacchetto(Scelte(2, "Atletica", "Cucina", "Sopravvivenza"));
        Assert.Null(risultato);
    }

    [Fact]
    public void DaPacchetto_valido_riporta_conteggio_e_abilita_mappate()
    {
        var vincolo = SkillChoiceRules.DaPacchetto(Scelte(2, "Arcano", "Storia"));

        Assert.NotNull(vincolo);
        Assert.Equal(2, vincolo!.Quante);
        Assert.Equal(new[] { SkillType.Arcana, SkillType.History }, vincolo.Fra);
    }

    [Fact]
    public void DaPacchetto_scarta_i_duplicati_nella_lista_From()
    {
        var vincolo = SkillChoiceRules.DaPacchetto(Scelte(2, "Atletica", "atletica", "Sopravvivenza"));

        Assert.NotNull(vincolo);
        Assert.Equal(new[] { SkillType.Athletics, SkillType.Survival }, vincolo!.Fra);
    }

    [Fact]
    public void DaPacchetto_con_Count_maggiore_delle_voci_mappate_torna_null()
    {
        // {count: 3, from: ["Arcano", "Storia"]}: solo 2 abilità disponibili per un vincolo da 3 —
        // Valida non potrebbe MAI dichiararlo completo (SERIO 2 del gate del 2026-08-06).
        var risultato = SkillChoiceRules.DaPacchetto(Scelte(3, "Arcano", "Storia"));
        Assert.Null(risultato);
    }

    [Fact]
    public void DaPacchetto_con_Count_maggiore_delle_voci_dopo_la_deduplica_torna_null()
    {
        // Stesso caso, ma la sproporzione emerge solo DOPO aver scartato il duplicato: from ha 3
        // voci scritte, ma "Atletica"/"atletica" contano una sola volta.
        var risultato = SkillChoiceRules.DaPacchetto(Scelte(3, "Atletica", "atletica", "Sopravvivenza"));
        Assert.Null(risultato);
    }

    // -----------------------------------------------------------------------------------
    // DaTesto — passa da PackageRowMerge.LeggiScelte, non un secondo parser
    // -----------------------------------------------------------------------------------

    [Fact]
    public void DaTesto_riconosce_il_formato_canonico()
    {
        var vincolo = SkillChoiceRules.DaTesto("2 fra: Arcano, Storia");

        Assert.NotNull(vincolo);
        Assert.Equal(2, vincolo!.Quante);
        Assert.Equal(new[] { SkillType.Arcana, SkillType.History }, vincolo.Fra);
    }

    [Fact]
    public void DaTesto_riconosce_le_varianti_scritte_a_mano()
    {
        var vincolo = SkillChoiceRules.DaTesto("2 tra:  Arcano ,  Storia  .");

        Assert.NotNull(vincolo);
        Assert.Equal(new[] { SkillType.Arcana, SkillType.History }, vincolo!.Fra);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("prosa libera che non segue il formato")]
    public void DaTesto_su_testo_non_invertibile_torna_null(string? testo)
        => Assert.Null(SkillChoiceRules.DaTesto(testo));

    [Fact]
    public void DaTesto_con_un_nome_homebrew_nel_testo_libero_torna_null()
        => Assert.Null(SkillChoiceRules.DaTesto("2 fra: Arcano, Cucina"));

    // -----------------------------------------------------------------------------------
    // Valida
    // -----------------------------------------------------------------------------------

    [Fact]
    public void Valida_con_vincolo_null_e_sempre_completa()
    {
        var esito = SkillChoiceRules.Valida(null, new[] { SkillType.Athletics }, null);

        Assert.True(esito.Completa);
        Assert.Null(esito.Messaggio);
    }

    [Fact]
    public void Valida_con_vincolo_null_e_argomenti_null_non_solleva_e_e_completa()
    {
        var esito = SkillChoiceRules.Valida(null, null, null);

        Assert.True(esito.Completa);
        Assert.Empty(esito.Sovrapposte);
        Assert.Null(esito.Messaggio);
    }

    [Fact]
    public void Valida_esatta_e_completa_senza_messaggio()
    {
        var vincolo = new VincoloAbilita(2, new[] { SkillType.Arcana, SkillType.History, SkillType.Nature });
        var esito = SkillChoiceRules.Valida(vincolo, new[] { SkillType.Arcana, SkillType.History }, null);

        Assert.True(esito.Completa);
        Assert.Empty(esito.Sovrapposte);
        Assert.Null(esito.Messaggio);
    }

    [Fact]
    public void Valida_incompleta_segnala_quante_ne_mancano()
    {
        var vincolo = new VincoloAbilita(2, new[] { SkillType.Arcana, SkillType.History, SkillType.Nature });
        var esito = SkillChoiceRules.Valida(vincolo, new[] { SkillType.Arcana }, null);

        Assert.False(esito.Completa);
        Assert.Equal("Scegline ancora 1.", esito.Messaggio);
    }

    [Fact]
    public void Valida_eccedente_segnala_quante_ne_sono_state_scelte()
    {
        var vincolo = new VincoloAbilita(2, new[] { SkillType.Arcana, SkillType.History, SkillType.Nature });
        var esito = SkillChoiceRules.Valida(
            vincolo, new[] { SkillType.Arcana, SkillType.History, SkillType.Nature }, null);

        Assert.False(esito.Completa);
        Assert.Equal("Ne hai scelte 3 su 2.", esito.Messaggio);
    }

    [Fact]
    public void Valida_conta_i_distinti_non_i_duplicati()
    {
        // [Arcana, Arcana] non deve valere come 2 scelte distinte su un vincolo da 2 (MINORE 5 del
        // gate del 2026-08-06): Sovrapposte usa già .Distinct(), Completa deve seguire la stessa regola.
        var vincolo = new VincoloAbilita(2, new[] { SkillType.Arcana, SkillType.History });
        var esito = SkillChoiceRules.Valida(vincolo, new[] { SkillType.Arcana, SkillType.Arcana }, null);

        Assert.False(esito.Completa);
        Assert.Equal("Scegline ancora 1.", esito.Messaggio);
    }

    [Fact]
    public void Valida_con_scelta_fuori_dallelenco_non_e_completa()
    {
        var vincolo = new VincoloAbilita(2, new[] { SkillType.Arcana, SkillType.History });
        var esito = SkillChoiceRules.Valida(vincolo, new[] { SkillType.Arcana, SkillType.Stealth }, null);

        Assert.False(esito.Completa);
        Assert.NotNull(esito.Messaggio);
    }

    [Fact]
    public void Valida_segnala_la_sovrapposizione_col_background_pur_restando_completa()
    {
        var vincolo = new VincoloAbilita(2, new[] { SkillType.Insight, SkillType.Survival, SkillType.Nature });
        var esito = SkillChoiceRules.Valida(
            vincolo,
            new[] { SkillType.Insight, SkillType.Survival },
            new[] { SkillType.Insight });

        Assert.True(esito.Completa);
        Assert.Equal(new[] { SkillType.Insight }, esito.Sovrapposte);
        Assert.Equal("Intuizione te la dà già il background: sceglierla è uno spreco.", esito.Messaggio);
    }

    [Fact]
    public void Valida_segnala_la_sovrapposizione_multipla_al_plurale()
    {
        var vincolo = new VincoloAbilita(2, new[] { SkillType.Insight, SkillType.Survival });
        var esito = SkillChoiceRules.Valida(
            vincolo,
            new[] { SkillType.Insight, SkillType.Survival },
            new[] { SkillType.Insight, SkillType.Survival });

        Assert.True(esito.Completa);
        Assert.Equal(2, esito.Sovrapposte.Count);
        Assert.Contains("sceglierle è uno spreco", esito.Messaggio);
    }

    [Fact]
    public void Valida_con_vincolo_null_segnala_comunque_la_sovrapposizione()
    {
        var esito = SkillChoiceRules.Valida(null, new[] { SkillType.Insight }, new[] { SkillType.Insight });

        Assert.True(esito.Completa);
        Assert.Equal(new[] { SkillType.Insight }, esito.Sovrapposte);
        Assert.NotNull(esito.Messaggio);
    }
}
