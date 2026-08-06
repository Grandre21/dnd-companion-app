using DndCompanion.Models;
using DndCompanion.Models.Packages;

namespace DndCompanion.Services;

/// <summary>Il motore del level-up guidato: pianifica il diff e lo applica. Helper puro `static`,
/// nessuno stato, nessuna I/O.
///
/// <see cref="Pianifica"/> non è stabile: va richiamata a ogni risposta, perché un incremento di
/// Costituzione cambia i punti ferita già maturati. <see cref="Applica"/> è separata apposta — così
/// il dialogo può mostrare il piano più e più volte senza toccare la scheda finché il giocatore non
/// conferma — e si fida della whitelist e della validazione già fatte da
/// <see cref="LevelUpPlan.Completo"/>, senza riverificarle.</summary>
public static class LevelUpPlanner
{
    /// <summary>Costruisce il piano per salire di un livello, viste le risposte date finora.
    /// Null se questa classe non ha una tabella di progressione da cui leggere, o se il personaggio
    /// è già al livello massimo: in nessuno dei due casi c'è un piano sensato da proporre.
    ///
    /// <paramref name="dadoVitaClasse"/> è il dado della classe letto dal catalogo
    /// (<c>CharacterClass.HitDie</c> / <c>PackageClass.HitDie</c>, es. <c>"d12"</c>) e, quando è
    /// valorizzato e riconoscibile, VINCE su <see cref="Character.HitDiceMax"/>: quel campo può
    /// essere vuoto o scritto in una forma strana, e il ripiego silenzioso su d8 presenterebbe un
    /// numero sbagliato come "calcolato dalle regole" — un Barbaro si vedrebbe proporre punti ferita
    /// da d8 invece che da d12. Parametro opzionale e in fondo apposta: le chiamate esistenti (i
    /// test, prima di questa fetta) restano valide senza modifiche.</summary>
    public static LevelUpPlan? Pianifica(
        Character pg,
        string? testoProgressione,
        IReadOnlyList<PackageSubclass>? sottoclassi,
        IReadOnlyList<PackageFeat>? talenti,
        IReadOnlyDictionary<string, Risposta>? risposte,
        int? tiroPuntiFerita = null,
        string? dadoVitaClasse = null)
    {
        if (pg.Level >= 20) return null;
        var livelloA = pg.Level + 1;

        var righe = ClassProgression.Leggi(testoProgressione);
        if (righe.Count == 0) return null;

        // Assente = nessun privilegio a questo livello, non un errore: una tabella può avere un
        // livello di soli slot (v. ClassProgression.PrivilegiFinoAl) o essere parziale.
        var rigaNuova = righe.FirstOrDefault(r => r.Livello == livelloA);
        var privilegi = rigaNuova?.Privilegi ?? Array.Empty<string>();

        var avvisi = new List<string>();

        // Il dado di catalogo vince quando è utilizzabile: è la fonte che sa davvero di quale
        // classe si tratta. Solo se non lo è (parametro assente o non riconoscibile) si ripiega sul
        // comportamento di prima — dedotto da HitDiceMax, con l'avviso se non si capisce nemmeno
        // quello. L'avviso non deve comparire quando il catalogo ha già risolto la domanda.
        var (facceClasse, classeNonRiconosciuta) = FacceDado(dadoVitaClasse);
        int facce;
        bool facceNonRiconosciute;
        if (classeNonRiconosciuta)
            (facce, facceNonRiconosciute) = FacceDado(pg.HitDiceMax);
        else
            (facce, facceNonRiconosciute) = (facceClasse, false);

        if (facceNonRiconosciute)
            avvisi.Add("Dado vita non riconosciuto: uso d8 come stima.");

        var decisioni = CostruisciDecisioni(
            livelloA, privilegi, pg.Subclass, sottoclassi, talenti, risposte, PunteggiAttuali(pg));

        // Il retroattivo: l'incremento di Costituzione vale anche per i livelli già posseduti, non
        // solo per quello nuovo. Va letto dalle risposte, non dal personaggio: pg.Constitution non
        // è ancora stato toccato (Pianifica non muta nulla).
        //
        // Tetto a 20, come ApplicaPunteggio: senza clamparlo anche qui, un personaggio già a 20
        // vedrebbe punti ferita calcolati sul modificatore di 22 — un numero che Applica non
        // scriverà mai, perché il punteggio che scrive è clampato. Math.Max con pg.Constitution
        // impedisce che il clamp NEGHI l'incremento: FinalAbilityScores arriva fino a 30 (i bonus
        // di razza si applicano dopo il tetto a 20 del background, v. CharacterWizardLogic), e un
        // Dono Epico può spingere oltre 20 anche in gioco — una Costituzione già a 22 non deve
        // vedersi calare a 20, e senza il Max qui il retroattivo diventerebbe negativo.
        var incrementoCostituzione = IncrementoCostituzione(decisioni, risposte);
        var incrementoCostituzioneClampato =
            Math.Max(pg.Constitution, Math.Min(pg.Constitution + incrementoCostituzione, 20)) - pg.Constitution;
        var modCosPrima = CharacterCalculations.GetModifier(pg.Constitution);
        var modCosDopo = CharacterCalculations.GetModifier(pg.Constitution + incrementoCostituzioneClampato);

        var mediaDado = (facce / 2) + 1;
        var tiro = tiroPuntiFerita is null ? mediaDado : Math.Clamp(tiroPuntiFerita.Value, 1, facce);
        var guadagnoLivello = Math.Max(1, tiro + modCosDopo);
        var retroattivo = (modCosDopo - modCosPrima) * pg.Level;

        var puntiFeritaMax = pg.MaxHitPoints + guadagnoLivello + retroattivo;
        var puntiFeritaCorrenti = pg.HitPoints + (puntiFeritaMax - pg.MaxHitPoints);

        var hitDiceAttuale = pg.HitDiceMax ?? string.Empty;
        string hitDiceProposto;
        if (hitDiceAttuale.Contains('+'))
        {
            // Multiclasse: non sappiamo quale dado aggiungere al livello nuovo senza sapere DI
            // QUALE classe è — il campo mischia i dadi di tutte. Si lascia intatto e si avvisa.
            hitDiceProposto = hitDiceAttuale;
            avvisi.Add("Dadi vita da più classi: aggiornali a mano.");
        }
        else
        {
            hitDiceProposto = CharacterWizardLogic.BuildHitDice($"d{facce}", livelloA);
        }

        var slotAttuali = SlotDaPg(pg);
        var slotProposti = Nove(ClassProgression.SlotFinoAl(testoProgressione, livelloA));

        int? cerchioSbloccato = null;
        for (var i = 0; i < 9; i++)
        {
            if (slotProposti[i] > 0 && slotAttuali[i] == 0)
            {
                cerchioSbloccato = i + 1;
                break;
            }
        }

        var caratteristicaAttuale = pg.SpellcastingAbility ?? string.Empty;
        var caratteristicaProposta = !string.IsNullOrWhiteSpace(pg.SpellcastingAbility)
            ? pg.SpellcastingAbility!                                  // scelta del tavolo: non si tocca
            : LevelUpRules.CaratteristicaIncantatore(pg.Class) ?? string.Empty;

        return new LevelUpPlan(
            Classe: pg.Class,
            LivelloDa: pg.Level,
            LivelloA: livelloA,
            DadoVita: $"d{facce}",
            MediaDado: mediaDado,
            PuntiFeritaMax: new Proposta<int>(pg.MaxHitPoints, puntiFeritaMax),
            PuntiFeritaCorrenti: new Proposta<int>(pg.HitPoints, puntiFeritaCorrenti),
            DadiVita: new Proposta<string>(hitDiceAttuale, hitDiceProposto),
            SlotMax: new Proposta<IReadOnlyList<int>>(slotAttuali, slotProposti),
            CaratteristicaIncantatore: new Proposta<string>(caratteristicaAttuale, caratteristicaProposta),
            BonusCompetenza: new Proposta<int>(
                CharacterCalculations.GetProficiencyBonus(pg.Level),
                CharacterCalculations.GetProficiencyBonus(livelloA)),
            PrivilegiOttenuti: privilegi,
            Decisioni: decisioni,
            Avvisi: avvisi,
            CerchioSbloccato: cerchioSbloccato);
    }

