using DndCompanion.Models;
using DndCompanion.Models.Packages;

namespace DndCompanion.Services;

/// <summary>Un livello della catena: il piano che il planner ha proposto per quel livello, se
/// richiede ancora scelte dal giocatore che BLOCCANO l'avanzamento (<see cref="RichiedeScelte"/>),
/// e se ne contiene altre che NON bloccano (<see cref="HaScelteFacoltative"/>).
///
/// I due flag distinguono due esiti diversi, non uno solo con due nomi: "non c'è nulla da
/// decidere" (entrambi falsi) e "c'è qualcosa che non blocca ma merita un tap" (solo
/// <see cref="HaScelteFacoltative"/> vero). Una <see cref="DecisioneLibera"/> non blocca MAI
/// <see cref="LevelUpPlan.Completa"/> — è facoltativa per contratto — ma "non blocca" non vuol dire
/// "non c'è": con l'auto-conferma a elenco della progressione (che tratterà come vuota ogni tappa
/// con <see cref="RichiedeScelte"/> falso) un Warlock di 5° si salverebbe senza le invocazioni
/// occulte annotate, e una classe di tavolo senza sottoclassi a catalogo (che apre una
/// <see cref="DecisioneLibera"/> proprio per la sottoclasse) nascerebbe senza sottoclasse — in
/// entrambi i casi in silenzio, perché nulla avrebbe richiesto un tap.</summary>
public sealed record TappaCreazione(int Livello, LevelUpPlan Piano, bool RichiedeScelte, bool HaScelteFacoltative);

/// <summary>L'esito del fold. Personaggio è SEMPRE un'istanza nuova, mai il baseline ricevuto.
/// Completa = si può salvare. Motivo = perché non si può, null se si può. Avvisi = incoerenze che
/// NON impediscono il salvataggio: le anomalie della tabella rilevate dal fold stesso (es. una
/// tabella di classe dichiarata solo in parte) PIÙ gli avvisi di ogni <see cref="LevelUpPlan.Avvisi"/>
/// incontrato lungo la catena, deduplicati (MINORE 5 del gate del 2026-08-06, secondo giro) — senza
/// questi ultimi, un avviso come "Dado vita non riconosciuto" resterebbe visibile solo dentro
/// <see cref="TappaCreazione.Piano"/> di ogni singola tappa. Si mostrano, non si correggono —
/// nello stesso spirito di <see cref="LevelUpPlan.Avvisi"/>.</summary>
public sealed record EsitoCatena(
    Character Personaggio,
    IReadOnlyList<TappaCreazione> Tappe,
    bool Completa,
    string? Motivo,
    IReadOnlyList<string> Avvisi);

