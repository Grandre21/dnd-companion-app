using DndCompanion.Models;
using DndCompanion.Models.Packages;
using DndCompanion.Services;
using Xunit;

namespace DndCompanion.Tests;

/// <summary>
/// <see cref="CreationChain.Deriva"/> è il fold che porta un personaggio dal baseline (livello 1) al
/// livello richiesto, un livello alla volta, riusando <see cref="LevelUpPlanner"/> — nessun secondo
/// motore di progressione qui dentro. Le fixture di tabella sono proprie (non il pacchetto SRD),
/// nello stile di <c>Tests/LevelUpPlannerTests.cs</c>.
/// </summary>
public class CreationChainTests
{
    // Cinque livelli, con una decisione bloccante al 3° (sottoclasse) e una al 4° (l'incremento di
    // caratteristica): sono le due che nella 5e aprono davvero una scelta del giocatore. Il 2° e il
    // 5° non aprono nulla (TipoDiScelta.Nessuna), e servono a verificare che quei livelli NON
    // richiedano scelte.
    private const string TabellaCinqueLivelli =
        "L1 — Furia\n" +
        "L2 — Senso del pericolo\n" +
        "L3 — Sottoclasse della prova\n" +
        "L4 — Incremento punteggio caratteristica\n" +
        "L5 — Attacco extra";

    // Come sopra, ma con un sesto livello che ECHEGGIA la sottoclasse — esattamente come fanno le
    // classi vere (Barbaro 3-6-10-14, Guerriero 3-7-10-15-18, Ladro 3-9-13-17, …): senza risposta al
    // 3°, pg.Subclass resta vuota e CostruisciDecisioni la ricreerebbe identica anche qui (SERIO 3).
    private const string TabellaSeiLivelliConEcoSottoclasse =
        "L1 — Furia\n" +
        "L2 — Senso del pericolo\n" +
        "L3 — Sottoclasse della prova\n" +
        "L4 — Incremento punteggio caratteristica\n" +
        "L5 — Attacco extra\n" +
        "L6 — Privilegio di sottoclasse";

    private static List<PackageSubclass> Sottoclassi() => new()
    {
        new() { Id = "s/prova", Name = "Sottoclasse Prova", Description = "Una sottoclasse di prova." }
    };

    private static List<PackageFeat> Talenti() => new()
    {
        new()
        {
            Id = "t/asi", Name = "Incremento del Punteggio di Caratteristica",
            Category = "Generale", Description = "Aumenta due punteggi di 1, o uno di 2."
        }
    };

    private static Character Baseline(
        int livello = 1, int costituzione = 14, string classe = "Prova", string dado = "d10")
    {
        var modCos = CharacterCalculations.GetModifier(costituzione);
        var pf = CharacterWizardLogic.SuggestMaxHp(dado, modCos, livello);
        return new Character
        {
            Id = "pg-1", Name = "Personaggio di prova", Class = classe, Level = livello,
            Strength = 10, Dexterity = 10, Constitution = costituzione,
            Intelligence = 10, Wisdom = 10, Charisma = 10,
            MaxHitPoints = pf, HitPoints = pf, HitDiceMax = $"{livello}{dado}",
        };
    }

    /// <summary>Confronto per riflessione, condiviso con <c>CharacterCloneTests</c> — v.
    /// <see cref="CharacterReflectionTestHelpers"/> (MINORE 9 del gate del 2026-08-06: era la stessa
    /// copia in due file, un punto solo da aggiornare al prossimo campo nuovo).</summary>
    private static void AssertPersonaggiUguali(Character atteso, Character effettivo) =>
        CharacterReflectionTestHelpers.AssertPersonaggiUguali(atteso, effettivo, (prop, valoreAtteso, valoreEffettivo) =>
            $"Character.{prop.Name}: atteso='{valoreAtteso}' ma ha '{valoreEffettivo}'.");

