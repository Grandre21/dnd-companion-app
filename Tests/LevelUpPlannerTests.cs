using System.Reflection;
using DndCompanion.Models;
using DndCompanion.Models.Packages;
using DndCompanion.Services;
using Xunit;

namespace DndCompanion.Tests;

public class LevelUpPlannerTests
{
    private const string TabellaBarbaro =
        "L1 — Ira, Difesa senza armatura\n" +
        "L2 — Senso del pericolo\n" +
        "L3 — Sottoclasse del Barbaro\n" +
        "L4 — Incremento punteggio caratteristica\n" +
        "L5 — Attacco extra, Movimento veloce";

    private const string TabellaMago =
        "L1 — Lanciare incantesimi · Slot 2\n" +
        "L2 — Tradizione arcana · Slot 3\n" +
        "L3 — · Slot 4/2";           // riga di soli slot: senza il marcatore "· Slot" ClassProgression.Leggi
                                     // leggerebbe "Slot 4/2" come un privilegio, non come slot assoluti

    private static Character Pg(int livello = 4, int costituzione = 16, string classe = "Barbaro")
        => new()
        {
            Id = "pg-1", Name = "Grog", Class = classe, Level = livello,
            Constitution = costituzione, MaxHitPoints = 38, HitPoints = 38,
            HitDiceMax = $"{livello}d12"
        };

    private static string PercorsoPacchetto()
    {
        // Il test gira da bin/<config>/<tfm>/: si risale fino alla cartella che contiene il
        // .csproj dell'app, così il percorso non dipende dalla profondità di output.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "DndCompanion.csproj")))
            dir = dir.Parent;

        Assert.True(dir is not null, "Radice del progetto non trovata risalendo da " + AppContext.BaseDirectory);
        return Path.Combine(dir!.FullName, "wwwroot/data/srd-2024-it.json");
    }

    private static CatalogPackage CaricaPacchetto()
    {
        var percorso = PercorsoPacchetto();
        Assert.True(File.Exists(percorso), $"Pacchetto SRD assente: {percorso}");

        var esito = CatalogPackageParser.Parse(File.ReadAllText(percorso), èIlManualeDellApp: true);

        Assert.True(esito.Errors.Count == 0,
            "Il pacchetto è stato RIFIUTATO dal parser:\n  " + string.Join("\n  ", esito.Errors));
        Assert.NotNull(esito.Package);
        return esito.Package!;
    }

    [Fact]
    public void Una_classe_senza_tabella_non_produce_un_piano()
        => Assert.Null(LevelUpPlanner.Pianifica(Pg(), testoProgressione: null, null, null, null));

    [Fact]
    public void I_punti_ferita_si_sommano_ai_correnti_non_si_ricalcolano()
    {
        // 38 max, ferito a 30. Guadagno medio d12 = 7, +3 di Costituzione = +10.
        var pg = Pg();
        pg.HitPoints = 30;

        var piano = LevelUpPlanner.Pianifica(pg, TabellaBarbaro, null, null, null)!;

        Assert.Equal(48, piano.PuntiFeritaMax.Proposto);
        Assert.Equal(40, piano.PuntiFeritaCorrenti.Proposto);   // ferito resta ferito, di 8
        Assert.Equal(38, piano.PuntiFeritaMax.Attuale);
    }

    [Fact]
    public void Il_tiro_del_dado_sostituisce_la_media_e_resta_nel_range()
    {
        var piano = LevelUpPlanner.Pianifica(Pg(), TabellaBarbaro, null, null, null, tiroPuntiFerita: 12)!;
        Assert.Equal(53, piano.PuntiFeritaMax.Proposto);        // 38 + 12 + 3

        var fuori = LevelUpPlanner.Pianifica(Pg(), TabellaBarbaro, null, null, null, tiroPuntiFerita: 99)!;
        Assert.Equal(53, fuori.PuntiFeritaMax.Proposto);        // troncato a 12, non 99
    }

