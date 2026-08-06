namespace DndCompanion.Services;

/// <summary>Un valore che il dialogo mostra come diff: sempre attuale e proposto affiancati.
/// La UI decide se e come segnalare la differenza; qui non si giudica.</summary>
public sealed record Proposta<T>(T Attuale, T Proposto);

/// <summary>Una voce selezionabile. <paramref name="Descrizione"/> è il testo del catalogo, che il
/// dialogo mostra in accordion — può essere lungo (le sottoclassi sfiorano i 3.000 caratteri).</summary>
public sealed record OpzioneDecisione(string Nome, string Descrizione);

/// <summary>Qualcosa che il giocatore deve decidere per poter salire. La <paramref name="Chiave"/>
/// ha forma <c>L{livello}:{tipo}</c> — <c>L4:talento</c>, <c>L4:talento/punteggi</c>,
/// <c>L3:sottoclasse</c> — ed è la stessa che compare come prefisso nelle righe appese ai campi
/// testuali del personaggio.</summary>
public abstract record Decisione(string Chiave, string Titolo);

/// <summary>Scelta fra voci note al catalogo: sottoclasse, talento, stile di combattimento, dono
/// epico. <paramref name="Quante"/> è quante se ne devono scegliere (quasi sempre 1).</summary>
public sealed record DecisioneFraOpzioni(
    string Chiave, string Titolo, IReadOnlyList<OpzioneDecisione> Opzioni, int Quante)
    : Decisione(Chiave, Titolo);

/// <summary>La ripartizione dell'incremento di caratteristica: +2 a una, oppure +1 a due. Compare
/// solo come figlia di una <see cref="DecisioneFraOpzioni"/> in cui è stato scelto il talento
/// dell'incremento. <paramref name="PunteggiAttuali"/> porta i sei valori correnti del
/// personaggio (chiavi inglesi minuscole, lo stesso ordine di
/// <see cref="CharacterWizardLogic.AbilityKeyOrder"/>): senza di questi il dialogo non saprebbe
/// quale caratteristica è già a 20 e disabilitare il relativo "+".</summary>
public sealed record DecisionePunteggi(
    string Chiave, string Titolo, IReadOnlyDictionary<string, int> PunteggiAttuali)
    : Decisione(Chiave, Titolo);

/// <summary>Scelta di cui il catalogo non conosce le opzioni (invocazioni occulte, metamagia,
/// maestrie). Si annota in prosa. <paramref name="Avviso"/> è il testo che spiega perché non c'è un
/// elenco. È sempre facoltativa: non blocca la conferma.</summary>
public sealed record DecisioneLibera(string Chiave, string Titolo, string Avviso)
    : Decisione(Chiave, Titolo);

/// <summary>La risposta a una <see cref="Decisione"/>. Solo il campo che compete alla forma della
/// decisione è valorizzato; gli altri restano vuoti.</summary>
public sealed record Risposta
{
    /// <summary>I nomi scelti, per <see cref="DecisioneFraOpzioni"/>.</summary>
    public IReadOnlyList<string> Scelte { get; init; } = Array.Empty<string>();

    /// <summary>Incrementi per <see cref="DecisionePunteggi"/>: chiavi inglesi minuscole
    /// (<c>strength</c>, <c>dexterity</c>, <c>constitution</c>, <c>intelligence</c>, <c>wisdom</c>,
    /// <c>charisma</c>) e valori che sommano a 2.</summary>
    public IReadOnlyDictionary<string, int> Punteggi { get; init; } =
        new Dictionary<string, int>(StringComparer.Ordinal);

    /// <summary>Il testo, per <see cref="DecisioneLibera"/>.</summary>
    public string Testo { get; init; } = string.Empty;
}