    [Fact]
    public void Deriva_non_muta_mai_il_baseline_ricevuto()
    {
        var baseline = Baseline();
        var primaDelFold = CharacterClone.Clona(baseline); // snapshot indipendente per il confronto

        CreationChain.Deriva(
            baseline, 5, TabellaCinqueLivelli, Sottoclassi(), Talenti(), null, null, dadoVitaClasse: "d10");

        AssertPersonaggiUguali(primaDelFold, baseline);
    }

    [Fact]
    public void Deriva_e_idempotente_con_gli_stessi_argomenti()
    {
        var baseline = Baseline();
        var risposte = RisposteComplete();

        var primo = CreationChain.Deriva(
            baseline, 5, TabellaCinqueLivelli, Sottoclassi(), Talenti(), risposte, null, dadoVitaClasse: "d10");
        var secondo = CreationChain.Deriva(
            baseline, 5, TabellaCinqueLivelli, Sottoclassi(), Talenti(), risposte, null, dadoVitaClasse: "d10");

        AssertPersonaggiUguali(primo.Personaggio, secondo.Personaggio);
        Assert.Equal(primo.Completa, secondo.Completa);
        Assert.Equal(primo.Motivo, secondo.Motivo);
        Assert.Equal(primo.Tappe.Count, secondo.Tappe.Count);
    }

    [Fact]
    public void Livello_richiesto_pari_al_baseline_non_produce_tappe()
    {
        var baseline = Baseline();

        var esito = CreationChain.Deriva(baseline, 1, TabellaCinqueLivelli, null, null, null, null);

        Assert.Empty(esito.Tappe);
        Assert.True(esito.Completa);
        Assert.Null(esito.Motivo);
        Assert.NotSame(baseline, esito.Personaggio);
        AssertPersonaggiUguali(baseline, esito.Personaggio);
    }

    [Fact]
    public void Livello_richiesto_inferiore_al_baseline_non_produce_tappe()
    {
        var baseline = Baseline(livello: 5);

        var esito = CreationChain.Deriva(baseline, 3, TabellaCinqueLivelli, null, null, null, null);

        Assert.Empty(esito.Tappe);
        Assert.True(esito.Completa);
        Assert.Equal(5, esito.Personaggio.Level); // non scende: non è un downgrade, solo un no-op
    }

    [Fact]
    public void Catena_1_a_5_segnala_le_tappe_con_scelte_e_avanza_comunque()
    {
        var baseline = Baseline();

        var esito = CreationChain.Deriva(
            baseline, 5, TabellaCinqueLivelli, Sottoclassi(), Talenti(), null, null, dadoVitaClasse: "d10");

        Assert.Equal(4, esito.Tappe.Count); // livelli 2, 3, 4, 5
        Assert.False(esito.Tappe.Single(t => t.Livello == 2).RichiedeScelte);
        Assert.True(esito.Tappe.Single(t => t.Livello == 3).RichiedeScelte);  // sottoclasse
        Assert.True(esito.Tappe.Single(t => t.Livello == 4).RichiedeScelte);  // talento/ASI
        Assert.False(esito.Tappe.Single(t => t.Livello == 5).RichiedeScelte);

        // Il "giocatore deve vedere i punti ferita crescere mentre decide": il livello avanza
        // fino in fondo anche senza le risposte a 3° e 4°.
        Assert.Equal(5, esito.Personaggio.Level);
        Assert.False(esito.Completa);
        Assert.NotNull(esito.Motivo);
        Assert.Contains("Restano 2 scelte da fare", esito.Motivo);
        Assert.Contains("3", esito.Motivo);
        Assert.Contains("4", esito.Motivo);

        // Nessuna scelta rubata: senza risposta, sottoclasse e caratteristiche restano quelle di
        // partenza, non un valore inventato.
        Assert.True(string.IsNullOrEmpty(esito.Personaggio.Subclass));
        Assert.Equal(baseline.Constitution, esito.Personaggio.Constitution);
    }