    [Fact]
    public void L_incremento_di_costituzione_vale_anche_per_i_livelli_gia_posseduti()
    {
        // Da 4° a 5°, Costituzione 15 → 17: il modificatore passa da +2 a +3.
        // Guadagno del livello: 7 (media d12) + 3 = 10. Retroattivo: +1 × 4 livelli già avuti = +4.
        // Tabella dedicata: TabellaBarbaro ha già un L5, e due righe con lo stesso livello
        // renderebbero ambiguo quale privilegio porta il quinto.
        const string tabella = "L1 — Ira\nL5 — Incremento punteggio caratteristica";

        var pg = Pg(livello: 4, costituzione: 15);
        var talenti = new List<PackageFeat>
        {
            new() { Id = "t/asi", Name = "Incremento del Punteggio di Caratteristica",
                    Category = "Generale", Description = "..." }
        };
        var risposte = new Dictionary<string, Risposta>
        {
            ["L5:talento"] = new() { Scelte = new[] { "Incremento del Punteggio di Caratteristica" } },
            ["L5:talento/punteggi"] = new()
            {
                Punteggi = new Dictionary<string, int> { ["constitution"] = 2 }
            }
        };

        var piano = LevelUpPlanner.Pianifica(pg, tabella, null, talenti, risposte)!;

        Assert.Equal(52, piano.PuntiFeritaMax.Proposto);        // 38 + 10 + 4
    }

    [Fact]
    public void Gli_slot_sono_assoluti_e_lunghi_nove()
    {
        var mago = Pg(livello: 2, classe: "Mago");
        var piano = LevelUpPlanner.Pianifica(mago, TabellaMago, null, null, null)!;

        Assert.Equal(9, piano.SlotMax.Proposto.Count);
        Assert.Equal(4, piano.SlotMax.Proposto[0]);
        Assert.Equal(2, piano.SlotMax.Proposto[1]);
        Assert.Equal(0, piano.SlotMax.Proposto[8]);
    }

    [Fact]
    public void Il_cerchio_che_si_apre_per_la_prima_volta_viene_segnalato()
    {
        var mago = Pg(livello: 2, classe: "Mago");
        mago.SpellSlots1Max = 3;

        var piano = LevelUpPlanner.Pianifica(mago, TabellaMago, null, null, null)!;

        Assert.Equal(2, piano.CerchioSbloccato);                // il 2° cerchio da 0 a 2
    }

    [Fact]
    public void La_caratteristica_da_incantatore_si_propone_solo_se_manca()
    {
        var mago = Pg(livello: 2, classe: "Mago");
        var piano = LevelUpPlanner.Pianifica(mago, TabellaMago, null, null, null)!;
        Assert.Equal("intelligence", piano.CaratteristicaIncantatore.Proposto);

        mago.SpellcastingAbility = "wisdom";                    // scelta del tavolo: non si tocca
        var secondo = LevelUpPlanner.Pianifica(mago, TabellaMago, null, null, null)!;
        Assert.Equal("wisdom", secondo.CaratteristicaIncantatore.Proposto);
    }

    [Fact]
    public void Il_livello_che_porta_la_sottoclasse_apre_la_scelta()
    {
        var pg = Pg(livello: 2);
        var sottoclassi = new List<PackageSubclass>
        {
            new() { Id = "x/berserker", Name = "Cammino del berserker", Description = "Furia." }
        };

        var piano = LevelUpPlanner.Pianifica(pg, TabellaBarbaro, sottoclassi, null, null)!;

        var scelta = Assert.IsType<DecisioneFraOpzioni>(Assert.Single(piano.Decisioni));
        Assert.Equal("L3:sottoclasse", scelta.Chiave);
        Assert.Equal("Cammino del berserker", Assert.Single(scelta.Opzioni).Nome);
    }

    [Fact]
    public void Una_sottoclasse_gia_scelta_non_viene_richiesta_di_nuovo()
    {
        var pg = Pg(livello: 2);
        pg.Subclass = "Cammino di casa nostra";                 // scritta a mano dal tavolo

        var piano = LevelUpPlanner.Pianifica(pg, TabellaBarbaro, null, null, null)!;

        Assert.DoesNotContain(piano.Decisioni, d => d.Chiave == "L3:sottoclasse");
    }

