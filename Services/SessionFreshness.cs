namespace DndCompanion.Services;

/// <summary>
/// In gotrue-csharp 4.2.7 <c>Client.RefreshSession()</c> e <c>Client.RefreshToken()</c> (senza
/// argomenti) contengono una guardia che lancia "Session expired" se l'access token è GIÀ scaduto:
/// la libreria si rifiuta di usare il refresh token proprio nel caso in cui servirebbe. La
/// correzione (in <see cref="SupabaseService"/>) è rinfrescare PRIMA che scada, con l'overload a
/// due argomenti che non ha quella guardia. Questa classe isola le due decisioni pure che servono
/// per farlo, testabili senza istanziare un client Gotrue: quando è il momento
/// (<see cref="VaRinfrescata"/>) e se ha senso ritentare dopo un fallimento
/// (<see cref="SiPuoRitentare"/>), per non martellare il server quando la rete è giù.
/// </summary>
public static class SessionFreshness
{
    /// <summary>Margine di sicurezza: si rinfresca prima della scadenza vera, così una richiesta
    /// partita subito dopo il controllo non si trova col token morto a metà strada.</summary>
    public static readonly TimeSpan Margine = TimeSpan.FromMinutes(5);

    /// <summary>Intervallo minimo fra due tentativi falliti, per non martellare il server quando
    /// la rete è giù: senza, ogni chiamata dati riproverebbe subito.</summary>
    public static readonly TimeSpan AttesaDopoFallimento = TimeSpan.FromSeconds(30);

    /// <summary>True se la sessione che scade a <paramref name="scadenzaUtc"/> va rinfrescata ora.</summary>
    public static bool VaRinfrescata(DateTime scadenzaUtc, DateTime adessoUtc)
        => adessoUtc + Margine >= scadenzaUtc;

    /// <summary>True se è passato abbastanza tempo dall'ultimo tentativo fallito.
    /// <paramref name="ultimoFallimentoUtc"/> null = nessun tentativo fallito finora.</summary>
    public static bool SiPuoRitentare(DateTime? ultimoFallimentoUtc, DateTime adessoUtc)
        => ultimoFallimentoUtc is null || adessoUtc - ultimoFallimentoUtc.Value >= AttesaDopoFallimento;
}