    [Fact]
    public void Catena_1_a_5_si_completa_quando_arrivano_tutte_le_risposte()
    {
        var baseline = Baseline();
        var risposte = RisposteComplete();

        var esito = CreationChain.Deriva(
            baseline, 5, TabellaCinqueLivelli, Sottoclassi(), Talenti(), risposte, null, dadoVitaClasse: "d10");

        Assert.True(esito.Completa);
        Assert.Null(esito.Motivo);
        Assert.All(esito.Tappe, t => Assert.False(t.RichiedeScelte));
        Assert.Equal("Sottoclasse Prova", esito.Personaggio.Subclass);
        Assert.Equal(baseline.Constitution + 2, esito.Personaggio.Constitution);
        Assert.Equal(5, esito.Personaggio.Level);
    }

    private static IReadOnlyDictionary<int, IReadOnlyDictionary<string, Risposta>> RisposteComplete() =>
        new Dictionary<int, IReadOnlyDictionary<string, Risposta>>
        {
            [3] = new Dictionary<string, Risposta>
            {
                ["L3:sottoclasse"] = new() { Scelte = new[] { "Sottoclasse Prova" } },
            },
            [4] = new Dictionary<string, Risposta>
            {
                ["L4:talento"] = new() { Scelte = new[] { "Incremento del Punteggio di Caratteristica" } },
                ["L4:talento/punteggi"] = new()
                {
                    Punteggi = new Dictionary<string, int> { ["constitution"] = 2 },
                },
            },
        };

    [Fact]
    public void Classe_senza_tabella_interrompe_la_catena_con_un_motivo_parlante()
    {
        var baseline = Baseline();

        var esito = CreationChain.Deriva(baseline, 3, testoProgressione: null, null, null, null, null);

        Assert.False(esito.Completa);
        Assert.NotNull(esito.Motivo);
        Assert.Contains("non ha una tabella", esito.Motivo);
        Assert.Empty(esito.Tappe);
        Assert.Equal(baseline.Level, esito.Personaggio.Level);
    }

    [Fact]
    public void I_tiri_per_livello_sono_indipendenti()
    {
        // Nessuna decisione su nessuno dei tre livelli: isola l'effetto del tiro dal resto.
        const string tabella = "L1 — Furia\nL2 — Senso del pericolo\nL3 — Vigore";
        var baseline = Baseline(costituzione: 10, dado: "d12"); // modificatore 0: il guadagno è il tiro puro
        var tiri = new Dictionary<int, int> { [2] = 12, [3] = 1 };

        var esito = CreationChain.Deriva(baseline, 3, tabella, null, null, null, tiri, dadoVitaClasse: "d12");

        var pf2 = esito.Tappe.Single(t => t.Livello == 2).Piano.PuntiFeritaMax.Proposto;
        var pf3 = esito.Tappe.Single(t => t.Livello == 3).Piano.PuntiFeritaMax.Proposto;

        Assert.Equal(baseline.MaxHitPoints + 12, pf2);   // tiro del livello 2: 12
        Assert.Equal(pf2 + 1, pf3);                       // tiro del livello 3: 1, non lo stesso 12
        Assert.Equal(pf3, esito.Personaggio.MaxHitPoints);
    }

    // L'incrocio fra le due fonti dei punti ferita (D4 della spec di design): con la media a ogni
    // livello e nessun incremento di Costituzione, la catena e CharacterWizardLogic.SuggestMaxHp
    // devono coincidere ESATTAMENTE, per costruzione (stesso dado, stesso modificatore, stessa
    // formula "media arrotondata per eccesso + modCos" per livello) — se un giorno divergono, una
    // delle due fonti ha un difetto.
    [Theory]
    [InlineData(6, 8)]
    [InlineData(6, 10)]
    [InlineData(6, 16)]
    [InlineData(8, 8)]
    [InlineData(8, 10)]
    [InlineData(8, 16)]
    [InlineData(12, 8)]
    [InlineData(12, 10)]
    [InlineData(12, 16)]
    public void Con_la_media_la_catena_e_SuggestMaxHp_coincidono(int facceDado, int costituzione)
    {
        const int livelloFinale = 6;
        var dado = $"d{facceDado}";
        var modCos = CharacterCalculations.GetModifier(costituzione);

        var baseline = Baseline(livello: 1, costituzione: costituzione, classe: "Prova", dado: dado);

        // Nessuna decisione a nessun livello: privilegi generici che non innescano TipoDiScelta.
        var tabella = string.Join("\n",
            Enumerable.Range(1, livelloFinale).Select(l => $"L{l} — Privilegio {l}"));

        var esito = CreationChain.Deriva(
            baseline, livelloFinale, tabella, null, null, null, null, dadoVitaClasse: dado);

        var atteso = CharacterWizardLogic.SuggestMaxHp(dado, modCos, livelloFinale);
        Assert.Equal(atteso, esito.Personaggio.MaxHitPoints);
    }