    [Fact]
    public void Il_talento_dell_incremento_apre_la_scelta_dei_punteggi()
    {
        var pg = Pg(livello: 3);
        var talenti = new List<PackageFeat>
        {
            new() { Id = "t/asi", Name = "Incremento del Punteggio di Caratteristica",
                    Category = "Generale", Description = "..." },
            new() { Id = "t/lottatore", Name = "Lottatore", Category = "Generale", Description = "..." }
        };

        var senzaRisposta = LevelUpPlanner.Pianifica(pg, TabellaBarbaro, null, talenti, null)!;
        Assert.Single(senzaRisposta.Decisioni);                 // solo la scelta del talento

        var risposte = new Dictionary<string, Risposta>
        {
            ["L4:talento"] = new() { Scelte = new[] { "Incremento del Punteggio di Caratteristica" } }
        };
        var conRisposta = LevelUpPlanner.Pianifica(pg, TabellaBarbaro, null, talenti, risposte)!;

        Assert.Contains(conRisposta.Decisioni, d => d is DecisionePunteggi && d.Chiave == "L4:talento/punteggi");
    }

    private static List<PackageFeat> TalentoAsi() => new()
    {
        new() { Id = "t/asi", Name = "Incremento del Punteggio di Caratteristica",
                Category = "Generale", Description = "..." }
    };

    [Fact]
    public void ApplicaPunteggio_non_supera_20()
    {
        // Una Costituzione già a 20 non deve diventare 22: CharacterNormalizer non clampa le
        // caratteristiche (gotcha nota del progetto), quindi il tetto deve stare qui.
        var pg = Pg(livello: 3, costituzione: 20);
        var risposte = new Dictionary<string, Risposta>
        {
            ["L4:talento"] = new() { Scelte = new[] { "Incremento del Punteggio di Caratteristica" } },
            ["L4:talento/punteggi"] = new()
            {
                Punteggi = new Dictionary<string, int> { ["constitution"] = 2 }
            }
        };

        var piano = LevelUpPlanner.Pianifica(pg, TabellaBarbaro, null, TalentoAsi(), risposte)!;
        LevelUpPlanner.Applica(pg, piano, risposte);

        Assert.Equal(20, pg.Constitution);
    }

    [Fact]
    public void ApplicaPunteggio_non_riduce_un_punteggio_gia_sopra_20()
    {
        // Un punteggio può arrivare qui già sopra 20 (bonus di razza, Dono Epico): il tetto non
        // deve mai abbassarlo. Math.Min(22+2, 20) da solo scriverebbe 20 al posto di 22 — una
        // riduzione, non un incremento. Regressione trovata dal gate su questa stessa correzione.
        var pg = Pg(livello: 3, costituzione: 22);
        var risposte = new Dictionary<string, Risposta>
        {
            ["L4:talento"] = new() { Scelte = new[] { "Incremento del Punteggio di Caratteristica" } },
            ["L4:talento/punteggi"] = new()
            {
                Punteggi = new Dictionary<string, int> { ["constitution"] = 2 }
            }
        };

        var piano = LevelUpPlanner.Pianifica(pg, TabellaBarbaro, null, TalentoAsi(), risposte)!;
        LevelUpPlanner.Applica(pg, piano, risposte);

        Assert.Equal(22, pg.Constitution);   // invariata, non 20
    }

    [Fact]
    public void I_punti_ferita_non_calano_se_la_costituzione_e_gia_sopra_20()
    {
        // Stessa regressione vista dal lato di Pianifica: senza il Math.Max nel clamp, il
        // retroattivo diventerebbe negativo e i PF massimi mostrati calerebbero rispetto a non
        // aver fatto nulla — un level-up che PEGGIORA il personaggio.
        var pg = Pg(livello: 3, costituzione: 22);
        var risposte = new Dictionary<string, Risposta>
        {
            ["L4:talento"] = new() { Scelte = new[] { "Incremento del Punteggio di Caratteristica" } },
            ["L4:talento/punteggi"] = new()
            {
                Punteggi = new Dictionary<string, int> { ["constitution"] = 2 }
            }
        };

        var piano = LevelUpPlanner.Pianifica(pg, TabellaBarbaro, null, TalentoAsi(), risposte)!;

        // Il modificatore resta +6 (mod di 22) prima e dopo: nessun retroattivo, guadagno normale.
        Assert.Equal(38 + 7 + 6, piano.PuntiFeritaMax.Proposto);
    }