    /// <summary>Scrive il piano sul personaggio — muta e restituisce lo stesso <see cref="Character"/>
    /// ricevuto, mai una copia: la scheda ne tiene un riferimento vivo, e una copia farebbe
    /// divergere i tab già aperti.
    ///
    /// Se il piano non è confermabile (<see cref="LevelUpPlan.Completo"/> falso) non tocca nulla:
    /// è la guardia che rende sicura la delega a questo metodo, non un extra. Una seconda guardia
    /// rende il metodo idempotente: livello e punti ferita sono valori assoluti presi dal piano (e
    /// <see cref="AppendUnica"/> dedupla le righe), ma i punteggi sono <c>+=</c> — se il genitore
    /// richiama <see cref="Applica"/> sullo stesso <see cref="Character"/> già mutato (es. dopo un
    /// errore di rete su <c>UpdateCharacterAsync</c> e un ritentativo), <c>Completo</c> resta vero e
    /// una caratteristica prenderebbe l'incremento una seconda volta.
    ///
    /// <para><b>Chiamante da conoscere prima di irrigidire questa guardia:</b> <see cref="CreationChain.Deriva"/>
    /// la elude DA FUORI apposta — costruisce un <see cref="LevelUpPlan"/> con SOLO le decisioni già
    /// risolte (<c>pianoDaApplicare</c>, filtrato dalle risposte valide) proprio per far passare
    /// <c>Completo</c> anche quando altre decisioni dello stesso livello restano aperte, perché lì il
    /// livello deve avanzare comunque. Un domani che rendesse questa guardia più severa (es. una
    /// verifica aggiuntiva che <c>CreationChain</c> non replica) romperebbe quel chiamante senza che
    /// nulla qui lo segnali.</para></summary>
    public static Character Applica(Character pg, LevelUpPlan piano, IReadOnlyDictionary<string, Risposta>? risposte)
    {
        if (!piano.Completo(risposte)) return pg;
        if (pg.Level != piano.LivelloDa) return pg;

        pg.Level = piano.LivelloA;
        pg.MaxHitPoints = piano.PuntiFeritaMax.Proposto;
        pg.HitPoints = piano.PuntiFeritaCorrenti.Proposto;
        pg.HitDiceMax = piano.DadiVita.Proposto;

        var slot = piano.SlotMax.Proposto;
        pg.SpellSlots1Max = slot[0];
        pg.SpellSlots2Max = slot[1];
        pg.SpellSlots3Max = slot[2];
        pg.SpellSlots4Max = slot[3];
        pg.SpellSlots5Max = slot[4];
        pg.SpellSlots6Max = slot[5];
        pg.SpellSlots7Max = slot[6];
        pg.SpellSlots8Max = slot[7];
        pg.SpellSlots9Max = slot[8];

        // Solo se era vuota: una CD già impostata dal tavolo è una scelta, non un difetto da
        // correggere silenziosamente ad ogni salita.
        if (string.IsNullOrWhiteSpace(pg.SpellcastingAbility))
            pg.SpellcastingAbility = piano.CaratteristicaIncantatore.Proposto;

        // I privilegi passivi NON si appendono a ClassFeatures: la scheda li deriva già dalla
        // stessa tabella (CharacterBioTab.razor → ClassProgression.PrivilegiFinoAl sullo stesso
        // ClassProgressionText che questo planner riceve). Appenderli qui li duplicherebbe, e il
        // secondo blocco finirebbe sotto un titolo falso ("privilegi annotati a mano") che cresce
        // a ogni livello. ClassFeatures resta per ciò che la tabella non contiene: le risposte
        // alle DecisioneLibera, scritte dal ramo delle decisioni qui sotto.
        foreach (var d in piano.Decisioni)
        {
            if (risposte is null || !risposte.TryGetValue(d.Chiave, out var r)) continue;

            switch (d)
            {
                case DecisioneFraOpzioni f when f.Chiave.EndsWith(":sottoclasse", StringComparison.Ordinal):
                    if (r.Scelte.Count > 0) pg.Subclass = r.Scelte[0];
                    break;

                // La sottoclasse annotata a mano (classe senza sottoclassi a catalogo) va nel
                // campo dedicato, non fra i privilegi: è lo stesso destino della scelta da elenco
                // qui sopra, solo con la fonte libera invece che da catalogo. Deve precedere il
                // ramo generale di DecisioneLibera più sotto, altrimenti non verrebbe mai raggiunto.
                case DecisioneLibera l when l.Chiave.EndsWith(":sottoclasse", StringComparison.Ordinal):
                    if (!string.IsNullOrWhiteSpace(r.Testo)) pg.Subclass = r.Testo;
                    break;

                case DecisioneFraOpzioni:
                    foreach (var nome in r.Scelte)
                        pg.Feats = AppendUnica(pg.Feats, $"L{piano.LivelloA}: {nome}");
                    break;

                case DecisionePunteggi:
                    foreach (var (chiave, valore) in r.Punteggi)
                        ApplicaPunteggio(pg, chiave, valore);
                    break;

                case DecisioneLibera when !string.IsNullOrWhiteSpace(r.Testo):
                    pg.ClassFeatures = AppendUnica(pg.ClassFeatures, $"L{piano.LivelloA}: {r.Testo}");
                    break;
            }
        }

        CharacterNormalizer.Normalize(pg);
        return pg;
    }