    // L'incrocio con l'ASI di Costituzione (D4): la Theory sopra usa solo "Privilegio {l}", cioè
    // zero decisioni — il retroattivo di Costituzione (LevelUpPlanner.Pianifica, righe 67-90) non
    // entra mai in gioco lì, ma è proprio il retroattivo la ragione per cui le due fonti coincidono
    // (MINORE 8 del gate del 2026-08-06). Verifica indipendente: baseline d10, COS 14, fino al 5°
    // con +2 Costituzione al 4° → 49 da entrambe le fonti.
    [Fact]
    public void Con_lASI_di_Costituzione_la_catena_e_SuggestMaxHp_coincidono()
    {
        const int livelloFinale = 5;
        var baseline = Baseline(livello: 1, costituzione: 14, classe: "Prova", dado: "d10");

        var risposte = new Dictionary<int, IReadOnlyDictionary<string, Risposta>>
        {
            [4] = new Dictionary<string, Risposta>
            {
                ["L4:talento"] = new() { Scelte = new[] { "Incremento del Punteggio di Caratteristica" } },
                ["L4:talento/punteggi"] = new()
                {
                    Punteggi = new Dictionary<string, int> { ["constitution"] = 2 },
                },
            },
        };

        var esito = CreationChain.Deriva(
            baseline, livelloFinale, TabellaCinqueLivelli, Sottoclassi(), Talenti(), risposte, null, dadoVitaClasse: "d10");

        var modCosFinale = CharacterCalculations.GetModifier(baseline.Constitution + 2);
        var atteso = CharacterWizardLogic.SuggestMaxHp("d10", modCosFinale, livelloFinale);

        // d10: 10+3 al 1°, poi 4 livelli (2°-5°) da 6+3 ciascuno → 13 + 4×9 = 49.
        Assert.Equal(49, atteso);
        Assert.Equal(16, esito.Personaggio.Constitution);
        Assert.Equal(atteso, esito.Personaggio.MaxHitPoints);
    }