/// <summary>Il fold che deriva un personaggio al livello N a partire dal suo baseline (di solito il
/// livello 1 prodotto dal wizard), riusando <see cref="LevelUpPlanner"/> un livello alla volta.
/// Helper puro `static`, nessuno stato, nessuna I/O — NON un secondo motore di progressione: non
/// calcola punti ferita, slot o competenza per conto proprio, li chiede a
/// <see cref="LevelUpPlanner.Pianifica"/> e li scrive con <see cref="LevelUpPlanner.Applica"/>. Il
/// wizard possiede il livello 1, questa classe possiede i livelli 2→N (v.
/// <c>docs/superpowers/specs/2026-08-06-creazione-guidata-design.md</c>, «Il principio»).</summary>
public static class CreationChain
{
    /// <summary>Deriva il personaggio al livello <paramref name="livelloRichiesto"/> a partire dal
    /// baseline, un livello alla volta.
    ///
    /// <para><b>ATTENZIONE, chiamante: il baseline deve arrivare già COMPLETO — le sei
    /// caratteristiche finali (<c>Strength</c> … <c>Charisma</c>) comprese — prima di chiamare
    /// questo metodo.</b> Il fold legge quei sei punteggi a ogni livello per calcolare i punti
    /// ferita (il modificatore di Costituzione entra nel calcolo passo per passo, non solo una
    /// volta alla fine). Se il chiamante sincronizza le caratteristiche finali sul draft DOPO aver
    /// chiamato <see cref="Deriva"/> — come fa oggi <c>CharacterWizard</c> in coda al salvataggio —
    /// un incremento di caratteristica maturato qui dentro (l'ASI del 4°, per esempio) verrebbe
    /// silenziosamente sovrascritto dal sync tardivo: build verde, test verdi, e il talento del 4°
    /// semplicemente non ci sarebbe più nella scheda salvata. Sincronizza PRIMA, deriva DOPO.</para>
    ///
    /// <para>Non muta <paramref name="baseline"/>: la prima riga utile lo clona. Il chiamante
    /// rieseguirà questo metodo da capo a ogni cambiamento — cambio di una risposta, cambio del
    /// livello richiesto, cambio di una caratteristica base — perché è così che il wizard "torna
    /// indietro": non un undo, un replay dal baseline. Un baseline mutato corromperebbe ogni
    /// esecuzione successiva.</para>
    ///
    /// <paramref name="livelloRichiesto"/> minore o uguale al livello del baseline (tipicamente 1)
    /// non è un errore: nessun giro, <see cref="EsitoCatena.Tappe"/> vuota, esito completo.
    ///
    /// Per ogni livello k da <c>baseline.Level + 1</c> a <paramref name="livelloRichiesto"/> chiama
    /// <see cref="LevelUpPlanner.Pianifica"/> con le risposte e il tiro DI QUEL livello (assenti →
    /// null, cioè la media del dado — mai lo stesso tiro riusato per livelli diversi, che darebbe lo
    /// stesso dado a ogni livello). Se il piano torna null la catena si interrompe lì: la classe non
    /// ha una tabella di progressione (o il personaggio ha già raggiunto il 20°), e il motivo è
    /// dichiarato in <see cref="EsitoCatena.Motivo"/>, non un silenzio.
    ///
    /// Se il piano c'è, il livello avanza SEMPRE — punti ferita, dadi vita, slot, bonus di
    /// competenza compresi — anche quando una delle sue decisioni (sottoclasse, talento,
    /// ripartizione dei punteggi) non ha ancora risposta: il giocatore deve poter vedere i punti
    /// ferita crescere livello per livello mentre decide, non solo alla fine. Le decisioni senza
    /// risposta restano semplicemente NON applicate — nessuna sottoclasse inventata, nessun talento
    /// fittizio, nessun punteggio alzato a caso: sarebbe una scelta rubata al giocatore, non un
    /// default — e la tappa lo segnala con <see cref="TappaCreazione.RichiedeScelte"/>.
    /// <see cref="EsitoCatena.Completa"/> finale è vero solo se NESSUNA tappa lo richiede.</summary>
    public static EsitoCatena Deriva(
        Character baseline,
        int livelloRichiesto,
        string? testoProgressione,
        IReadOnlyList<PackageSubclass>? sottoclassi,
        IReadOnlyList<PackageFeat>? talenti,
        IReadOnlyDictionary<int, IReadOnlyDictionary<string, Risposta>>? rispostePerLivello,
        IReadOnlyDictionary<int, int>? tiriPerLivello,
        string? dadoVitaClasse = null)
    {
        var pg = CharacterClone.Clona(baseline);

        if (livelloRichiesto <= baseline.Level)
            return new EsitoCatena(pg, Array.Empty<TappaCreazione>(), Completa: true, Motivo: null, Avvisi: Array.Empty<string>());

        // Calcolata una sola volta: non cambia livello per livello (dipende solo dal testo), e la
        // riusano sia il ramo "nessuna tabella" sotto sia il controllo di riga mancante (SERIO 2 /
        // MINORE 11).
        var righeTabella = ClassProgression.Leggi(testoProgressione);
        // SERIO 2 del gate del 2026-08-06 (secondo giro): il criterio di "riga mancante" guarda
        // SOLO a questo valore — i livelli oltre l'ultima riga dichiarata — non a ogni livello
        // senza una riga propria. V. il commento sopra a livelliSenzaRiga.Add più sotto: una
        // tabella sparsa con buchi INTERNI è normale e attesa in questo formato.
        var ultimoLivelloDichiarato = righeTabella.Count == 0 ? 0 : righeTabella.Max(r => r.Livello);

        var tappe = new List<TappaCreazione>();
        var livelliSenzaRiga = new List<int>();
        // MINORE 5 del gate del 2026-08-06 (secondo giro): gli avvisi dei singoli piani ("Dado vita
        // non riconosciuto…", "Dadi vita da più classi…") sono identici a ogni livello — dipendono
        // da pg/testoProgressione/dadoVitaClasse, non dalle risposte — e qui si accumulano grezzi;
        // la deduplica avviene una sola volta in fondo, prima di unirli a Avvisi.
        var avvisiPiani = new List<string>();
        string? motivoInterruzione = null;

        // La sottoclasse è UNA sola scelta nella catena, quella del livello in cui compare la prima
        // volta (SERIO 3 del gate del 2026-08-06): se resta senza risposta, memorizza qui la chiave
        // di quella prima decisione — la chiave in sé serve solo per diagnosi e leggibilità: il
        // codice la legge come flag null/non-null (v. sotto), non la confronta con nulla. La
        // soppressione degli echi che CostruisciDecisioni (dentro Pianifica) ricrea ai livelli
        // successivi avviene per SUFFISSO (":sottoclasse"): ogni eco è la STESSA scelta
        // ripresentata sotto una chiave di livello diverso (es. "L6:sottoclasse" invece di
        // "L3:sottoclasse") — Barbaro 3-6-10-14, Guerriero 3-7-10-15-18, Ladro 3-9-13-17, e così per
        // le altre nove classi — perché ai loro occhi pg.Subclass è ancora vuota. Quando la
        // sottoclasse VIENE risposta, Applica scrive pg.Subclass e CostruisciDecisioni smette da sé
        // di generarne altre: quel caso non passa da qui e resta quello di prima, non va toccato.
        //
        // "Risposta data" qui NON è sinonimo di piano.Completa(decisione, risposta) (SERIO 1 del
        // gate del 2026-08-06, secondo giro): Completa apre con `if (decisione is DecisioneLibera)
        // return true` — SEMPRE, risposta null compresa (v. LevelUpContracts.cs) — e
        // CostruisciDecisioni apre proprio una DecisioneLibera per la sottoclasse quando il
        // catalogo non ha sottoclassi per questa classe. Con Completa come criterio,
        // sottoclasseInSospesoChiave non si sarebbe MAI valorizzata per quelle classi, e ogni eco
        // successivo sarebbe comparso come una scheda separata invece che restare soppresso come al
        // livello della prima decisione. Serve la stessa nozione che haScelteFacoltative applica
        // poco sotto per le DecisioneLibera: "risposta data" vuol dire testo non vuoto.
        //
        // Se una risposta arriva depositata sotto una chiave GIÀ soppressa (es. una risposta per
        // "L6:sottoclasse" mentre "L3:sottoclasse" è ancora in sospeso), viene ignorata in silenzio:
        // quella decisione non è in decisioniVisibili, quindi non entra mai in risposteValide più
        // sotto e Applica non la scrive (MINORE 6 del gate del 2026-08-06, secondo giro). È
        // deliberato: l'interfaccia attuale non può produrre questo stato — la scheda del 6° non si
        // mostra finché il 3° resta aperto — e "ripescare" quella risposta in automatico creerebbe
        // uno stato confuso (sottoclasse scritta al 6° con il 3° ancora presentato come da
        // decidere). Se un domani le risposte venissero persistite e lo stato diventasse
        // raggiungibile, il comportamento va deciso da capo, non riscoperto per caso — per questo è
        // fissato da un test (v. Risposta_sotto_chiave_soppressa_viene_ignorata).
        string? sottoclasseInSospesoChiave = null;

        for (var k = baseline.Level + 1; k <= livelloRichiesto; k++)
        {
            IReadOnlyDictionary<string, Risposta>? risposteLivello = null;
            rispostePerLivello?.TryGetValue(k, out risposteLivello);

            int? tiroLivello = null;
            if (tiriPerLivello is not null && tiriPerLivello.TryGetValue(k, out var tiro))
                tiroLivello = tiro;

            var piano = LevelUpPlanner.Pianifica(
                pg, testoProgressione, sottoclassi, talenti, risposteLivello, tiroLivello, dadoVitaClasse);

            if (piano is null)
            {
                // Due motivi possibili dietro lo stesso null di Pianifica: nessuna tabella da cui
                // leggere, o livello massimo già raggiunto. Si distinguono guardando la stessa
                // fonte che Pianifica legge, non duplicandone la logica.
                motivoInterruzione = righeTabella.Count == 0
                    ? $"Questa classe non ha una tabella dei livelli: la progressione si ferma al {pg.Level}°."
                    : $"Il personaggio ha già raggiunto il livello massimo (20°): la progressione si ferma al {pg.Level}°.";
                break;
            }

            // SERIO 2 del gate del 2026-08-06 (secondo giro): una tabella SPARSA con buchi INTERNI
            // (es. "L3 — Sottoclasse\nL5 — Attacco extra", senza una riga per L4) è normale e
            // ATTESA in questo formato — ClassProgression.Serializza omette di proposito le righe
            // senza privilegi né slot ("una riga vuota non aggiunge nulla"), e PrivilegiFinoAl
            // filtra già su Privilegi.Count > 0. Segnalare quei buchi come "riga mancante"
            // avviserebbe su dati CORRETTI, e con un testo sbagliato: per un buco interno
            // "ripetere l'ultimo livello dichiarato" (SlotFinoAl) è il comportamento giusto, non
            // un'anomalia.
            //
            // L'unico caso davvero anomalo è la tabella dichiarata solo IN PARTE (MINORE 11 del
            // gate del 2026-08-06: es. fino al 5°, personaggio richiesto all'8°) — lì i livelli
            // OLTRE l'ultima riga dichiarata (ultimoLivelloDichiarato, sopra) non hanno alcuna riga
            // da cui SlotFinoAl/PrivilegiFinoAl possano ripetere qualcosa: un 8° con i privilegi e
            // gli slot di un 5°, Completa = true, Motivo = null, nessun segnale senza questo
            // controllo. È un dato mancante del catalogo, non una scelta del giocatore: si registra
            // come avviso — si mostra, non blocca il salvataggio.
            if (k > ultimoLivelloDichiarato)
                livelliSenzaRiga.Add(k);

            avvisiPiani.AddRange(piano.Avvisi);

            IReadOnlyList<Decisione> decisioniVisibili = sottoclasseInSospesoChiave is null
                ? piano.Decisioni
                : piano.Decisioni
                    .Where(d => !d.Chiave.EndsWith(":sottoclasse", StringComparison.Ordinal))
                    .ToList();

            if (sottoclasseInSospesoChiave is null)
            {
                var decisioneSottoclasse = decisioniVisibili.FirstOrDefault(
                    d => d.Chiave.EndsWith(":sottoclasse", StringComparison.Ordinal));
                if (decisioneSottoclasse is not null)
                {
                    Risposta? rSottoclasse = null;
                    risposteLivello?.TryGetValue(decisioneSottoclasse.Chiave, out rSottoclasse);

                    // SERIO 1: v. il commento sopra a sottoclasseInSospesoChiave — Completa non
                    // basta come "risposta data" per una DecisioneLibera, che lo restituisce
                    // sempre vero.
                    var risolta = decisioneSottoclasse is DecisioneLibera
                        ? rSottoclasse is not null && !string.IsNullOrWhiteSpace(rSottoclasse.Testo)
                        : piano.Completa(decisioneSottoclasse, rSottoclasse);
                    if (!risolta)
                        sottoclasseInSospesoChiave = decisioneSottoclasse.Chiave;
                }
            }

            var pianoVisibile = piano with { Decisioni = decisioniVisibili };

            var richiedeScelte = !pianoVisibile.Completo(risposteLivello);

            // MINORE 10: le DecisioneLibera non contano per RichiedeScelte (non bloccano mai), ma
            // devono restare visibili come "c'è qualcosa che non blocca" — v. il commento XML su
            // TappaCreazione.HaScelteFacoltative.
            var haScelteFacoltative = pianoVisibile.Decisioni.Any(d =>
            {
                if (d is not DecisioneLibera) return false;
                Risposta? r = null;
                risposteLivello?.TryGetValue(d.Chiave, out r);
                return r is null || string.IsNullOrWhiteSpace(r.Testo);
            });

            // I campi numerici del piano applicato devono derivare dalle STESSE risposte che
            // Applica scriverà davvero, non da quelle intere. LevelUpPlanner.IncrementoCostituzione
            // legge risposte[...].Punteggi["constitution"] SENZA passare da LevelUpPlan.PunteggiValidi
            // (che è privato: solo Completa/Completo lo consultano). Con la guardia tutto-o-niente
            // di Applica quel dettaglio era innocuo — un piano incompleto non scriveva comunque
            // nulla — ma qui quella guardia è appositamente elusa (il livello avanza sempre): una
            // risposta malformata come {constitution: 1} (un solo "+" su Costituzione, non i due
            // punti che la 5e 2024 richiede) farebbe calcolare i punti ferita su un incremento che
            // poi Applica si rifiuta di scrivere — build verde, PF sbagliati (SERIO 3 del gate del
            // 2026-08-06).
            //
            // Si pianifica due volte, non toccando LevelUpPlanner: la prima (sopra, `piano`) con
            // TUTTE le risposte del livello, perché la TappaCreazione deve poter mostrare anche le
            // decisioni ancora aperte; la seconda (sotto, `pianoNumerico`) con le SOLE risposte che
            // soddisfano davvero la propria decisione secondo pianoVisibile.Completa — i suoi campi
            // numerici sono quindi calcolati esattamente sull'incremento che sta per essere
            // scritto, non su uno che non lo sarà mai. Pianifica è pura e senza I/O: richiamarla due
            // volte non costa nulla di rilevante.
            var risposteValide = new Dictionary<string, Risposta>(StringComparer.Ordinal);
            foreach (var d in pianoVisibile.Decisioni)
            {
                Risposta? r = null;
                risposteLivello?.TryGetValue(d.Chiave, out r);
                if (r is not null && pianoVisibile.Completa(d, r))
                    risposteValide[d.Chiave] = r;
            }

            var pianoNumerico = LevelUpPlanner.Pianifica(
                pg, testoProgressione, sottoclassi, talenti, risposteValide, tiroLivello, dadoVitaClasse);
            // Non può essere null qui: le sole condizioni che fanno tornare null Pianifica (nessuna
            // tabella, livello massimo) dipendono da pg e testoProgressione, non dalle risposte —
            // sono le stesse che hanno già fatto proseguire `piano` qualche riga sopra.
            var decisioniDaApplicare = pianoNumerico!.Decisioni
                .Where(d =>
                {
                    risposteValide.TryGetValue(d.Chiave, out var r);
                    return pianoNumerico.Completa(d, r);
                })
                .ToList();
            var pianoDaApplicare = pianoNumerico with { Decisioni = decisioniDaApplicare };

            LevelUpPlanner.Applica(pg, pianoDaApplicare, risposteValide);

            // SERIO 4 del gate del 2026-08-06 (secondo giro): la tappa mostrata deve portare i
            // numeri di CIÒ CHE VIENE SCRITTO (pianoNumerico, calcolato sopra sulle sole
            // risposteValide), non quelli di `piano`/`pianoVisibile` (calcolati su TUTTE le
            // risposte del livello, malformate comprese) — altrimenti una risposta malformata ai
            // punteggi fa annunciare alla tappa punti ferita che il personaggio non riceverà, e la
            // tappa successiva li mostra tornare indietro. Le decisioni restano quelle VISIBILI
            // (decisioniVisibili: il piano intero meno l'eco di sottoclasse soppresso): sono ciò
            // che resta aperto al giocatore, non ciò che verrà scritto in questo giro. Completo e
            // Completa dipendono solo da Decisioni e risposte — mai dai campi numerici — quindi
            // richiedeScelte e haScelteFacoltative (già calcolati sopra da pianoVisibile, che porta
            // le stesse Decisioni) restano validi anche per questa tappa; Avvisi, PrivilegiOttenuti,
            // SlotMax e BonusCompetenza non dipendono dalle risposte, quindi non cambiano fra
            // `piano` e `pianoNumerico` e non si perde alcun avviso passando all'uno o all'altro.
            var pianoTappa = pianoNumerico with { Decisioni = decisioniVisibili };
            tappe.Add(new TappaCreazione(k, pianoTappa, richiedeScelte, haScelteFacoltative));
        }

        var livelliMancanti = tappe.Where(t => t.RichiedeScelte).Select(t => t.Livello).ToList();
        var completa = motivoInterruzione is null && livelliMancanti.Count == 0;

        var motivo = motivoInterruzione ?? (livelliMancanti.Count switch
        {
            0 => null,
            1 => $"Resta 1 scelta da fare (livello {livelliMancanti[0]}).",
            _ => $"Restano {livelliMancanti.Count} scelte da fare (livelli {string.Join(", ", livelliMancanti)})."
        });

        var avvisi = new List<string>();
        if (livelliSenzaRiga.Count > 0)
        {
            avvisi.Add(livelliSenzaRiga.Count == 1
                ? $"La tabella dei livelli non ha una riga per il livello {livelliSenzaRiga[0]}: " +
                  "i privilegi si fermano all'ultimo livello dichiarato e gli slot lo ripetono."
                : $"La tabella dei livelli non ha una riga per i livelli {string.Join(", ", livelliSenzaRiga)}: " +
                  "i privilegi si fermano all'ultimo livello dichiarato e gli slot lo ripetono.");
        }

        // MINORE 5 del gate del 2026-08-06 (secondo giro): senza questo, "Dado vita non
        // riconosciuto: uso d8 come stima." e "Dadi vita da più classi: aggiornali a mano."
        // resterebbero visibili solo dentro Tappe[k].Piano.Avvisi, ripetuti a ogni livello — e chi
        // legge EsitoCatena.Avvisi (la lettura naturale del nome) perderebbe proprio l'avviso che
        // spiega perché i punti ferita proposti sono quelli. Distinct() preserva il primo ordine di
        // apparizione ed è la deduplica: sono lo stesso avviso a ogni livello, non vanno ripetuti.
        avvisi.AddRange(avvisiPiani.Distinct());

        return new EsitoCatena(pg, tappe, completa, motivo, avvisi);
    }
}