    [Fact]
    public void I_punti_ferita_non_usano_il_modificatore_di_un_punteggio_oltre_20()
    {
        // Costituzione già a 20 (mod +5): un altro +2 non deve valere +6 (mod di 22) nel calcolo
        // dei punti ferita mostrati, anche se il punteggio scritto viene clampato correttamente.
        var pg = Pg(livello: 3, costituzione: 20);
        var risposte = new Dictionary<string, Risposta>
        {
            ["L4:talento"] = new() { Scelte = new[] { "Incremento del Punteggio di Caratteristica" } },
            ["L4:talento/punteggi"] = new()
            {
                Punteggi = new Dictionary<string, int> { ["constitution"] = 2 }
            }
        };

        var piano = LevelUpPlanner.Pianifica(pg, TabellaBarbaro, null, TalentoAsi(), risposte)!;

        // media d12 (7) + mod Costituzione clampato a 20 (+5, invariato) = 12; niente retroattivo,
        // perché il modificatore non cambia. Senza il clamp sarebbe 7+6=13, retroattivo +1×3=3.
        Assert.Equal(38 + 12, piano.PuntiFeritaMax.Proposto);
    }

    [Fact]
    public void Applica_due_volte_lo_stesso_piano_non_raddoppia_i_punteggi()
    {
        // Il genitore ritenta Applica sullo stesso Character già mutato (es. dopo un errore di
        // rete su UpdateCharacterAsync): Level non è più LivelloDa, e la guardia deve bloccare.
        var pg = Pg(livello: 3, costituzione: 15);
        var risposte = new Dictionary<string, Risposta>
        {
            ["L4:talento"] = new() { Scelte = new[] { "Incremento del Punteggio di Caratteristica" } },
            ["L4:talento/punteggi"] = new()
            {
                Punteggi = new Dictionary<string, int> { ["constitution"] = 2 }
            }
        };

        var piano = LevelUpPlanner.Pianifica(pg, TabellaBarbaro, null, TalentoAsi(), risposte)!;
        LevelUpPlanner.Applica(pg, piano, risposte);
        Assert.Equal(17, pg.Constitution);
        Assert.Equal(4, pg.Level);

        LevelUpPlanner.Applica(pg, piano, risposte);

        Assert.Equal(17, pg.Constitution);   // non 19
        Assert.Equal(4, pg.Level);           // non 5
    }

    [Fact]
    public void Senza_catalogo_talenti_la_scelta_diventa_libera_e_non_blocca()
    {
        // CatalogService.GetPackageAsync torna null su rete assente → Feats è null/vuoto. La
        // scelta non deve più bloccare la conferma per sempre.
        var pg = Pg(livello: 3);   // L4 in TabellaBarbaro porta "Incremento punteggio caratteristica"

        var senzaCatalogo = LevelUpPlanner.Pianifica(pg, TabellaBarbaro, null, null, null)!;
        var decisione = Assert.IsType<DecisioneLibera>(Assert.Single(senzaCatalogo.Decisioni));
        Assert.Equal("L4:talento", decisione.Chiave);
        Assert.True(senzaCatalogo.Completo(null));

        var conListaVuota = LevelUpPlanner.Pianifica(pg, TabellaBarbaro, null, Array.Empty<PackageFeat>(), null)!;
        Assert.IsType<DecisioneLibera>(Assert.Single(conListaVuota.Decisioni));
        Assert.True(conListaVuota.Completo(null));
    }