    // SERIO 4 del gate del 2026-08-06: LevelUpPlanner.IncrementoCostituzione legge
    // risposte[...].Punteggi["constitution"] senza passare da PunteggiValidi (privato). Con la
    // guardia tutto-o-niente di Applica era innocuo; qui quella guardia è elusa apposta (il livello
    // avanza sempre), quindi una risposta malformata come {constitution: 1} (un solo "+", non i due
    // punti richiesti) farebbe calcolare i PF su un incremento che poi non verrà mai scritto.
    //
    // SERIO 3 del gate del 2026-08-06 (secondo giro): la Costituzione del baseline deve essere
    // DISPARI. Con 14 (il valore di Baseline() di default) la risposta malformata la porta a 15, e
    // GetModifier(14) == GetModifier(15) == 2: il modificatore non cambia, quindi il piano "con
    // tutte le risposte" e quello "con le sole risposte valide" producono lo STESSO guadagno di
    // livello e lo STESSO retroattivo — il confronto sotto passerebbe identico anche togliendo la
    // doppia pianificazione che questo test vuole verificare. Con 13 (dispari) 13→14 sposta il
    // modificatore da +1 a +2: guadagno di livello e retroattivo ((modCosDopo-modCosPrima)×livello,
    // v. LevelUpPlanner.cs) divergono per parecchio se la seconda pianificazione sparisse. NON
    // "semplificare" questo valore a un pari: il test tornerebbe a passare per coincidenza
    // aritmetica, non perché verifica la correzione (v. la prova a mano descritta nel report del
    // gate: commentando la seconda pianificazione questo test diventa rosso).
    [Fact]
    public void Risposta_di_Costituzione_malformata_non_altera_i_punti_ferita()
    {
        var baseline = Baseline(costituzione: 13);
        var rispostaMalformata = new Dictionary<int, IReadOnlyDictionary<string, Risposta>>
        {
            [4] = new Dictionary<string, Risposta>
            {
                ["L4:talento"] = new() { Scelte = new[] { "Incremento del Punteggio di Caratteristica" } },
                ["L4:talento/punteggi"] = new()
                {
                    // Un solo "+": la 5e 2024 richiede +2 a una sola caratteristica o +1 a due
                    // distinte — questa risposta non soddisfa PunteggiValidi, Applica non la scrive.
                    Punteggi = new Dictionary<string, int> { ["constitution"] = 1 },
                },
            },
        };

        var conRispostaMalformata = CreationChain.Deriva(
            baseline, 5, TabellaCinqueLivelli, Sottoclassi(), Talenti(), rispostaMalformata, null, dadoVitaClasse: "d10");
        var senzaRisposta = CreationChain.Deriva(
            baseline, 5, TabellaCinqueLivelli, Sottoclassi(), Talenti(), null, null, dadoVitaClasse: "d10");

        Assert.Equal(senzaRisposta.Personaggio.MaxHitPoints, conRispostaMalformata.Personaggio.MaxHitPoints);
        Assert.Equal(baseline.Constitution, conRispostaMalformata.Personaggio.Constitution); // non scritta
    }

    // SERIO 3 del gate del 2026-08-06: senza risposta al 3°, CostruisciDecisioni ricrea la decisione
    // di sottoclasse IDENTICA al 6° (l'eco che tutte le classi con più privilegi di sottoclasse
    // portano: Barbaro 3-6-10-14, Guerriero 3-7-10-15-18, Ladro 3-9-13-17, …) perché pg.Subclass è
    // ancora vuota. Deve restare UNA sola decisione in tutte le tappe, e il Motivo non deve nominare
    // due volte la stessa scelta (livello 3 e 6 non sono due scelte, ne è una sola non risposta).
    [Fact]
    public void Sottoclasse_non_risposta_non_si_ripete_ai_livelli_successivi()
    {
        var baseline = Baseline();
        var risposte = new Dictionary<int, IReadOnlyDictionary<string, Risposta>>
        {
            [4] = new Dictionary<string, Risposta>
            {
                ["L4:talento"] = new() { Scelte = new[] { "Incremento del Punteggio di Caratteristica" } },
                ["L4:talento/punteggi"] = new()
                {
                    Punteggi = new Dictionary<string, int> { ["constitution"] = 2 },
                },
            },
            // Livello 3 (sottoclasse) e il suo eco al 6° restano deliberatamente senza risposta.
        };

        var esito = CreationChain.Deriva(
            baseline, 6, TabellaSeiLivelliConEcoSottoclasse, Sottoclassi(), Talenti(), risposte, null, dadoVitaClasse: "d10");

        var decisioniSottoclasse = esito.Tappe
            .SelectMany(t => t.Piano.Decisioni)
            .Where(d => d.Chiave.EndsWith(":sottoclasse", StringComparison.Ordinal))
            .ToList();

        Assert.Single(decisioniSottoclasse);
        Assert.Equal("L3:sottoclasse", decisioniSottoclasse[0].Chiave);
        Assert.False(esito.Tappe.Single(t => t.Livello == 6).RichiedeScelte);

        Assert.False(esito.Completa);
        Assert.Equal("Resta 1 scelta da fare (livello 3).", esito.Motivo);
    }

