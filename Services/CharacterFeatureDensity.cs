namespace DndCompanion.Services;

/// <summary>Con quanto risalto rendere un privilegio nella vista di gioco: <c>Piena</c> mostra la
/// nota per intero, <c>Riga</c> il solo nome (un tocco lo apre), <c>Spenta</c> come <c>Riga</c> ma
/// con l'indicazione visiva di contatore esaurito.</summary>
public enum DensitaPrivilegio { Piena, Riga, Spenta }

/// <summary>
/// Decide la densità di rendering di un <see cref="VistaPrivilegio"/> — la logica che tiene la vista
/// di gioco leggibile invece che un muro di testo alla stessa densità massima per tutte le voci.
/// Logica pura, gemella di <see cref="CharacterFeatureJoin"/> e <see cref="CharacterFeatureRules"/>:
/// nessuno stato, nessuna I/O, testabile senza il render.
/// </summary>
public static class CharacterFeatureDensity
{
    /// <summary>
    /// I casi, in quest'ordine — l'ordine è la specifica, non un dettaglio: decide le due
    /// precedenze che altrimenti nessun test visibile distinguerebbe (v.
    /// <c>Tests/CharacterFeatureDensityTests.cs</c>).
    ///
    /// 1. <paramref name="attiva"/> → <see cref="DensitaPrivilegio.Riga"/>. La sua nota è già
    ///    mostrata per intero dalla strip ATTIVO in cima allo schermo: ripeterla nella lista è
    ///    duplicazione.
    /// 2. Contatore esaurito (<c>Contatore.Spesi >= Contatore.Max</c>, con <c>Max &gt; 0</c>) →
    ///    <see cref="DensitaPrivilegio.Spenta"/>. Ira 0/3 non è usabile fino al riposo lungo: il
    ///    suo testo è rumore CERTIFICATO DAL DATO, non indovinato da un'euristica. Torna piena da
    ///    sola col riposo, che il tab già traccia. Viene prima del caso 3 apposta: un contatore
    ///    esaurito con anche una nota scritta dall'utente deve restare <c>Spenta</c>, non tornare
    ///    <c>Piena</c> per via della nota.
    /// 3. Ha un contatore, oppure è <see cref="VistaPrivilegio.Attivabile"/>, oppure ha una nota
    ///    SCRITTA DALL'UTENTE (<c>!NotaDiCatalogo &amp;&amp; !string.IsNullOrWhiteSpace(Nota)</c>) →
    ///    <see cref="DensitaPrivilegio.Piena"/>. Il criterio non è "aperto/chiuso", è «chi ha
    ///    scritto il testo»: ciò che l'utente ha distillato è per definizione ciò che vuole vedere;
    ///    il testo del manuale è materiale di consultazione e sta bene a un tocco di distanza.
    /// 4. Altrimenti → <see cref="DensitaPrivilegio.Riga"/>.
    /// </summary>
    public static DensitaPrivilegio Classifica(VistaPrivilegio voce, bool attiva)
    {
        if (attiva) return DensitaPrivilegio.Riga;

        var contatore = voce.Contatore;
        if (contatore is not null && contatore.Max > 0 && contatore.Spesi >= contatore.Max)
            return DensitaPrivilegio.Spenta;

        var notaDellUtente = !voce.NotaDiCatalogo && !string.IsNullOrWhiteSpace(voce.Nota);
        if (contatore is not null || voce.Attivabile || notaDellUtente)
            return DensitaPrivilegio.Piena;

        return DensitaPrivilegio.Riga;
    }

    /// <summary>Righe massime prima del troncamento: densità Piena (card) usa una soglia più
    /// larga, densità compatta (Riga/Spenta) una più stretta.</summary>
    public const int RigheClampPiena = 8;
    public const int RigheClampCompatta = 2;
    private const int CaratteriPerRigaStimati = 30;

    /// <summary>Nessuna misura DOM (niente interop, v. brief): stima quante righe occuperebbe la
    /// nota contando gli a capo espliciti e, per ogni segmento, un numero prudente di caratteri
    /// per riga su schermo di telefono. Sottostimare i caratteri per riga (quindi sovrastimare le
    /// righe) è la scelta sicura: un controllo di espansione che non serve è solo un fastidio,
    /// una nota troncata senza modo di espanderla nasconde un dato.</summary>
    public static bool NotaTroncabile(string? nota, int righeMax)
    {
        if (string.IsNullOrWhiteSpace(nota)) return false;

        var righeStimate = 0;
        foreach (var segmento in nota.Split('\n'))
            righeStimate += Math.Max(1, (int)Math.Ceiling(segmento.Length / (double)CaratteriPerRigaStimati));
        return righeStimate > righeMax;
    }
}