    [Fact]
    public void Due_privilegi_da_scegliere_sullo_stesso_livello_hanno_chiavi_diverse()
    {
        // Classe del tavolo: "Features" è testo libero e può portare due privilegi "da talento"
        // sullo stesso livello (proprio l'esempio del finding), cosa che lo SRD non fa.
        const string tabella = "L1 — Ira\nL4 — Incremento punteggio caratteristica, Stile di combattimento";
        var pg = Pg(livello: 3, costituzione: 15);
        var talenti = new List<PackageFeat>
        {
            new() { Id = "t/asi", Name = "Incremento del Punteggio di Caratteristica",
                    Category = "Generale", Description = "..." },
            new() { Id = "t/duello", Name = "Combattimento in duello",
                    Category = "Stile di combattimento", Description = "..." }
        };

        var senzaRisposte = LevelUpPlanner.Pianifica(pg, tabella, null, talenti, null)!;
        var scelte = senzaRisposte.Decisioni.OfType<DecisioneFraOpzioni>().ToList();
        Assert.Equal(2, scelte.Count);
        Assert.Equal(2, scelte.Select(d => d.Chiave).Distinct().Count());   // chiavi diverse
        Assert.Contains(scelte, d => d.Chiave == "L4:talento");            // la prima resta invariata
        Assert.False(senzaRisposte.Completo(null));                        // entrambe richieste

        var risposte = new Dictionary<string, Risposta>
        {
            [scelte[0].Chiave] = new() { Scelte = new[] { scelte[0].Opzioni[0].Nome } },   // Incremento
            [scelte[1].Chiave] = new() { Scelte = new[] { scelte[1].Opzioni[0].Nome } }    // Stile di combattimento
        };

        // La scelta dell'incremento apre anche la sua figlia dei punteggi: senza rispondere pure a
        // quella, il piano resta incompleto — non basta rispondere alle due scelte da elenco.
        var conScelte = LevelUpPlanner.Pianifica(pg, tabella, null, talenti, risposte)!;
        Assert.False(conScelte.Completo(risposte));
        var figlia = Assert.Single(conScelte.Decisioni.OfType<DecisionePunteggi>());
        Assert.Equal($"{scelte[0].Chiave}/punteggi", figlia.Chiave);

        risposte[figlia.Chiave] = new() { Punteggi = new Dictionary<string, int> { ["constitution"] = 2 } };

        var conRisposte = LevelUpPlanner.Pianifica(pg, tabella, null, talenti, risposte)!;
        Assert.True(conRisposte.Completo(risposte));

        LevelUpPlanner.Applica(pg, conRisposte, risposte);

        Assert.Contains("Incremento del Punteggio di Caratteristica", pg.Feats);
        Assert.Contains("Combattimento in duello", pg.Feats);
        Assert.Equal(17, pg.Constitution);
    }

    [Fact]
    public void La_figlia_dei_punteggi_si_aggancia_al_talento_giusto_anche_se_non_e_il_primo()
    {
        // L'incremento è il SECONDO privilegio del livello: la figlia dei punteggi deve trovarlo
        // comunque, non solo quando la scelta dell'incremento è la prima (chiave "L{n}:talento").
        const string tabella = "L1 — Ira\nL4 — Stile di combattimento, Incremento punteggio caratteristica";
        var pg = Pg(livello: 3);
        var talenti = new List<PackageFeat>
        {
            new() { Id = "t/duello", Name = "Combattimento in duello",
                    Category = "Stile di combattimento", Description = "..." },
            new() { Id = "t/asi", Name = "Incremento del Punteggio di Caratteristica",
                    Category = "Generale", Description = "..." }
        };

        var senzaRisposte = LevelUpPlanner.Pianifica(pg, tabella, null, talenti, null)!;
        var scelte = senzaRisposte.Decisioni.OfType<DecisioneFraOpzioni>().ToList();
        var chiaveStile = scelte.Single(d => d.Titolo == "Stile di combattimento").Chiave;
        var chiaveAsi = scelte.Single(d => d.Titolo == "Incremento punteggio caratteristica").Chiave;
        Assert.NotEqual(chiaveStile, chiaveAsi);
        Assert.Equal("L4:talento", chiaveStile);   // il primo del livello: chiave invariata

        var risposte = new Dictionary<string, Risposta>
        {
            [chiaveStile] = new() { Scelte = new[] { "Combattimento in duello" } },
            [chiaveAsi] = new() { Scelte = new[] { "Incremento del Punteggio di Caratteristica" } }
        };

        var piano = LevelUpPlanner.Pianifica(pg, tabella, null, talenti, risposte)!;

        Assert.Contains(piano.Decisioni, d => d is DecisionePunteggi && d.Chiave == $"{chiaveAsi}/punteggi");
        Assert.DoesNotContain(piano.Decisioni, d => d is DecisionePunteggi && d.Chiave == $"{chiaveStile}/punteggi");
    }