    // MINORE 10 del gate del 2026-08-06: una DecisioneLibera (qui: sottoclasse senza catalogo) non
    // blocca mai RichiedeScelte, ma deve restare visibile come "c'è qualcosa che non blocca" — non
    // sparire come se la tappa fosse vuota, o l'auto-conferma a elenco della progressione (D5) la
    // salterebbe senza che nessuno annoti la sottoclasse.
    [Fact]
    public void Sottoclasse_senza_catalogo_non_blocca_ma_si_segnala_come_facoltativa()
    {
        var baseline = Baseline();

        var esito = CreationChain.Deriva(
            baseline, 3, TabellaCinqueLivelli, sottoclassi: null, talenti: null,
            rispostePerLivello: null, tiriPerLivello: null, dadoVitaClasse: "d10");

        var tappa3 = esito.Tappe.Single(t => t.Livello == 3);
        Assert.False(tappa3.RichiedeScelte);
        Assert.True(tappa3.HaScelteFacoltative);
        Assert.True(esito.Completa);
    }

    // MINORE 11 del gate del 2026-08-06: una tabella dichiarata solo in parte (qui fino al 5°,
    // personaggio richiesto all'8°) non fa tornare null Pianifica — le righe ci sono, il livello è
    // sotto 20 — quindi senza un avviso esplicito il fold avanzerebbe in silenzio con privilegi
    // vuoti e gli slot dell'ultimo livello dichiarato. È un dato mancante del catalogo, non una
    // scelta del giocatore: Completa resta vero, l'avviso si limita a segnalarlo.
    [Fact]
    public void Tabella_dichiarata_solo_in_parte_produce_un_avviso_e_non_blocca()
    {
        var baseline = Baseline();
        var tabellaParziale = string.Join("\n", Enumerable.Range(1, 5).Select(l => $"L{l} — Privilegio {l}"));

        var esito = CreationChain.Deriva(
            baseline, 8, tabellaParziale, null, null, null, null, dadoVitaClasse: "d10");

        Assert.True(esito.Completa);
        Assert.Null(esito.Motivo);
        Assert.Equal(8, esito.Personaggio.Level);
        Assert.Contains(esito.Avvisi, a => a.Contains("6") && a.Contains("7") && a.Contains("8"));
    }

    // SERIO 1 del gate del 2026-08-06 (secondo giro): per una classe SENZA sottoclassi a catalogo,
    // CostruisciDecisioni apre una DecisioneLibera per la sottoclasse (non una DecisioneFraOpzioni)
    // — e LevelUpPlan.Completa la considera SEMPRE soddisfatta, risposta null compresa (v.
    // LevelUpContracts.cs). Usare Completa come criterio di "risposta data" (com'era prima di
    // questa correzione) significa che sottoclasseInSospesoChiave non si valorizza MAI per queste
    // classi, e l'eco al 6° (Barbaro 3-6-10-14, Guerriero 3-7-10-15-18, Ladro 3-9-13-17, …) torna a
    // comparire come una seconda scheda identica invece di restare soppresso come al livello 3. Il
    // test gemello sopra (Sottoclasse_non_risposta_non_si_ripete_ai_livelli_successivi) NON
    // esercita questo ramo: lì il catalogo c'è, quindi la decisione è una DecisioneFraOpzioni e
    // passava già dal ramo Completa corretto.
    [Fact]
    public void Sottoclasse_libera_non_risposta_non_si_ripete_ai_livelli_successivi()
    {
        var baseline = Baseline();
        var risposte = new Dictionary<int, IReadOnlyDictionary<string, Risposta>>
        {
            [4] = new Dictionary<string, Risposta>
            {
                ["L4:talento"] = new() { Scelte = new[] { "Incremento del Punteggio di Caratteristica" } },
                ["L4:talento/punteggi"] = new()
                {
                    Punteggi = new Dictionary<string, int> { ["constitution"] = 2 },
                },
            },
            // Livello 3 (sottoclasse) e il suo eco al 6° restano senza risposta, come nel test
            // gemello sopra — ma qui sottoclassi è null: CostruisciDecisioni apre una
            // DecisioneLibera, non una DecisioneFraOpzioni.
        };

        var esito = CreationChain.Deriva(
            baseline, 6, TabellaSeiLivelliConEcoSottoclasse, null, Talenti(), risposte, null, dadoVitaClasse: "d10");

        var decisioniSottoclasse = esito.Tappe
            .SelectMany(t => t.Piano.Decisioni)
            .Where(d => d.Chiave.EndsWith(":sottoclasse", StringComparison.Ordinal))
            .ToList();

        Assert.Single(decisioniSottoclasse);
        Assert.Equal("L3:sottoclasse", decisioniSottoclasse[0].Chiave);
        Assert.IsType<DecisioneLibera>(decisioniSottoclasse[0]);
    }