    // ---------------------------------------------------------------
    // Helper privati
    // ---------------------------------------------------------------

    /// <summary>Le decisioni aperte dai privilegi del livello nuovo. L'ordine dei privilegi nella
    /// tabella si rispetta: è lo stesso ordine in cui il manuale li elenca.</summary>
    private static List<Decisione> CostruisciDecisioni(
        int livelloA,
        IReadOnlyList<string> privilegi,
        string? subclassCorrente,
        IReadOnlyList<PackageSubclass>? sottoclassi,
        IReadOnlyList<PackageFeat>? talenti,
        IReadOnlyDictionary<string, Risposta>? risposte,
        IReadOnlyDictionary<string, int> punteggiAttuali)
    {
        var decisioni = new List<Decisione>();
        var chiaveTalentoBase = $"L{livelloA}:talento";

        foreach (var privilegio in privilegi)
        {
            var tipo = LevelUpRules.TipoDi(privilegio);
            switch (tipo)
            {
                case TipoDiScelta.Sottoclasse:
                    // Già scelta (anche a mano): niente da chiedere di nuovo. Vale anche per gli
                    // echi della sottoclasse ai livelli successivi al 3° ("Privilegio di
                    // sottoclasse"), che altrimenti la riproporrebbero da capo.
                    if (!string.IsNullOrWhiteSpace(subclassCorrente)) break;

                    if (sottoclassi is { Count: > 0 })
                    {
                        var opzioni = sottoclassi
                            .Select(s => new OpzioneDecisione(s.Name, s.Description))
                            .ToList();
                        decisioni.Add(new DecisioneFraOpzioni($"L{livelloA}:sottoclasse", privilegio, opzioni, 1));
                    }
                    else
                    {
                        decisioni.Add(new DecisioneLibera($"L{livelloA}:sottoclasse", privilegio,
                            "Il manuale non ha un elenco di sottoclassi per questa classe: annota qui la tua scelta."));
                    }
                    break;

                case TipoDiScelta.TalentoGenerale:
                case TipoDiScelta.StileDiCombattimento:
                case TipoDiScelta.DonoEpico:
                {
                    // Chiave: "L{n}:talento" per la prima scelta "da talento" del livello — i test
                    // esistenti la usano così, e resta invariata nel caso comune (un solo
                    // privilegio di questo tipo per livello). Da un eventuale secondo privilegio
                    // in poi si aggiunge un discriminante stabile: con lo SRD non capita, ma capita
                    // con una tabella del tavolo che porta, per esempio, sia l'incremento di
                    // punteggio sia uno stile di combattimento sullo stesso livello — altrimenti le
                    // due decisioni condividerebbero la stessa risposta e una scelta sparirebbe.
                    var precedenti = decisioni.Count(d =>
                        d.Chiave == chiaveTalentoBase
                        || d.Chiave.StartsWith(chiaveTalentoBase + "/", StringComparison.Ordinal));
                    var chiave = precedenti == 0
                        ? chiaveTalentoBase
                        : $"{chiaveTalentoBase}/{CatalogKey.NormalizeName(privilegio)}";

                    var categoria = LevelUpRules.CategoriaPerScelta(tipo);
                    var opzioni = (talenti ?? Array.Empty<PackageFeat>())
                        .Where(t => t.Category == categoria)
                        .Select(t => new OpzioneDecisione(t.Name, t.Description))
                        .ToList();

                    // Catalogo assente (rete giù: CatalogService.GetPackageAsync torna null e
                    // Feats è vuoto) → nessuna opzione da proporre. Una DecisioneFraOpzioni senza
                    // opzioni bloccherebbe la conferma per sempre, perché Completo la richiede
                    // sempre soddisfatta: si ripiega sulla stessa forma libera già usata sopra per
                    // la sottoclasse senza catalogo.
                    decisioni.Add(opzioni.Count == 0
                        ? new DecisioneLibera(chiave, privilegio,
                            "Il catalogo dei talenti non è disponibile: annota qui la tua scelta.")
                        : new DecisioneFraOpzioni(chiave, privilegio, opzioni, 1));
                    break;
                }

                case TipoDiScelta.Libera:
                    decisioni.Add(new DecisioneLibera(
                        $"L{livelloA}:libera/{CatalogKey.NormalizeName(privilegio)}", privilegio,
                        "Il manuale non porta l'elenco: annota qui la tua scelta."));
                    break;

                case TipoDiScelta.Nessuna:
                default:
                    break;
            }
        }

        // Figlia dei punteggi: solo se una delle scelte "da talento" di questo livello è già stata
        // risposta ed è proprio quella dell'incremento — non a ogni scelta di talento, che nella
        // maggioranza dei casi non tocca le caratteristiche. Va cercata fra TUTTE le decisioni "da
        // talento" del livello, non solo la prima con la chiave base: con due privilegi di questo
        // tipo sullo stesso livello (v. sopra) la scelta dell'incremento può essere la seconda, con
        // la chiave discriminata.
        if (risposte is not null)
        {
            // Raccolte a parte e aggiunte dopo: decisioni è la stessa lista su cui si sta
            // iterando, e aggiungerci direttamente dentro il foreach romperebbe l'enumerazione.
            var figlie = new List<Decisione>();
            foreach (var d in decisioni)
            {
                if (d is not DecisioneFraOpzioni f) continue;
                if (f.Chiave != chiaveTalentoBase
                    && !f.Chiave.StartsWith(chiaveTalentoBase + "/", StringComparison.Ordinal)) continue;
                if (!risposte.TryGetValue(f.Chiave, out var rispostaTalento) || rispostaTalento.Scelte.Count == 0)
                    continue;

                var nomeScelto = CatalogKey.NormalizeName(rispostaTalento.Scelte[0]);
                var talentoScelto = (talenti ?? Array.Empty<PackageFeat>())
                    .FirstOrDefault(t => CatalogKey.NormalizeName(t.Name) == nomeScelto);

                if (LevelUpRules.ÈTalentoDiIncremento(talentoScelto))
                    figlie.Add(new DecisionePunteggi($"{f.Chiave}/punteggi",
                        "Ripartisci l'incremento delle caratteristiche", punteggiAttuali));
            }
            decisioni.AddRange(figlie);
        }

        return decisioni;
    }

