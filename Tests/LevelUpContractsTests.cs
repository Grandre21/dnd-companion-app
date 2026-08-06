using DndCompanion.Services;
using Xunit;

namespace DndCompanion.Tests;

/// <summary>
/// Test di <see cref="LevelUpPlan.Completo"/> sulla forma di <see cref="DecisionePunteggi"/>.
///
/// Il solo totale non basta a validare una ripartizione di caratteristiche: prima di questo file
/// <c>{"strength": 5, "constitution": -3}</c> passava il controllo perché somma a 2, e
/// <see cref="LevelUpPlanner.Applica"/> — che si fida di <c>Completo</c> e non riverifica la
/// forma — avrebbe scritto una Costituzione ABBASSATA di 3.
/// </summary>
public class LevelUpContractsTests
{
    private static readonly IReadOnlyDictionary<string, int> PunteggiAttualiFittizi =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["strength"] = 15, ["dexterity"] = 12, ["constitution"] = 16,
            ["intelligence"] = 8, ["wisdom"] = 10, ["charisma"] = 10,
        };

    private static readonly IReadOnlyList<Decisione> UnaDecisionePunteggi =
        new Decisione[]
        {
            new DecisionePunteggi("L4:talento/punteggi", "Incremento", PunteggiAttualiFittizi)
        };

    private static LevelUpPlan Piano() => new(
        Classe: "Barbaro",
        LivelloDa: 3,
        LivelloA: 4,
        DadoVita: "d12",
        MediaDado: 7,
        PuntiFeritaMax: new Proposta<int>(30, 37),
        PuntiFeritaCorrenti: new Proposta<int>(30, 37),
        DadiVita: new Proposta<string>("3d12", "4d12"),
        SlotMax: new Proposta<IReadOnlyList<int>>(Array.Empty<int>(), Array.Empty<int>()),
        CaratteristicaIncantatore: new Proposta<string>(string.Empty, string.Empty),
        BonusCompetenza: new Proposta<int>(2, 2),
        PrivilegiOttenuti: Array.Empty<string>(),
        Decisioni: UnaDecisionePunteggi,
        Avvisi: Array.Empty<string>(),
        CerchioSbloccato: null);

    private static IReadOnlyDictionary<string, Risposta> Risposte(IReadOnlyDictionary<string, int> punteggi)
        => new Dictionary<string, Risposta>
        {
            ["L4:talento/punteggi"] = new() { Punteggi = punteggi }
        };

    [Fact]
    public void Piu_due_a_una_sola_caratteristica_e_valido()
    {
        var risposte = Risposte(new Dictionary<string, int> { ["constitution"] = 2 });
        Assert.True(Piano().Completo(risposte));
    }

    [Fact]
    public void Piu_uno_a_due_caratteristiche_distinte_e_valido()
    {
        var risposte = Risposte(new Dictionary<string, int> { ["constitution"] = 1, ["strength"] = 1 });
        Assert.True(Piano().Completo(risposte));
    }

    [Fact]
    public void Punteggi_negativi_che_sommano_a_due_non_sono_validi()
    {
        // Il caso che il vecchio controllo lasciava passare: somma 2, ma abbassa la Costituzione.
        var risposte = Risposte(new Dictionary<string, int> { ["strength"] = 5, ["constitution"] = -3 });
        Assert.False(Piano().Completo(risposte));
    }

    [Fact]
    public void Una_chiave_ignota_non_e_valida()
    {
        var risposte = Risposte(new Dictionary<string, int> { ["pippo"] = 2 });
        Assert.False(Piano().Completo(risposte));
    }

    [Fact]
    public void La_somma_di_uno_solo_non_basta()
    {
        var risposte = Risposte(new Dictionary<string, int> { ["constitution"] = 1 });
        Assert.False(Piano().Completo(risposte));
    }

    [Fact]
    public void Il_dizionario_vuoto_non_e_valido()
    {
        var risposte = Risposte(new Dictionary<string, int>());
        Assert.False(Piano().Completo(risposte));
    }

    // ===== Completa: la stessa regola, ma su una singola decisione — usata dal dialogo per la
    // pillola di stato di una riga, senza duplicare la logica di Completo. =====

    [Fact]
    public void Completa_rispecchia_Completo_sulla_stessa_decisione()
    {
        var decisione = Assert.Single(UnaDecisionePunteggi);
        var soddisfatta = new Risposta { Punteggi = new Dictionary<string, int> { ["constitution"] = 2 } };
        var insufficiente = new Risposta { Punteggi = new Dictionary<string, int> { ["constitution"] = 1 } };

        var piano = Piano();
        Assert.True(piano.Completa(decisione, soddisfatta));
        Assert.False(piano.Completa(decisione, insufficiente));
        Assert.False(piano.Completa(decisione, null));
    }

    [Fact]
    public void Completa_una_DecisioneLibera_e_sempre_vera_anche_senza_risposta()
    {
        var libera = new DecisioneLibera("L4:libera/nota", "Nota", "avviso");
        Assert.True(Piano().Completa(libera, null));
    }

    [Fact]
    public void Completa_una_DecisioneFraOpzioni_richiede_il_numero_giusto_di_scelte()
    {
        var scelta = new DecisioneFraOpzioni("L3:sottoclasse", "Sottoclasse",
            new[] { new OpzioneDecisione("A", "..."), new OpzioneDecisione("B", "...") }, 1);

        var piano = Piano();
        Assert.True(piano.Completa(scelta, new Risposta { Scelte = new[] { "A" } }));
        Assert.False(piano.Completa(scelta, new Risposta()));
        Assert.False(piano.Completa(scelta, null));
    }
}