    [Fact]
    public void Il_dado_di_catalogo_vince_se_HitDiceMax_e_vuoto()
    {
        // Senza il dado di catalogo, HitDiceMax vuoto ripiegherebbe su d8 (media 5, avviso).
        // Col dado di catalogo, la media è quella del d12 (7) e non c'è nulla da segnalare.
        var pg = Pg();
        pg.HitDiceMax = "";

        var piano = LevelUpPlanner.Pianifica(pg, TabellaBarbaro, null, null, null, dadoVitaClasse: "d12")!;

        Assert.Equal(48, piano.PuntiFeritaMax.Proposto);        // 38 + media d12 (7) + 3 di Costituzione
        Assert.Empty(piano.Avvisi);
    }

    [Fact]
    public void Il_dado_di_catalogo_vince_anche_su_un_HitDiceMax_sbagliato()
    {
        // "4d6" è sbagliato per un Barbaro (dovrebbe essere d12): il dado di catalogo prevale
        // comunque, perché è la fonte che sa davvero di quale classe si tratta.
        var pg = Pg();
        pg.HitDiceMax = "4d6";

        var piano = LevelUpPlanner.Pianifica(pg, TabellaBarbaro, null, null, null, dadoVitaClasse: "d12")!;

        Assert.Equal("d12", piano.DadoVita);
        Assert.Equal(48, piano.PuntiFeritaMax.Proposto);        // media d12 (7), non d6 (4)
        Assert.Empty(piano.Avvisi);
    }

    [Fact]
    public void Senza_dado_di_catalogo_il_comportamento_e_quello_di_prima()
    {
        var pg = Pg();

        var conNullEsplicito = LevelUpPlanner.Pianifica(pg, TabellaBarbaro, null, null, null, dadoVitaClasse: null)!;
        var senzaParametro = LevelUpPlanner.Pianifica(pg, TabellaBarbaro, null, null, null)!;

        Assert.Equal(senzaParametro.DadoVita, conNullEsplicito.DadoVita);
        Assert.Equal(senzaParametro.PuntiFeritaMax.Proposto, conNullEsplicito.PuntiFeritaMax.Proposto);
        Assert.Equal(senzaParametro.Avvisi, conNullEsplicito.Avvisi);
    }

    [Fact]
    public void I_dadi_vita_multiclasse_non_si_toccano_e_si_avvisa()
    {
        var pg = Pg();
        pg.HitDiceMax = "3d12+1d8";

        var piano = LevelUpPlanner.Pianifica(pg, TabellaBarbaro, null, null, null)!;

        Assert.Equal("3d12+1d8", piano.DadiVita.Proposto);      // invariato
        Assert.NotEmpty(piano.Avvisi);
    }

    /// <summary>Le sole proprietà che <see cref="LevelUpPlanner.Applica"/> può scrivere — i sei
    /// punteggi compresi, perché una <see cref="DecisionePunteggi"/> può toccare qualunque delle
    /// sei. Confrontata campo a campo con la riflessione qui sotto: un'assegnazione accidentale su
    /// una qualunque ALTRA colonna (<c>TempHitPoints</c>, <c>HitDiceSpent</c>, <c>Speed</c>,
    /// <c>ArmorClass</c>, ...) deve far fallire il test, non arrivare in produzione — "il test più
    /// importante del progetto".</summary>
    private static readonly HashSet<string> ProprietaAmmesseDaApplica = new(StringComparer.Ordinal)
    {
        nameof(Character.Level), nameof(Character.MaxHitPoints), nameof(Character.HitPoints),
        nameof(Character.HitDiceMax),
        nameof(Character.SpellSlots1Max), nameof(Character.SpellSlots2Max), nameof(Character.SpellSlots3Max),
        nameof(Character.SpellSlots4Max), nameof(Character.SpellSlots5Max), nameof(Character.SpellSlots6Max),
        nameof(Character.SpellSlots7Max), nameof(Character.SpellSlots8Max), nameof(Character.SpellSlots9Max),
        nameof(Character.Subclass), nameof(Character.SpellcastingAbility),
        nameof(Character.ClassFeatures), nameof(Character.Feats),
        nameof(Character.Strength), nameof(Character.Dexterity), nameof(Character.Constitution),
        nameof(Character.Intelligence), nameof(Character.Wisdom), nameof(Character.Charisma),
    };