/// <summary>Cosa comporta salire di un livello, viste le risposte date finora. È un diff
/// **proposto**: nessun campo del personaggio è stato toccato.
///
/// Il piano <b>non è stabile</b>: va ricalcolato a ogni risposta, perché l'incremento di
/// Costituzione cambia i punti ferita di tutti i livelli già posseduti.</summary>
public sealed record LevelUpPlan(
    string Classe,
    int LivelloDa,
    int LivelloA,
    /// <summary>Il dado vita della classe ("d12"), per il selettore del tiro.</summary>
    string DadoVita,
    /// <summary>La media del dado ("d12" → 7), già calcolata qui: il dialogo la mostra nel
    /// selettore del tiro e non deve riparsare <see cref="DadoVita"/> per ricavarla da sé — due
    /// implementazioni dello stesso fatto avrebbero potuto divergere.</summary>
    int MediaDado,
    Proposta<int> PuntiFeritaMax,
    Proposta<int> PuntiFeritaCorrenti,
    Proposta<string> DadiVita,
    /// <summary>Nove valori, dal 1° al 9° cerchio.</summary>
    Proposta<IReadOnlyList<int>> SlotMax,
    Proposta<string> CaratteristicaIncantatore,
    Proposta<int> BonusCompetenza,
    IReadOnlyList<string> PrivilegiOttenuti,
    IReadOnlyList<Decisione> Decisioni,
    /// <summary>Incoerenze rilevate: si mostrano, non si correggono.</summary>
    IReadOnlyList<string> Avvisi,
    /// <summary>Il cerchio di incantesimi che si apre per la prima volta, null se nessuno.</summary>
    int? CerchioSbloccato)
{
    /// <summary>Vero se ogni decisione che blocca la conferma ha una risposta valida. Le
    /// <see cref="DecisioneLibera"/> non bloccano mai: annotare è un servizio, non un obbligo.
    /// Cicla su <see cref="Completa"/> invece di riscrivere la stessa regola qui.</summary>
    public bool Completo(IReadOnlyDictionary<string, Risposta>? risposte)
    {
        foreach (var d in Decisioni)
        {
            Risposta? r = null;
            risposte?.TryGetValue(d.Chiave, out r);
            if (!Completa(d, r)) return false;
        }
        return true;
    }

    /// <summary>Vero se questa decisione è soddisfatta da questa risposta. Estratta da
    /// <see cref="Completo"/> perché il dialogo la riusa per la pillola di stato di una singola
    /// riga, e non deve duplicare la regola per conto suo.</summary>
    public bool Completa(Decisione decisione, Risposta? risposta)
    {
        if (decisione is DecisioneLibera) return true;
        if (risposta is null) return false;

        return decisione switch
        {
            DecisioneFraOpzioni f => risposta.Scelte.Count == f.Quante,
            DecisionePunteggi => PunteggiValidi(risposta.Punteggi),
            _ => true
        };
    }

    /// <summary>Le sole sei chiavi che una <see cref="DecisionePunteggi"/> può usare — la stessa
    /// fonte di <see cref="CharacterWizardLogic.AbilityKeyOrder"/>, non un elenco duplicato.</summary>
    private static readonly HashSet<string> CaratteristicheAmmesse =
        new(CharacterWizardLogic.AbilityKeyOrder, StringComparer.Ordinal);

    /// <summary>Vero se la ripartizione dei punti è una delle due forme legali della 5e 2024: +2 a
    /// una sola caratteristica, oppure +1 a due caratteristiche distinte. Il solo totale non basta:
    /// somma a 2 anche <c>{"strength": 5, "constitution": -3}</c>, che scriverebbe una Costituzione
    /// ABBASSATA di 3 — <see cref="LevelUpPlanner.Applica"/> si fida di questo controllo e non
    /// riverifica la forma.</summary>
    private static bool PunteggiValidi(IReadOnlyDictionary<string, int> punteggi)
    {
        if (punteggi.Count is not (1 or 2)) return false;

        var somma = 0;
        foreach (var (chiave, valore) in punteggi)
        {
            if (!CaratteristicheAmmesse.Contains(chiave)) return false;
            if (valore is not (1 or 2)) return false;
            somma += valore;
        }
        return somma == 2;
    }
}