    /// <summary>La somma degli incrementi di Costituzione già decisi in questo piano: serve al
    /// calcolo dei punti ferita, che deve vederli PRIMA di sapere se il piano è confermabile.</summary>
    private static int IncrementoCostituzione(
        IReadOnlyList<Decisione> decisioni, IReadOnlyDictionary<string, Risposta>? risposte)
    {
        if (risposte is null) return 0;

        var incremento = 0;
        foreach (var d in decisioni)
        {
            if (d is DecisionePunteggi dp
                && risposte.TryGetValue(dp.Chiave, out var r)
                && r.Punteggi.TryGetValue("constitution", out var v))
            {
                incremento += v;
            }
        }
        return incremento;
    }

    /// <summary>Tetto a 20, come <see cref="CharacterWizardLogic.ApplyBackgroundBonuses"/>:
    /// <see cref="CharacterNormalizer"/> non clampa le caratteristiche, quindi senza questo tetto
    /// qui una Costituzione già a 20 salirebbe a 22 — e <see cref="Pianifica"/> avrebbe già usato
    /// il modificatore di 20, non di 22, per i punti ferita mostrati.
    ///
    /// Il <see cref="Math.Max(int,int)"/> con il valore attuale è necessario perché il tetto non
    /// deve mai ABBASSARE un punteggio: un personaggio può arrivare qui già sopra 20 (bonus di
    /// razza clampano a 30 in <see cref="CharacterWizardLogic.FinalAbilityScores"/>, e un Dono
    /// Epico può spingere una caratteristica oltre 20 prima di un ulteriore incremento) — senza il
    /// Max, <c>Math.Min(22 + 2, 20)</c> scriverebbe 20 al posto di 22, cioè una riduzione, non un
    /// incremento.</summary>
    private static void ApplicaPunteggio(Character pg, string chiave, int valore)
    {
        switch (chiave)
        {
            case "strength": pg.Strength = Math.Max(pg.Strength, Math.Min(pg.Strength + valore, 20)); break;
            case "dexterity": pg.Dexterity = Math.Max(pg.Dexterity, Math.Min(pg.Dexterity + valore, 20)); break;
            case "constitution": pg.Constitution = Math.Max(pg.Constitution, Math.Min(pg.Constitution + valore, 20)); break;
            case "intelligence": pg.Intelligence = Math.Max(pg.Intelligence, Math.Min(pg.Intelligence + valore, 20)); break;
            case "wisdom": pg.Wisdom = Math.Max(pg.Wisdom, Math.Min(pg.Wisdom + valore, 20)); break;
            case "charisma": pg.Charisma = Math.Max(pg.Charisma, Math.Min(pg.Charisma + valore, 20)); break;
        }
    }