    [Fact]
    public void Applica_tocca_solo_i_campi_dichiarati()
    {
        var pg = Pg();
        pg.SpellSlots1Used = 2;
        pg.GoldPieces = 120;
        pg.ArmorClass = 16;
        pg.Strength = 18;

        // Snapshot di TUTTE le proprietà dichiarate su Character (BindingFlags.DeclaredOnly:
        // esclude quelle di BaseModel, che Applica non tocca e non deve). Non a campione: un test
        // che ne controllasse solo quattro scelte a mano lascerebbe passare una scrittura
        // accidentale su una qualunque delle altre.
        var proprieta = typeof(Character)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        var prima = proprieta.ToDictionary(p => p.Name, p => p.GetValue(pg));

        var piano = LevelUpPlanner.Pianifica(pg, TabellaBarbaro, null, null, null)!;
        LevelUpPlanner.Applica(pg, piano, null);

        // Applica chiude con CharacterNormalizer.Normalize, che tocca anche fuori whitelist (Size
        // → "Media" se vuoto, trim dei testi, clamp dei numerici a 0/1..20). Qui non fa differenza
        // perché Pg() parte già ai valori di riposo del normalizzatore — lo si annota qui invece di
        // scoprirlo in produzione, come richiesto: se in futuro questo test iniziasse a fallire su
        // uno di questi campi per colpa di Normalize e non di Applica, è un falso positivo noto.
        foreach (var p in proprieta)
        {
            var attesoInvariato = !ProprietaAmmesseDaApplica.Contains(p.Name);
            if (!attesoInvariato) continue;

            var valorePrima = prima[p.Name];
            var valoreDopo = p.GetValue(pg);
            Assert.True(Equals(valorePrima, valoreDopo),
                $"{p.Name} è cambiato da '{valorePrima}' a '{valoreDopo}' ma non è nella whitelist di Applica.");
        }

        // I campi non toccati restano dati vivi, non sovrascritti dal piano.
        Assert.Equal(2, pg.SpellSlots1Used);
        Assert.Equal(120, pg.GoldPieces);
        Assert.Equal(16, pg.ArmorClass);
        Assert.Equal(18, pg.Strength);
        Assert.Equal(5, pg.Level);                 // questo sì è nella whitelist
    }

    [Fact]
    public void Applica_con_risposte_incomplete_non_muta_nulla()
    {
        var pg = Pg(livello: 2);
        var sottoclassi = new List<PackageSubclass>
        {
            new() { Id = "x/b", Name = "Cammino del berserker", Description = "Furia." }
        };
        var piano = LevelUpPlanner.Pianifica(pg, TabellaBarbaro, sottoclassi, null, null)!;

        Assert.False(piano.Completo(null));
        LevelUpPlanner.Applica(pg, piano, null);

        Assert.Equal(2, pg.Level);                 // niente è cambiato
        Assert.Equal(38, pg.MaxHitPoints);
    }

    [Fact]
    public void I_privilegi_passivi_non_si_appendono_ma_le_annotazioni_libere_si()
    {
        // I privilegi passivi NON si appendono più a ClassFeatures: CharacterBioTab.razor li deriva
        // già dalla stessa tabella (ClassProgression.PrivilegiFinoAl sullo stesso testo che il
        // planner riceve) — appenderli anche qui li duplicherebbe. Le annotazioni libere restano
        // l'unica cosa che finisce lì, perché non sono derivabili dalla tabella.
        const string tabella = "L1 — Ira\nL5 — Attacco extra, Invocazioni occulte";
        var pg = Pg(livello: 4);
        pg.ClassFeatures = "L1: Ira";

        var senzaRisposte = LevelUpPlanner.Pianifica(pg, tabella, null, null, null)!;
        var libera = Assert.IsType<DecisioneLibera>(Assert.Single(senzaRisposte.Decisioni));

        var risposte = new Dictionary<string, Risposta> { [libera.Chiave] = new() { Testo = "Occhio del profondo" } };
        var piano = LevelUpPlanner.Pianifica(pg, tabella, null, null, risposte)!;

        LevelUpPlanner.Applica(pg, piano, risposte);

        Assert.Contains("L1: Ira", pg.ClassFeatures);                    // il testo preesistente resta
        Assert.DoesNotContain("Attacco extra", pg.ClassFeatures!);       // passivo: la scheda lo mostra già
        Assert.Contains("L5: Occhio del profondo", pg.ClassFeatures!);   // annotazione libera
    }