    // SERIO 2 del gate del 2026-08-06 (secondo giro): una tabella SPARSA con buchi INTERNI (qui
    // righe solo a 1, 3, 5) è legale e attesa in questo formato — ClassProgression.Serializza
    // omette di proposito le righe senza privilegi né slot. Il livello 2 e il 4 sono buchi interni,
    // non "righe mancanti": non devono produrre alcun avviso. Il test esistente
    // Tabella_dichiarata_solo_in_parte_produce_un_avviso_e_non_blocca usa una tabella DENSA
    // troncata (1..5, richiesto 8): copre il caso voluto (righe oltre l'ultima dichiarata) e resta
    // com'è.
    [Fact]
    public void Tabella_sparsa_con_buchi_interni_non_produce_avvisi()
    {
        var baseline = Baseline();
        const string tabellaSparsa = "L1 — Privilegio 1\nL3 — Privilegio 3\nL5 — Privilegio 5";

        var esito = CreationChain.Deriva(
            baseline, 5, tabellaSparsa, null, null, null, null, dadoVitaClasse: "d10");

        Assert.True(esito.Completa);
        Assert.Null(esito.Motivo);
        Assert.Equal(5, esito.Personaggio.Level);
        Assert.Empty(esito.Avvisi);
    }

    // SERIO 4 del gate del 2026-08-06 (secondo giro): la tappa mostrata deve portare i punti
    // ferita di CIÒ CHE VIENE SCRITTO (pianoNumerico, calcolato sulle sole risposte valide), non
    // quelli calcolati su una risposta malformata che Applica non scriverà mai — altrimenti la
    // tappa del 4° annuncia punti ferita che il personaggio non riceve, e quella del 5° li
    // mostrerebbe tornare indietro.
    [Fact]
    public void Tappa_mostra_i_punti_ferita_che_il_personaggio_riceve_anche_con_risposta_malformata()
    {
        var baseline = Baseline(costituzione: 13); // dispari: v. il commento sul test SERIO 3 sopra

        var rispostaMalformata = new Dictionary<int, IReadOnlyDictionary<string, Risposta>>
        {
            [4] = new Dictionary<string, Risposta>
            {
                ["L4:talento"] = new() { Scelte = new[] { "Incremento del Punteggio di Caratteristica" } },
                ["L4:talento/punteggi"] = new()
                {
                    Punteggi = new Dictionary<string, int> { ["constitution"] = 1 }, // malformata
                },
            },
        };

        var esitoFino4 = CreationChain.Deriva(
            baseline, 4, TabellaCinqueLivelli, Sottoclassi(), Talenti(), rispostaMalformata, null, dadoVitaClasse: "d10");
        var esitoFino5 = CreationChain.Deriva(
            baseline, 5, TabellaCinqueLivelli, Sottoclassi(), Talenti(), rispostaMalformata, null, dadoVitaClasse: "d10");

        var tappa4 = esitoFino5.Tappe.Single(t => t.Livello == 4);

        // "Il personaggio derivato" fino al 4°, indipendentemente: deve avere gli STESSI punti
        // ferita che la tappa del 4° annuncia dentro la derivazione più lunga (fino al 5°).
        Assert.Equal(esitoFino4.Personaggio.MaxHitPoints, tappa4.Piano.PuntiFeritaMax.Proposto);
    }