    /// <summary>I sei punteggi correnti del personaggio, chiavi inglesi minuscole nello stesso
    /// ordine di <see cref="CharacterWizardLogic.AbilityKeyOrder"/> — per <see cref="DecisionePunteggi"/>,
    /// che li porta al dialogo perché non muta nulla prima della conferma.</summary>
    private static IReadOnlyDictionary<string, int> PunteggiAttuali(Character pg) =>
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["strength"] = pg.Strength,
            ["dexterity"] = pg.Dexterity,
            ["constitution"] = pg.Constitution,
            ["intelligence"] = pg.Intelligence,
            ["wisdom"] = pg.Wisdom,
            ["charisma"] = pg.Charisma,
        };

    /// <summary>Le facce del dado vita, lette dal primo blocco "NdM" (o "dM", la forma del dado di
    /// catalogo) di una stringa — non dalla tabella, che non le dichiara. Riusata sia per il dado di
    /// classe (<c>dadoVitaClasse</c>) che per <see cref="Character.HitDiceMax"/>: chi chiama decide
    /// cosa fare dell'esito, questo helper non sa da quale delle due fonti viene il testo.
    /// (8, true) se il testo è vuoto o scritto in una forma che non si riconosce.</summary>
    private static (int Facce, bool NonRiconosciute) FacceDado(string? testo)
    {
        if (!string.IsNullOrWhiteSpace(testo))
        {
            var primoBlocco = testo.Split('+')[0].Trim();
            var iD = primoBlocco.IndexOf('d', StringComparison.OrdinalIgnoreCase);
            if (iD >= 0 && iD + 1 < primoBlocco.Length)
            {
                var cifre = new string(primoBlocco.Skip(iD + 1).TakeWhile(char.IsDigit).ToArray());
                if (int.TryParse(cifre, out var facce) && facce > 0) return (facce, false);
            }
        }
        return (8, true);
    }

    private static int[] SlotDaPg(Character pg) => new[]
    {
        pg.SpellSlots1Max, pg.SpellSlots2Max, pg.SpellSlots3Max, pg.SpellSlots4Max, pg.SpellSlots5Max,
        pg.SpellSlots6Max, pg.SpellSlots7Max, pg.SpellSlots8Max, pg.SpellSlots9Max
    };

    /// <summary>Riempie a nove con zeri: le tabelle spesso ne dichiarano meno, e i nove campi
    /// <c>SpellSlotsNMax</c> vanno scritti tutti.</summary>
    private static int[] Nove(IReadOnlyList<int> slot)
    {
        var nove = new int[9];
        for (var i = 0; i < Math.Min(9, slot.Count); i++) nove[i] = slot[i];
        return nove;
    }

    /// <summary>Appende una riga di storico su una riga nuova, saltando i duplicati esatti — lo
    /// stesso privilegio non deve comparire due volte se il piano viene applicato più di una volta
    /// per errore, o se due decisioni distinte producono la stessa riga.</summary>
    private static string AppendUnica(string? testoAttuale, string riga)
    {
        var righeEsistenti = (testoAttuale ?? string.Empty).Split('\n').Select(r => r.TrimEnd('\r'));
        if (righeEsistenti.Any(r => r == riga)) return testoAttuale ?? string.Empty;

        return string.IsNullOrEmpty(testoAttuale) ? riga : testoAttuale + "\n" + riga;
    }
}