    [Fact]
    public void La_sottoclasse_annotata_a_mano_finisce_in_Subclass_non_in_ClassFeatures()
    {
        // Nessun catalogo di sottoclassi passato: CostruisciDecisioni emette una DecisioneLibera
        // con chiave "L3:sottoclasse". Il nome scritto dal giocatore deve finire nel campo dedicato
        // che la scheda mostra, non appeso come un privilegio qualsiasi.
        var pg = Pg(livello: 2);
        var piano = LevelUpPlanner.Pianifica(pg, TabellaBarbaro, null, null, null)!;
        Assert.IsType<DecisioneLibera>(Assert.Single(piano.Decisioni, d => d.Chiave == "L3:sottoclasse"));

        var risposte = new Dictionary<string, Risposta>
        {
            ["L3:sottoclasse"] = new() { Testo = "Cammino del sale" }
        };

        LevelUpPlanner.Applica(pg, piano, risposte);

        Assert.Equal("Cammino del sale", pg.Subclass);
        Assert.DoesNotContain("Cammino del sale", pg.ClassFeatures ?? string.Empty);
    }

    [Fact]
    public void Applica_e_un_punto_fisso_di_Normalize()
    {
        // Se Normalize cambiasse qualcosa dopo Applica, il diff mostrato all'utente sarebbe stato
        // smentito subito dopo la conferma, in silenzio.
        var pg = Pg();
        var piano = LevelUpPlanner.Pianifica(pg, TabellaBarbaro, null, null, null)!;
        LevelUpPlanner.Applica(pg, piano, null);

        var pfDopoApplica = pg.MaxHitPoints;
        var dadiDopoApplica = pg.HitDiceMax;
        CharacterNormalizer.Normalize(pg);

        Assert.Equal(pfDopoApplica, pg.MaxHitPoints);
        Assert.Equal(dadiDopoApplica, pg.HitDiceMax);
    }

    [Fact]
    public void Il_giro_completo_su_un_mago_reale_dal_pacchetto()
    {
        // End-to-end: attraversa contratti, regole, planner e i dati veri. Nessuna delle altre
        // fette lo scriverebbe, perché nessuna li vede tutti insieme.
        var pacchetto = CaricaPacchetto();
        var mago = pacchetto.Classes.Single(c => c.Name == "Mago");
        var tabella = ClassProgression.Serializza(mago.Levels);

        var pg = new Character
        {
            Id = "pg-2", Name = "Elminster", Class = "Mago", Level = 2,
            Constitution = 14, MaxHitPoints = 13, HitPoints = 13, HitDiceMax = "2d6"
        };

        var piano = LevelUpPlanner.Pianifica(pg, tabella, mago.Subclasses, pacchetto.Feats, null)!;

        // Il Mago prende la sottoclasse al 3° («Tradizione arcana» nel pacchetto): finché non è
        // scelta, il piano non è confermabile.
        Assert.False(piano.Completo(null));
        Assert.Contains(piano.Decisioni, d => d.Chiave == "L3:sottoclasse");

        var risposte = new Dictionary<string, Risposta>
        {
            ["L3:sottoclasse"] = new() { Scelte = new[] { "Invocatore" } }
        };
        var conScelta = LevelUpPlanner.Pianifica(pg, tabella, mago.Subclasses, pacchetto.Feats, risposte)!;
        Assert.True(conScelta.Completo(risposte));

        LevelUpPlanner.Applica(pg, conScelta, risposte);

        Assert.Equal(3, pg.Level);
        Assert.Equal("3d6", pg.HitDiceMax);
        Assert.Equal(19, pg.MaxHitPoints);          // 13 + media d6 (4) + 2 di Costituzione
        Assert.Equal("Invocatore", pg.Subclass);
        Assert.Equal("intelligence", pg.SpellcastingAbility);
        Assert.Equal(4, pg.SpellSlots1Max);
        Assert.Equal(2, pg.SpellSlots2Max);
        Assert.Equal(0, pg.SpellSlots3Max);
    }
}
