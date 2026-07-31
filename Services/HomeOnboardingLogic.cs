namespace DndCompanion.Services;

/// <summary>
/// Passi del primo avvio guidato mostrato in Home: creare/entrare in una campagna, creare il
/// proprio personaggio, (solo master) invitare i compagni, aprire il tracker iniziativa. Pura e
/// testabile — stesso pattern di <see cref="BottomNavRoutes"/> — perché decidere quali passi
/// mostrare e quali sono già completati non è un dettaglio di markup: dipende da ruolo e stato
/// della campagna, con rami che meritano test propri.
/// </summary>
public static class HomeOnboardingLogic
{
    /// <summary>Come completare il passo: navigare a una rotta, oppure copiare il codice invito (nessuna rotta).</summary>
    public enum StepAction
    {
        Navigate,
        CopyInviteCode,

        /// <summary>Il passo non porta altrove: quel che serve è già in pagina (il primo passo,
        /// «crea o entra in una campagna», si compie nel selettore in cima alla Home). Esiste come
        /// valore proprio perché <see cref="StepAction.Navigate"/> con <c>Route</c> nullo era uno
        /// stato che il contratto del record non prevede, e obbligava la pagina a difendersi con
        /// un controllo aggiuntivo per non chiamare una navigazione verso null.</summary>
        None,
    }

    /// <param name="Title">Titolo breve del passo.</param>
    /// <param name="Description">Una riga di spiegazione in italiano semplice.</param>
    /// <param name="Action">Come si completa: v. <see cref="StepAction"/>.</param>
    /// <param name="Route">Rotta di destinazione se <see cref="Action"/> è <see cref="StepAction.Navigate"/>; altrimenti null.</param>
    /// <param name="IsDone">Se il passo risulta già completato, dato lo stato corrente.</param>
    public sealed record Step(string Title, string Description, StepAction Action, string? Route, bool IsDone);

    /// <summary>
    /// Costruisce i passi per il contesto corrente, nell'ordine in cui vanno fatti.
    /// </summary>
    /// <param name="hasActiveCampaign">C'è una campagna selezionata come attiva.</param>
    /// <param name="isMaster">Ruolo dell'utente nella campagna attiva (irrilevante se non c'è una campagna).</param>
    /// <param name="hasOwnCharacter">L'utente ha già almeno un personaggio in questa campagna.</param>
    /// <param name="hasOtherMembers">La campagna ha membri oltre al master (almeno un giocatore invitato).</param>
    /// <remarks>
    /// Senza campagna attiva il percorso si ferma al primo passo (comprensibilmente: creare/entrare
    /// nella campagna sblocca tutto il resto). Il passo "invita i compagni" compare solo per il
    /// master: un giocatore non invita, entra con il codice altrui. L'ultimo passo (tracker
    /// iniziativa) non ha un segnale di completamento persistito — aprire il tracker non lascia
    /// traccia in nessuna tabella, e aggiungerne una solo per questo sarebbe una migrazione fuori
    /// scopo — quindi resta sempre "da fare": è <see cref="IsSetupComplete"/> a ignorarlo nel
    /// decidere se il percorso guidato può sparire (v. lì).
    /// </remarks>
    public static IReadOnlyList<Step> BuildSteps(
        bool hasActiveCampaign,
        bool isMaster,
        bool hasOwnCharacter,
        bool hasOtherMembers)
    {
        var steps = new List<Step>
        {
            new("Crea o entra in una campagna",
                "Il punto di partenza: creane una nuova oppure unisciti con un codice invito.",
                StepAction.None,
                Route: null,
                IsDone: hasActiveCampaign),
        };

        if (!hasActiveCampaign) return steps;

        steps.Add(new("Crea il tuo personaggio",
            "Un wizard guidato ti accompagna passo passo: classe, caratteristiche, equipaggiamento.",
            StepAction.Navigate,
            Route: "characters/nuovo",
            IsDone: hasOwnCharacter));

        if (isMaster)
        {
            steps.Add(new("Invita i tuoi compagni",
                "Condividi il codice invito qui sopra: basta per farli entrare nella campagna.",
                StepAction.CopyInviteCode,
                Route: null,
                IsDone: hasOtherMembers));
        }

        steps.Add(new(
            isMaster ? "Apri il tracker iniziativa" : "Segui l'iniziativa",
            isMaster
                ? "Da lì aggiungi i combattenti e gestisci i turni."
                : "La trovi anche nella barra di navigazione, quando il Master la avvia.",
            StepAction.Navigate,
            Route: "combat",
            IsDone: false));

        return steps;
    }

    /// <summary>
    /// Vero quando tutti i passi TRACCIABILI sono completati (personaggio creato, e per il master
    /// anche l'invito). L'ultimo passo (tracker iniziativa) non è tracciabile — v. la nota su
    /// <see cref="BuildSteps"/> — e va escluso dal conto: altrimenti il percorso guidato non
    /// sparirebbe mai, restando visibile in Home anche a campagna avviata da mesi. Con questo,
    /// la guida compare durante il primo avvio e si toglie da sola una volta che campagna,
    /// personaggio e (per il master) compagni ci sono: da lì in poi basta la barra di navigazione.
    /// </summary>
    public static bool IsSetupComplete(IReadOnlyList<Step> steps) =>
        steps.Count > 1 && steps.Take(steps.Count - 1).All(s => s.IsDone);
}