    // MINORE 5 del gate del 2026-08-06 (secondo giro): "Dadi vita da più classi: aggiornali a
    // mano." nasce dentro LevelUpPlanner.Pianifica (hitDiceAttuale multiclasse, col '+') e restava
    // visibile solo dentro Tappe[k].Piano.Avvisi — ripetuto IDENTICO a ogni livello, perché Applica
    // lascia HitDiceMax intatto proprio per questo motivo (non sa quale dado aggiungere) e il '+'
    // resta a ogni giro successivo. EsitoCatena.Avvisi (la lettura naturale del nome) deve
    // aggregarlo, deduplicato. dadoVitaClasse="d10" tiene fuori dal test l'altro avviso ("Dado vita
    // non riconosciuto"), che qui si autorisolverebbe dopo un livello (Applica scrive il dado di
    // ripiego, che diventa riconoscibile) e non aiuterebbe a verificare la deduplica.
    [Fact]
    public void Avvisi_aggrega_gli_avvisi_dei_piani_deduplicati()
    {
        var baseline = Baseline();
        baseline.HitDiceMax = "2d8+1d6"; // multiclasse: l'avviso si ripete a ogni livello

        var esito = CreationChain.Deriva(
            baseline, 3, TabellaCinqueLivelli, Sottoclassi(), Talenti(), null, null, dadoVitaClasse: "d10");

        Assert.All(esito.Tappe,
            t => Assert.Contains(t.Piano.Avvisi, a => a.Contains("Dadi vita da più classi")));
        Assert.Single(esito.Avvisi, a => a.Contains("Dadi vita da più classi"));
    }

    // MINORE 6 del gate del 2026-08-06 (secondo giro): se una risposta arriva depositata sotto la
    // chiave GIÀ soppressa (qui "L6:sottoclasse", mentre "L3:sottoclasse" resta senza risposta), va
    // ignorata in silenzio — non "ripescata" per scrivere pg.Subclass. L'interfaccia attuale non
    // può produrre questo stato (la scheda del 6° non si mostra finché il 3° resta aperto), ma se
    // un domani le risposte venissero persistite il comportamento deve restare quello fissato qui,
    // non riscoperto per caso.
    [Fact]
    public void Risposta_sotto_chiave_soppressa_viene_ignorata()
    {
        var baseline = Baseline();
        var risposte = new Dictionary<int, IReadOnlyDictionary<string, Risposta>>
        {
            [4] = new Dictionary<string, Risposta>
            {
                ["L4:talento"] = new() { Scelte = new[] { "Incremento del Punteggio di Caratteristica" } },
                ["L4:talento/punteggi"] = new()
                {
                    Punteggi = new Dictionary<string, int> { ["constitution"] = 2 },
                },
            },
            // Nessuna risposta al 3° (la sottoclasse resta in sospeso da lì) — ma una risposta
            // arriva depositata sotto l'eco del 6°, come se un client avesse scritto nella chiave
            // sbagliata.
            [6] = new Dictionary<string, Risposta>
            {
                ["L6:sottoclasse"] = new() { Scelte = new[] { "Sottoclasse Prova" } },
            },
        };

        var esito = CreationChain.Deriva(
            baseline, 6, TabellaSeiLivelliConEcoSottoclasse, Sottoclassi(), Talenti(), risposte, null, dadoVitaClasse: "d10");

        Assert.True(string.IsNullOrEmpty(esito.Personaggio.Subclass)); // la risposta depositata non viene scritta
        Assert.False(esito.Completa);
        Assert.Equal("Resta 1 scelta da fare (livello 3).", esito.Motivo); // non due, malgrado la risposta al 6°
    }
}
